using NSBD.Core;

namespace NSBD.Web;

public static class SampleData
{
    public static IReadOnlyList<NetworkEvent> NetworkEvents { get; } =
    [
        new(DateTime.Today.AddMinutes(1), "192.168.1.10", "192.168.1.1", 53, "UDP", "success"),
        new(DateTime.Today.AddMinutes(2), "192.168.1.11", "93.184.216.34", 443, "TCP", "success"),
        new(DateTime.Today.AddMinutes(3), "192.168.1.12", "192.168.1.20", 22, "TCP", "failed"),
        new(DateTime.Today.AddMinutes(4), "192.168.1.12", "192.168.1.21", 22, "TCP", "failed"),
        new(DateTime.Today.AddMinutes(5), "192.168.1.12", "192.168.1.22", 22, "TCP", "failed"),
        new(DateTime.Today.AddMinutes(6), "192.168.1.50", "192.168.1.100", 8080, "TCP", "success"),
        new(DateTime.Today.AddMinutes(7), "192.168.1.50", "192.168.1.101", 8081, "TCP", "success"),
        new(DateTime.Today.AddMinutes(8), "192.168.1.50", "192.168.1.102", 8082, "TCP", "success"),
        new(DateTime.Today.AddMinutes(9), "192.168.1.50", "192.168.1.103", 8083, "TCP", "success"),
        new(DateTime.Today.AddMinutes(10), "192.168.1.50", "192.168.1.104", 8084, "TCP", "success")
    ];
}
