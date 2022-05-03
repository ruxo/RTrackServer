using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Akka.Actor;
using Akka.IO;
using LanguageExt.UnitsOfMeasure;
using Microsoft.Extensions.Logging;
using RTrack.Common;
using RTrackServer.Domain;

namespace RTrackServer.Services;

sealed class Tracker : ReceiveActor
{
    public sealed record GetTrackedClients;

    readonly Dictionary<IP4EndPoint, Client> trackedClients = new ();

    static readonly TimeSpan ClientTimeout = 10.Seconds();
    static readonly TimeSpan AliveCheckInterval = ClientTimeout.Add(5.Seconds());

    public Tracker(ILogger logger, int port) {
        logger.LogDebug("Tracker is being started!");
        Context.System.Tcp().Tell(new Tcp.Bind(Self, new IPEndPoint(IPAddress.Any, port)));
        Context.System.Scheduler.ScheduleTellRepeatedly(AliveCheckInterval, AliveCheckInterval, Self, new CheckAlive(), ActorRefs.NoSender);

        Receive<Tcp.Bound>(bound => logger.LogInformation("Listening on {$Address}", bound.LocalAddress));
        Receive<Tcp.Connected>(client => {
            var endpoint = IP4EndPoint.From((IPEndPoint)client.RemoteAddress);
            logger.LogInformation("New connection from {EndPoint}", endpoint);

            var tracker = new ClientTracker(endpoint);
            var connection = Context.ActorOf(Props.Create(() => new TrackerConnection(logger, tracker, Sender)));
            trackedClients.Add(endpoint, new (tracker, connection));
            Sender.Tell(new Tcp.Register(connection));
        });
        Receive<CheckAlive>(_ => {
            var now = DateTime.UtcNow;
            var timeoutClients = trackedClients.Values.Where(c => c.Tracker.SinceLastUpdate(now) > ClientTimeout).ToArray();
            if (timeoutClients.Length == 0) return;
            logger.LogInformation("{ClientNum} clients are timed-out", timeoutClients.Length);
            timeoutClients.Iter(c => {
                trackedClients.Remove(c.Tracker.EndPoint);
                c.Connection.Tell(TrackerConnection.Close.Instance);
            });
        });

        // ReSharper disable once RedundantCast
        Receive<GetTrackedClients>(_ => Sender.Tell((ClientTracker[]) trackedClients.Values.Select(i => i.Tracker).ToArray()));
    }

    sealed record CheckAlive;

    sealed record Client(ClientTracker Tracker, IActorRef Connection);
}