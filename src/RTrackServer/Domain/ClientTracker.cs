using System;
using RTrack.Common;

namespace RTrackServer.Domain;

public sealed class ClientTracker
{
    public ClientTracker(IP4EndPoint endPoint) {
        EndPoint = endPoint;
        LastUpdate = DateTime.UtcNow;
    }

    public IP4EndPoint EndPoint { get; }
    public DateTime LastUpdate { get; private set; }

    public TimeSpan Ping(DateTime now) {
        var lastUpdateInterval = SinceLastUpdate(now);
        LastUpdate = now;
        return lastUpdateInterval;
    }

    public TimeSpan SinceLastUpdate(DateTime now) => now - LastUpdate;
}