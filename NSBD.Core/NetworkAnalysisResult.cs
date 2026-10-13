namespace NSBD.Core;

public sealed record NetworkAnalysisResult(
    int TotalConnections,
    IReadOnlyList<string> ActiveIps,
    IReadOnlyList<string> ExternalIps,
    IReadOnlyDictionary<int, int> TopPorts,
    IReadOnlyDictionary<string, int> ConnectionsByIp,
    IReadOnlyList<SecurityAlert> Alerts);
