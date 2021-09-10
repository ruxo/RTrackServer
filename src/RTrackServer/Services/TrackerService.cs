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

namespace RTrackServer.Services
{
    public interface ITrackerService
    {
        Task<string[]> GetTrackingClients();
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

        public Task<string[]> GetTrackingClients() =>
            tracker.Ask<string[]>(new Tracker.GetTrackedClients());
    }

    sealed class Tracker : ReceiveActor
    {
        readonly Dictionary<IPEndPoint, DateTime> trackedClients = new ();

        public Tracker(ILogger logger, int port) {
            logger.LogDebug("Tracker is being started!");
            Context.System.Tcp().Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port)));

            Receive<Tcp.Bound>(bound => logger.LogInformation("Listening on {$Address}", bound.LocalAddress));
            Receive<Tcp.Connected>(client => {
                var endpoint = (IPEndPoint)client.RemoteAddress;
                logger.LogInformation("New connection from {EndPoint}", endpoint);

                trackedClients[endpoint] = DateTime.UtcNow;
                var connection = Context.ActorOf(Props.Create(() => new TrackerConnection(endpoint, Self, Sender)));
                Sender.Tell(new Tcp.Register(connection));
            });

            Receive<Ping>(ping => {
                logger.LogDebug("Endpoint {EndPoint} is pinging!", ping.EndPoint);
                trackedClients[ping.EndPoint] = DateTime.UtcNow;
            });
            Receive<GetTrackedClients>(_ => Sender.Tell(trackedClients.Keys.Select(ep => ep.ToString()).ToArray()));
        }

        public sealed record Ping(IPEndPoint EndPoint);
        public sealed record GetTrackedClients;
    }

    sealed class TrackerConnection : ReceiveActor
    {
        public TrackerConnection(IPEndPoint myself, IActorRef parent, IActorRef connection) {
            Receive<Tcp.Received>(ping => {
                parent.Tell(new Tracker.Ping(myself));
                connection.Tell(Tcp.Write.Create(ping.Data));
            });
        }
    }
}