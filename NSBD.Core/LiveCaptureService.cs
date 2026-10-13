using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Hosting;
using PacketDotNet;
using SharpPcap;

namespace NSBD.Core;

public sealed class LiveCaptureService : BackgroundService
{
    private const int MaxEvents = 1000;
    private readonly ConcurrentQueue<NetworkEvent> events = new();
    private readonly object captureLock = new();
    private ICaptureDevice? device;
    private string? requestedDeviceDescription;
    private int receivedPackets;
    private int parseErrors;

    public string Status { get; private set; } = "Starting";

    public int EventCount => events.Count;

    public int ReceivedPackets => receivedPackets;

    public int ParseErrors => parseErrors;

    public IReadOnlyList<NetworkEvent> GetEvents() => events.ToArray();

    public void PauseCapture()
    {
        lock (captureLock)
        {
            StopCurrentCapture();
            Status = "Capture paused";
        }
    }

    public void ResumeCapture()
    {
        lock (captureLock)
        {
            if (device is not null)
            {
                return;
            }

            try
            {
                StartSelectedCapture(CaptureDeviceList.Instance);
            }
            catch (Exception exception)
            {
                Status = $"Capture error: {exception.Message}";
            }
        }
    }

    public void ClearEvents()
    {
        while (events.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref receivedPackets, 0);
        Interlocked.Exchange(ref parseErrors, 0);
    }

    public IReadOnlyList<string> GetAvailableDeviceDescriptions() =>
        CaptureDeviceList.Instance
            .Select(item => item.Description)
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToList();

    public void SelectDevice(string description)
    {
        lock (captureLock)
        {
            requestedDeviceDescription = description;

            if (device is null)
            {
                return;
            }

            try
            {
                StopCurrentCapture();
                StartSelectedCapture(CaptureDeviceList.Instance);
            }
            catch (Exception exception)
            {
                Status = $"Capture error: {exception.Message}";
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var devices = CaptureDeviceList.Instance;

            if (devices.Count == 0)
            {
                Status = "No network adapter found";
                await WaitForStop(stoppingToken);
                return;
            }

            lock (captureLock)
            {
                StartSelectedCapture(devices);
            }

            await WaitForStop(stoppingToken);
        }
        catch (Exception exception)
        {
            Status = $"Capture error: {exception.Message}";
            await WaitForStop(stoppingToken);
        }
        finally
        {
            lock (captureLock)
            {
                StopCurrentCapture();
            }
        }
    }

    private void StartSelectedCapture(CaptureDeviceList devices)
    {
        device = requestedDeviceDescription is null
            ? devices.OrderByDescending(GetDevicePriority).FirstOrDefault(IsPhysicalNetworkDevice) ?? devices[0]
            : devices.FirstOrDefault(item => item.Description.Equals(
                requestedDeviceDescription,
                StringComparison.OrdinalIgnoreCase));

        if (device is null)
        {
            Status = "Selected network adapter was not found";
            return;
        }

        device.OnPacketArrival += OnPacketArrival;
        device.Open(DeviceModes.Promiscuous, 1000);
        device.Filter = "tcp or udp";
        device.StartCapture();
        Status = $"Capturing on {device.Description}";
    }

    private void StopCurrentCapture()
    {
        if (device is null)
        {
            return;
        }

        device.OnPacketArrival -= OnPacketArrival;
        device.StopCapture();
        device.Close();
        device = null;
    }

    private static async Task WaitForStop(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool IsPhysicalNetworkDevice(ICaptureDevice captureDevice)
    {
        var description = captureDevice.Description ?? string.Empty;

        return !description.Contains("loopback", StringComparison.OrdinalIgnoreCase) &&
               !description.Contains("wan miniport", StringComparison.OrdinalIgnoreCase) &&
               !description.Contains("npcap", StringComparison.OrdinalIgnoreCase) &&
               !description.Contains("bluetooth", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetDevicePriority(ICaptureDevice captureDevice)
    {
        var description = captureDevice.Description ?? string.Empty;

        if (!IsPhysicalNetworkDevice(captureDevice))
        {
            return -100;
        }

        var activeAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter =>
                adapter.OperationalStatus == OperationalStatus.Up &&
                adapter.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211 &&
                adapter.GetIPProperties().GatewayAddresses.Count > 0)
            .Select(adapter => adapter.Description)
            .ToList();

        if (activeAdapters.Any(adapter =>
                description.Contains(adapter, StringComparison.OrdinalIgnoreCase) ||
                adapter.Contains(description, StringComparison.OrdinalIgnoreCase)))
        {
            return 200;
        }

        if (description.Contains("wi-fi", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("wireless", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("ethernet", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("realtek", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("intel", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("qualcomm", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("mediatek", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        return 0;
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        Interlocked.Increment(ref receivedPackets);

        try
        {
            var capture = e.GetPacket();
            var parsedPacket = Packet.ParsePacket(capture.LinkLayerType, capture.Data);
            var ipPacket = parsedPacket.Extract<IPPacket>();

            if (ipPacket is null)
            {
                return;
            }

            var tcpPacket = ipPacket.Extract<TcpPacket>();
            var udpPacket = ipPacket.Extract<UdpPacket>();
            var protocol = tcpPacket is not null ? "TCP" : udpPacket is not null ? "UDP" : "IP";
            var port = tcpPacket?.DestinationPort ?? udpPacket?.DestinationPort ?? 0;

            events.Enqueue(new NetworkEvent(
                ToLocalTime(capture.Timeval.Date),
                ipPacket.SourceAddress.ToString(),
                ipPacket.DestinationAddress.ToString(),
                port,
                protocol,
                "observed"));

            while (events.Count > MaxEvents && events.TryDequeue(out _))
            {
            }
        }
        catch
        {
            Interlocked.Increment(ref parseErrors);
        }
    }

    private static DateTime ToLocalTime(DateTime captureTime)
    {
        var utcTime = DateTime.SpecifyKind(captureTime, DateTimeKind.Utc);
        return utcTime.ToLocalTime();
    }
}
