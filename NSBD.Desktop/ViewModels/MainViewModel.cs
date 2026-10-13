using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NSBD.Core;

namespace NSBD.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly NetworkAnalyzer analyzer = new();
    private readonly LiveCaptureService liveCapture = new();
    private readonly DispatcherTimer refreshTimer;
    private IReadOnlyList<NetworkEvent> allEvents = [];
    private IReadOnlyList<NetworkEvent>? importedEvents;
    private bool isShuttingDown;

    public ObservableCollection<NetworkEvent> Events { get; } = [];

    public ObservableCollection<SecurityAlert> Alerts { get; } = [];

    public ObservableCollection<string> StatusOptions { get; } = ["All statuses", "success", "failed", "observed"];

    public ObservableCollection<string> ProtocolOptions { get; } = ["All protocols", "TCP", "UDP"];

    public ObservableCollection<string> NetworkAdapters { get; } = ["Automatic"];

    [ObservableProperty]
    private int totalConnections;

    [ObservableProperty]
    private int activeIpCount;

    [ObservableProperty]
    private int externalIpCount;

    [ObservableProperty]
    private string captureStatus = "Starting";

    [ObservableProperty]
    private int capturedPacketCount;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string portFilter = string.Empty;

    [ObservableProperty]
    private string statusFilter = "All statuses";

    [ObservableProperty]
    private string protocolFilter = "All protocols";

    [ObservableProperty]
    private string selectedNetworkAdapter = "Automatic";

    [ObservableProperty]
    private string fileStatus = "Live data and CSV";

    [ObservableProperty]
    private bool isCapturePaused;

    [ObservableProperty]
    private int alertCount;

    public string CaptureActionText => IsCapturePaused ? "Resume capture" : "Pause capture";

    public MainViewModel() : this(new AppSettings())
    {
    }

    public MainViewModel(AppSettings settings)
    {
        foreach (var adapter in liveCapture.GetAvailableDeviceDescriptions())
        {
            NetworkAdapters.Add(adapter);
        }

        _ = liveCapture.StartAsync(CancellationToken.None);
        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(settings.RefreshIntervalSeconds) };
        refreshTimer.Tick += (_, _) => Refresh();
        refreshTimer.Start();

        Refresh();
        DispatcherTimer.RunOnce(Refresh, TimeSpan.FromSeconds(1));
    }

    public void ApplySettings(AppSettings settings)
    {
        refreshTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(settings.RefreshIntervalSeconds, 1, 60));
    }

    public void Refresh()
    {
        var fileEvents = importedEvents ?? [];
        var allEvents = fileEvents
            .Concat(liveCapture.GetEvents())
            .OrderByDescending(item => item.Timestamp)
            .ToList();
        this.allEvents = allEvents;
        var result = analyzer.Analyze(allEvents);

        ApplyFilters();

        Alerts.Clear();
        foreach (var alert in result.Alerts)
        {
            Alerts.Add(alert);
        }

        AlertCount = result.Alerts.Count;

        TotalConnections = result.TotalConnections;
        ActiveIpCount = result.ActiveIps.Count;
        ExternalIpCount = result.ExternalIps.Count;
        CaptureStatus = liveCapture.Status;
        CapturedPacketCount = liveCapture.ReceivedPackets;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnPortFilterChanged(string value) => ApplyFilters();

    partial void OnStatusFilterChanged(string value) => ApplyFilters();

    partial void OnProtocolFilterChanged(string value) => ApplyFilters();

    partial void OnIsCapturePausedChanged(bool value) => OnPropertyChanged(nameof(CaptureActionText));

    partial void OnSelectedNetworkAdapterChanged(string value)
    {
        if (!string.Equals(value, "Automatic", StringComparison.OrdinalIgnoreCase))
        {
            liveCapture.SelectDevice(value);
            Refresh();
        }
    }

    private void ApplyFilters()
    {
        var search = SearchText.Trim();
        var port = PortFilter.Trim();
        var status = StatusFilter == "All statuses" ? string.Empty : StatusFilter;
        var protocol = ProtocolFilter == "All protocols" ? string.Empty : ProtocolFilter;

        var filteredEvents = allEvents.Where(networkEvent =>
            (string.IsNullOrWhiteSpace(search) ||
             networkEvent.SourceIp.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             networkEvent.DestinationIp.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             networkEvent.Port.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
             networkEvent.Protocol.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             networkEvent.Status.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             networkEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss").Contains(search, StringComparison.OrdinalIgnoreCase) ||
             networkEvent.Timestamp.ToString("dd.MM.yyyy HH:mm:ss").Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(port) || networkEvent.Port.ToString() == port) &&
            (string.IsNullOrWhiteSpace(status) || networkEvent.Status.Equals(status, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(protocol) || networkEvent.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase)));

        Events.Clear();
        foreach (var networkEvent in filteredEvents)
        {
            Events.Add(networkEvent);
        }
    }

    public async Task SaveSnapshotAsync(Stream output)
    {
        await JsonSerializer.SerializeAsync(output, allEvents, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        FileStatus = $"Saved {allEvents.Count} events";
    }

    public async Task LoadSnapshotAsync(Stream input)
    {
        var loadedEvents = await JsonSerializer.DeserializeAsync<List<NetworkEvent>>(input) ?? [];
        importedEvents = loadedEvents;
        FileStatus = $"Loaded {loadedEvents.Count} events";
        Refresh();
    }

    public void SetFileStatus(string message) => FileStatus = message;

    public void ToggleCapture()
    {
        if (IsCapturePaused)
        {
            liveCapture.ResumeCapture();
            IsCapturePaused = false;
        }
        else
        {
            liveCapture.PauseCapture();
            IsCapturePaused = true;
        }

        Refresh();
    }

    public void ClearLiveEvents()
    {
        liveCapture.ClearEvents();
        importedEvents = null;
        FileStatus = "Live events cleared";
        Refresh();
    }

    public async Task ShutdownAsync()
    {
        if (isShuttingDown)
        {
            return;
        }

        isShuttingDown = true;
        refreshTimer.Stop();
        await liveCapture.StopAsync(CancellationToken.None);
    }
}
