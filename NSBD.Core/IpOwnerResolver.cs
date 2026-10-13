using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace NSBD.Core;

public static class IpOwnerResolver
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();
    private static readonly ConcurrentDictionary<string, byte> Pending = new();

    public static string Resolve(string address)
    {
        if (!IPAddress.TryParse(address, out var ip))
        {
            return "Unknown";
        }

        if (IPAddress.IsLoopback(ip))
        {
            return "This computer";
        }

        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || IsPrivateIPv4(ip))
        {
            return "Local network";
        }

        if (Cache.TryGetValue(address, out var cached))
        {
            return cached;
        }

        if (Pending.TryAdd(address, 0))
        {
            _ = Task.Run(() => ResolveExternal(address, ip));
        }

        return "Resolving...";
    }

    private static void ResolveExternal(string address, IPAddress ip)
    {
        try
        {
            var host = Dns.GetHostEntry(ip).HostName;
            Cache[address] = string.IsNullOrWhiteSpace(host) ? "External network" : host;
        }
        catch (SocketException)
        {
            Cache[address] = "External IP";
        }
        catch (Exception)
        {
            Cache[address] = "External IP";
        }
        finally
        {
            Pending.TryRemove(address, out _);
        }
    }

    private static bool IsPrivateIPv4(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
               (bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168));
    }
}
