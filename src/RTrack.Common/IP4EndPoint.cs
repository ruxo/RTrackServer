using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace RTrack.Common
{
    public sealed record IP4EndPoint
    {
        public static IP4EndPoint From(IPEndPoint endpoint) {
            if (endpoint.AddressFamily != AddressFamily.InterNetwork)
                throw new InvalidOperationException("Only IPv4 is supported");
            return From(endpoint.ToString());
        }

        public static IP4EndPoint From(string hostAndPort) {
            var parts = hostAndPort.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException($"{hostAndPort} is not a valid 'host:port' format for IPv4!");
            var host = IPAddress.TryParse(parts[0], out var ip)
                           ? ip.ToString()
                           : Dns.GetHostAddresses(parts[0]).FirstOrDefault()?.ToString();
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException($"Host '{host ?? parts[0]}' is not a valid domain name!");
            var port = int.Parse(parts[1]);
            return new(host, port);
        }

        IP4EndPoint(string host, int port) {
            (Host, Port) = (host, port);
        }
        public string Host { get; }
        public int Port { get; }
    }
}