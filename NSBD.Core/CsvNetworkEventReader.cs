using System.Globalization;
using System.Text;

namespace NSBD.Core;

public sealed class CsvNetworkEventReader
{
    public IReadOnlyList<NetworkEvent> Read(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        if (lines.Length == 0)
        {
            return [];
        }

        var headers = ParseCsvLine(lines[0]);
        var timestampIndex = FindRequiredHeader(headers, "Timestamp", "frame.time", "frame.time_epoch", "Time");
        var sourceIndex = FindRequiredHeader(headers, "SourceIp", "ip.src", "Source");
        var destinationIndex = FindRequiredHeader(headers, "DestinationIp", "ip.dst", "Destination");
        var protocolIndex = FindHeader(headers, "Protocol", "_ws.col.Protocol");
        var statusIndex = FindHeader(headers, "Status");
        var portIndexes = FindHeaders(headers, "Port", "tcp.dstport", "udp.dstport", "tcp.srcport", "udp.srcport");

        if (portIndexes.Count == 0)
        {
            throw new FormatException("CSV must contain a Port, tcp.dstport or udp.dstport column.");
        }

        var events = new List<NetworkEvent>();

        for (var index = 1; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var line = lines[index].Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = ParseCsvLine(line);
            var timestamp = ParseTimestamp(GetValue(columns, timestampIndex), lineNumber);
            var port = ParsePort(columns, portIndexes, lineNumber);

            events.Add(new NetworkEvent(
                timestamp,
                GetValue(columns, sourceIndex),
                GetValue(columns, destinationIndex),
                port,
                protocolIndex >= 0 ? GetValue(columns, protocolIndex) : "Unknown",
                statusIndex >= 0 ? GetValue(columns, statusIndex) : "observed"));
        }

        return events;
    }

    private static DateTime ParseTimestamp(string value, int lineNumber)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var timestamp) ||
            DateTime.TryParse(value, out timestamp))
        {
            return timestamp;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(unixSeconds * 1000)).LocalDateTime;
        }

        throw new FormatException($"CSV line {lineNumber} contains an invalid timestamp.");
    }

    private static int ParsePort(IReadOnlyList<string> columns, IReadOnlyList<int> portIndexes, int lineNumber)
    {
        foreach (var index in portIndexes)
        {
            var value = GetValue(columns, index);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (int.TryParse(value, out var port))
            {
                return port;
            }

            throw new FormatException($"CSV line {lineNumber} contains an invalid port.");
        }

        return 0;
    }

    private static int FindRequiredHeader(IReadOnlyList<string> headers, params string[] names)
    {
        var index = FindHeader(headers, names);

        if (index < 0)
        {
            throw new FormatException($"CSV is missing a required column: {string.Join(" or ", names)}.");
        }

        return index;
    }

    private static int FindHeader(IReadOnlyList<string> headers, params string[] names)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (names.Any(name => headers[index].Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<int> FindHeaders(IReadOnlyList<string> headers, params string[] names) =>
        headers
            .Select((header, index) => new { header, index })
            .Where(item => names.Any(name => item.header.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.index)
            .ToList();

    private static string GetValue(IReadOnlyList<string> columns, int index) =>
        index >= 0 && index < columns.Count ? columns[index].Trim() : string.Empty;

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var insideQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (character == '"')
            {
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (character == ',' && !insideQuotes)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString());
        return values;
    }
}
