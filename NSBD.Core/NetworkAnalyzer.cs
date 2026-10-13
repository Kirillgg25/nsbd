namespace NSBD.Core;

using System.Net;

public sealed class NetworkAnalyzer
{
    private static readonly HashSet<int> CommonPorts = [22, 53, 80, 123, 443, 3389];

    public NetworkAnalysisResult Analyze(IEnumerable<NetworkEvent> events)
    {
        var items = events.ToList();
        var activeIps = items
            .SelectMany(item => new[] { item.SourceIp, item.DestinationIp })
            .Distinct()
            .Order()
            .ToList();

        var externalIps = activeIps
            .Where(IsExternalIp)
            .ToList();

        var topPorts = items
            .Where(item => item.Port > 0)
            .GroupBy(item => item.Port)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(5)
            .ToDictionary(group => group.Key, group => group.Count());

        var connectionsByIp = items
            .GroupBy(item => item.SourceIp)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

        var alerts = FindAlerts(items, connectionsByIp);

        return new NetworkAnalysisResult(
            items.Count,
            activeIps,
            externalIps,
            topPorts,
            connectionsByIp,
            alerts);
    }

    private static bool IsExternalIp(string value)
    {
        if (!IPAddress.TryParse(value, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address) || address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                10 => false,
                127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] is >= 16 and <= 31 => false,
                192 when bytes[1] == 168 => false,
                _ => true
            };
        }

        return (bytes[0] & 0xFE) != 0xFC && !(bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);
    }

    private static List<SecurityAlert> FindAlerts(
        IReadOnlyList<NetworkEvent> events,
        IReadOnlyDictionary<string, int> connectionsByIp)
    {
        var alerts = new List<SecurityAlert>();

        foreach (var (ip, count) in connectionsByIp)
        {
            if (count >= 5)
            {
                alerts.Add(new SecurityAlert(
                    "Warning",
                    $"Many connections from {ip}",
                    $"{ip} created {count} connections. This can be a sign of scanning or unusual activity."));
            }
        }

        foreach (var group in events.GroupBy(item => item.SourceIp))
        {
            var ports = group
                .Select(item => item.Port)
                .Where(port => port > 0)
                .Distinct()
                .Order()
                .ToList();

            if (ports.Count >= 4)
            {
                alerts.Add(new SecurityAlert(
                    "Warning",
                    $"Possible port scan from {group.Key}",
                    $"{group.Key} contacted {ports.Count} different ports: {string.Join(", ", ports)}. Trying many ports can be a sign of reconnaissance."));
            }

            var failedConnections = group.Count(item =>
                item.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));

            if (failedConnections >= 3)
            {
                alerts.Add(new SecurityAlert(
                    "Warning",
                    $"Repeated failed connections from {group.Key}",
                    $"{group.Key} had {failedConnections} failed connections. This can indicate a brute-force attempt or a misconfigured device."));
            }
        }

        var unusualPorts = events
            .Where(item => item.Port > 0)
            .Where(item => !CommonPorts.Contains(item.Port))
            .Select(item => item.Port)
            .Distinct()
            .Order()
            .ToList();

        if (unusualPorts.Count > 0)
        {
            alerts.Add(new SecurityAlert(
                "Info",
                "Unusual ports detected",
                $"The log contains ports that are not in the common allow-list: {string.Join(", ", unusualPorts)}."));
        }

        return alerts;
    }
}
