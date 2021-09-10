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
using RTrack.Common;

namespace RTrackClient
{
    class Program
    {
        static void Main(string[] args) {
            CreateHostBuilder(args).Build().Run();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) => services.AddHostedService<ActorEngine>());
    }

    sealed class ActorEngine : IHostedService
    {
        ActorSystem system = null!;
        readonly IServiceProvider serviceProvider;
        public ActorEngine(IServiceProvider serviceProvider) {
            this.serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            var appConfig = serviceProvider.GetRequiredService<IConfiguration>();
            var serverConfig = appConfig["server"] ?? "localhost:9999";
            var config = ConfigurationFactory.ParseString(File.ReadAllText("app.hocon.conf")).BootstrapFromDocker();
            var bootstrap = BootstrapSetup.Create()
                                          .WithConfig(config)
                                          .WithActorRefProvider(ProviderSelection.Local.Instance)
                                          .And(DependencyResolverSetup.Create(serviceProvider));
            system = ActorSystem.Create("RTrackPinger", bootstrap);

            system.ActorOf(Props.Create(() => new RTrackClient(serviceProvider.GetRequiredService<ILogger<RTrackClient>>(), IP4EndPoint.From(serverConfig))));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) =>
            CoordinatedShutdown.Get(system).Run(CoordinatedShutdown.ClrExitReason.Instance);
    }
}