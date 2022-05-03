using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using Akka.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RTrackServer.Domain;

namespace RTrackServer.Services;

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