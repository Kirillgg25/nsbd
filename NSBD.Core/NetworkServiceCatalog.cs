namespace NSBD.Core;

public static class NetworkServiceCatalog
{
    private static readonly Dictionary<int, string> Services = new()
    {
        [22] = "SSH",
        [25] = "SMTP",
        [53] = "DNS",
        [80] = "HTTP",
        [110] = "POP3",
        [123] = "NTP",
        [143] = "IMAP",
        [443] = "HTTPS/TLS",
        [445] = "SMB",
        [3306] = "MySQL",
        [3389] = "RDP",
        [5432] = "PostgreSQL",
        [8080] = "HTTP-alt"
    };

    public static string GetService(string protocol, int port)
    {
        if (Services.TryGetValue(port, out var service))
        {
            return service;
        }

        return $"{protocol}/{port}";
    }
}
