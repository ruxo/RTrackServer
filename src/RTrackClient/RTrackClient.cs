using System;
using System.Net;
using System.Text;
using Akka.Actor;
using Akka.IO;
using LanguageExt.UnitsOfMeasure;
using Microsoft.Extensions.Logging;
using RTrack.Common;

namespace RTrackClient
{
    public sealed class RTrackClient : ReceiveActor, IWithTimers
    {
        static readonly TimeSpan PongCheckInterval = 10.Seconds();

        readonly ILogger logger;
        readonly IP4EndPoint host;
        readonly Random random = new();
        public RTrackClient(ILogger logger, IP4EndPoint host) {
            this.logger = logger;
            this.host = host;
            Self.Tell(new Reconnect());

            RetryConnection();
        }

        Action Connected(IActorRef sender) {
            Self.Tell(new Ping());
            string? pongCheck = null;
            var consecutiveTimeout = 0;
            return () => {
                Receive<Ping>(_ => {
                    pongCheck = DateTime.UtcNow.Ticks.ToString();
                    logger.LogDebug("Ping with {PongCheck}", pongCheck);
                    sender.Tell(Tcp.Write.Create(ByteString.FromString(pongCheck, Encoding.UTF8)));
                    Timers.StartSingleTimer("checkPong", new CheckPong(pongCheck), PongCheckInterval);
                });
                Receive<Tcp.Received>(received => {
                    var pong = received.Data.ToString(Encoding.UTF8);
                    if (pong != pongCheck)
                        logger.LogError("Something's wrong, pong is out of sync. Ignore: {Pong}, expected {PongCheck}", pong, pongCheck);
                    else {
                        logger.LogDebug("Pong {Pong}!", pongCheck);
                        pongCheck = null;
                        consecutiveTimeout = 0;
                        Timers.Cancel("checkPong");
                        OneShot<Ping>(5, 10);
                    }
                });
                Receive<CheckPong>(pong => {
                    if (pong.Expected == pongCheck) {
                        logger.LogWarning("Ping {Pong} timeout!", pongCheck);
                        if (++consecutiveTimeout == 3) {
                            logger.LogWarning("3 consecutive timeout... Probably server has a problem. Retry connection!");
                            Become(RetryConnection);
                            Self.Tell(new Reconnect());
                        }
                        else
                            Self.Tell(new Ping());
                    }
                    else
                        logger.LogCritical("This should not happen.. expect {Expected}, got {PongCheck}... Ignored", pong.Expected, pongCheck);
                });
                Receive<Tcp.PeerClosed>(_ => {
                    logger.LogWarning("Connection is reset by peer!");
                    Become(RetryConnection);
                    Self.Tell(new Reconnect());
                });
            };
        }

        void RetryConnection() {
            Receive<Reconnect>(_ => ConnectHost(host));
            Receive<Tcp.CommandFailed>(_ => {
                logger.LogError("Connecting to host failed!");
                OneShot<Reconnect>(10, 15);
            });
            Receive<Tcp.Connected>(connected => {
                logger.LogInformation("Connected to {$Remote}", connected.RemoteAddress);

                Sender.Tell(new Tcp.Register(Self));
                Become(Connected(Sender));
            });
        }

        void ConnectHost(IP4EndPoint ep) {
            logger.LogDebug("Connect to host {EndPoint}", ep);
            Context.System.Tcp().Tell(new Tcp.Connect(new DnsEndPoint(ep.Host, ep.Port)));
        }

        void OneShot<T>(int minSec, int maxSec) where T: new() =>
            Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(random.Next(minSec, maxSec)), Self, new T(), ActorRefs.NoSender);

        sealed record Ping;
        sealed record Reconnect;
        sealed record CheckPong(string Expected);
        public ITimerScheduler Timers { get; set; } = null!;
    }
}