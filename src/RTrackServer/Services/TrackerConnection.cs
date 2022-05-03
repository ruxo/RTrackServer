using System;
using Akka.Actor;
using Akka.IO;
using Microsoft.Extensions.Logging;
using RTrackServer.Domain;

namespace RTrackServer.Services;

sealed class TrackerConnection : ReceiveActor
{
    public sealed record Close { public static readonly Close Instance = new (); }

    public TrackerConnection(ILogger logger, ClientTracker myself, IActorRef connection) {
        Receive<Tcp.Received>(ping => {
            var elapsed = myself.Ping(DateTime.UtcNow);
            logger.LogDebug("Endpoint {EndPoint} is pinging! Elasped in {Seconds} seconds", myself.EndPoint, elapsed.TotalSeconds);
            connection.Tell(Tcp.Write.Create(ping.Data));
        });

        Receive<Close>(_ => {
            connection.Tell(Tcp.Close.Instance);
            Context.Stop(connection);
            Context.Stop(Self);
        });
    }
}