using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using Akka.DependencyInjection;
using Akka.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTrack.Common;
using RTrackServer.Domain;

namespace RTrackServer.Services
{
    public interface ITrackerService
    {
        Task<ClientTracker[]> GetTrackingClients();
    }

    public sealed class TrackerService : IHostedService, ITrackerService
    {
        ActorSystem system = null!;
        readonly IServiceProvider serviceProvider;

        IActorRef tracker = ActorRefs.Nobody;

        public TrackerService(IServiceProvider serviceProvider) {
            this.serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            var port = int.Parse(serviceProvider.GetRequiredService<IConfiguration>()["TrackerPort"] ?? "9999");
            var config = ConfigurationFactory.ParseString(File.ReadAllText("app.hocon.conf")).BootstrapFromDocker();
            var boostrap = BootstrapSetup.Create()
                                         .WithConfig(config)
                                         .WithActorRefProvider(ProviderSelection.Local.Instance)
                                         .And(DependencyResolverSetup.Create(serviceProvider));
            system = ActorSystem.Create("RTracker", boostrap);

            tracker = system.ActorOf(Props.Create(() => new Tracker(serviceProvider.GetRequiredService<ILogger<Tracker>>(), port)));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) =>
            CoordinatedShutdown.Get(system).Run(CoordinatedShutdown.ClrExitReason.Instance);

        public Task<ClientTracker[]> GetTrackingClients() =>
            tracker.Ask<ClientTracker[]>(new Tracker.GetTrackedClients());
    }

    sealed class Tracker : ReceiveActor
    {
        readonly Dictionary<IP4EndPoint, ClientTracker> trackedClients = new ();

        public Tracker(ILogger logger, int port) {
            logger.LogDebug("Tracker is being started!");
            Context.System.Tcp().Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port)));

            Receive<Tcp.Bound>(bound => logger.LogInformation("Listening on {$Address}", bound.LocalAddress));
            Receive<Tcp.Connected>(client => {
                var endpoint = IP4EndPoint.From((IPEndPoint)client.RemoteAddress);
                logger.LogInformation("New connection from {EndPoint}", endpoint);

                var tracker = new ClientTracker(endpoint);
                trackedClients[endpoint] = tracker;
                var connection = Context.ActorOf(Props.Create(() => new TrackerConnection(logger, tracker, Sender)));
                Sender.Tell(new Tcp.Register(connection));
            });

            Receive<GetTrackedClients>(_ => Sender.Tell(trackedClients.Values.ToArray()));
        }

        public sealed record GetTrackedClients;
    }

    sealed class TrackerConnection : ReceiveActor
    {
        public TrackerConnection(ILogger logger, ClientTracker myself, IActorRef connection) {
            Receive<Tcp.Received>(ping => {
                var elapsed = myself.Ping(DateTime.UtcNow);
                logger.LogDebug("Endpoint {EndPoint} is pinging! Elasped in {Seconds} seconds", myself.EndPoint, elapsed.TotalSeconds);
                connection.Tell(Tcp.Write.Create(ping.Data));
            });
        }
    }
}