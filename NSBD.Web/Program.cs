using System.Text;
using NSBD.Core;
using NSBD.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<NetworkAnalyzer>();
builder.Services.AddSingleton<CsvNetworkEventReader>();
builder.Services.AddSingleton<LiveCaptureService>();
builder.Services.AddSingleton<IHostedService>(services =>
    services.GetRequiredService<LiveCaptureService>());

var app = builder.Build();

var dataFilePath = Path.Combine(
    app.Environment.ContentRootPath,
    "data",
    "network-events.csv");

app.MapGet("/", (NetworkAnalyzer analyzer, CsvNetworkEventReader reader, LiveCaptureService liveCapture) =>
{
    var events = reader.Read(dataFilePath).Concat(liveCapture.GetEvents()).ToList();
    var result = analyzer.Analyze(events);
    var html = new StringBuilder();

    html.Append("""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>NSBD Network Monitor</title>
            <style>
                body { margin: 0; font-family: Arial, sans-serif; color: #101827; background: #f4f7fb; }
                header { padding: 28px 36px; background: #0f172a; color: white; }
                main { padding: 28px 36px; display: grid; gap: 18px; }
                .cards { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; }
                .card, table { background: white; border: 1px solid #d9e2ef; border-radius: 8px; }
                .card { padding: 18px; }
                .value { font-size: 32px; font-weight: 700; }
                table { width: 100%; border-collapse: collapse; overflow: hidden; }
                th, td { padding: 12px 14px; border-bottom: 1px solid #e5edf7; text-align: left; }
                th { background: #eef4fb; }
                .filters { display: flex; flex-wrap: wrap; gap: 10px; margin: 12px 0; }
                .filters input, .filters select { padding: 9px 10px; border: 1px solid #cbd8e8; border-radius: 6px; font: inherit; }
                .status-success { color: #16734a; font-weight: 700; }
                .status-failed { color: #b42318; font-weight: 700; }
                .alert { padding: 14px 16px; border-radius: 8px; border: 1px solid #f1c27d; background: #fff8ec; }
                .level { font-weight: 700; color: #9a5800; }
                @media (max-width: 800px) { .cards { grid-template-columns: 1fr; } }
            </style>
        </head>
        <body>
            <header>
                <h1>NSBD Network Monitor</h1>
                <p>Prototype dashboard for analysing network activity.</p>
                <p>Last updated: __LAST_UPDATED__</p>
                <p>Live capture: __CAPTURE_STATUS__ (__CAPTURE_COUNT__ captured events, __PACKET_COUNT__ packets received)</p>
            </header>
            <main>
        """);

    html.Append($"""
        <section class="cards">
            <div class="card"><div>Total connections</div><div class="value">{result.TotalConnections}</div></div>
            <div class="card"><div>Active IP addresses</div><div class="value">{result.ActiveIps.Count}</div></div>
            <div class="card"><div>External IP addresses</div><div class="value">{result.ExternalIps.Count}</div></div>
            <div class="card"><div>Security alerts</div><div class="value">{result.Alerts.Count}</div></div>
        </section>
        """);

    html.Append("<section><h2>External IP addresses</h2><table><tr><th>Address</th><th>Type</th></tr>");
    foreach (var ip in result.ExternalIps)
    {
        html.Append($"<tr><td>{System.Net.WebUtility.HtmlEncode(ip)}</td><td>External destination or source</td></tr>");
    }

    html.Append("</table></section>");

    html.Append("""
        <section>
            <h2>Network events</h2>
            <div class="filters">
                <input id="ipFilter" type="search" placeholder="Search by IP">
                <input id="portFilter" type="number" min="1" max="65535" placeholder="Port">
                <select id="statusFilter">
                    <option value="">All statuses</option>
                    <option value="success">Success</option>
                    <option value="failed">Failed</option>
                </select>
            </div>
            <table id="eventsTable">
                <thead><tr><th>Time</th><th>Source IP</th><th>Destination IP</th><th>Port</th><th>Protocol</th><th>Status</th></tr></thead>
                <tbody>
        """);
    foreach (var networkEvent in events)
    {
        var statusClass = networkEvent.Status.Equals("success", StringComparison.OrdinalIgnoreCase)
            ? "status-success"
            : "status-failed";

        html.Append($"""
            <tr data-source-ip="{System.Net.WebUtility.HtmlEncode(networkEvent.SourceIp)}" data-destination-ip="{System.Net.WebUtility.HtmlEncode(networkEvent.DestinationIp)}" data-port="{networkEvent.Port}" data-status="{System.Net.WebUtility.HtmlEncode(networkEvent.Status.ToLowerInvariant())}">
                <td>{networkEvent.Timestamp:yyyy-MM-dd HH:mm:ss}</td>
                <td>{System.Net.WebUtility.HtmlEncode(networkEvent.SourceIp)}</td>
                <td>{System.Net.WebUtility.HtmlEncode(networkEvent.DestinationIp)}</td>
                <td>{networkEvent.Port}</td>
                <td>{System.Net.WebUtility.HtmlEncode(networkEvent.Protocol)}</td>
                <td class="{statusClass}">{System.Net.WebUtility.HtmlEncode(networkEvent.Status)}</td>
            </tr>
            """);
    }

    html.Append("</tbody></table></section>");

    html.Append("<section><h2>Connections by source IP</h2><table><tr><th>Source IP</th><th>Connections</th></tr>");
    foreach (var item in result.ConnectionsByIp)
    {
        html.Append($"<tr><td>{item.Key}</td><td>{item.Value}</td></tr>");
    }

    html.Append("</table></section>");

    html.Append("<section><h2>Top ports</h2><table><tr><th>Port</th><th>Uses</th></tr>");
    foreach (var item in result.TopPorts)
    {
        html.Append($"<tr><td>{item.Key}</td><td>{item.Value}</td></tr>");
    }

    html.Append("</table></section>");

    html.Append("<section><h2>Alerts</h2>");
    foreach (var alert in result.Alerts)
    {
        html.Append($"""
            <div class="alert">
                <div class="level">{alert.Level}</div>
                <strong>{alert.Title}</strong>
                <p>{alert.Description}</p>
            </div>
            """);
    }

    html.Append("""
        </section>
        <script>
            const ipFilter = document.getElementById('ipFilter');
            const portFilter = document.getElementById('portFilter');
            const statusFilter = document.getElementById('statusFilter');
            const eventRows = document.querySelectorAll('#eventsTable tbody tr');

            function filterEvents() {
                const ip = ipFilter.value.trim().toLowerCase();
                const port = portFilter.value.trim();
                const status = statusFilter.value;

                sessionStorage.setItem('nsbd-ip-filter', ipFilter.value);
                sessionStorage.setItem('nsbd-port-filter', portFilter.value);
                sessionStorage.setItem('nsbd-status-filter', statusFilter.value);

                eventRows.forEach(row => {
                    const matchesIp = !ip || row.dataset.sourceIp.includes(ip) || row.dataset.destinationIp.includes(ip);
                    const matchesPort = !port || row.dataset.port === port;
                    const matchesStatus = !status || row.dataset.status === status;
                    row.hidden = !(matchesIp && matchesPort && matchesStatus);
                });
            }

            ipFilter.addEventListener('input', filterEvents);
            portFilter.addEventListener('input', filterEvents);
            statusFilter.addEventListener('change', filterEvents);

            ipFilter.value = sessionStorage.getItem('nsbd-ip-filter') ?? '';
            portFilter.value = sessionStorage.getItem('nsbd-port-filter') ?? '';
            statusFilter.value = sessionStorage.getItem('nsbd-status-filter') ?? '';
            filterEvents();

            setTimeout(() => window.location.reload(), 5000);
        </script>
        </main></body></html>
        """);
    html.Replace("__LAST_UPDATED__", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    html.Replace("__CAPTURE_STATUS__", System.Net.WebUtility.HtmlEncode(liveCapture.Status));
    html.Replace("__CAPTURE_COUNT__", liveCapture.EventCount.ToString());
    html.Replace("__PACKET_COUNT__", liveCapture.ReceivedPackets.ToString());

    return Results.Content(html.ToString(), "text/html");
});

app.MapGet("/api/analysis", (NetworkAnalyzer analyzer, CsvNetworkEventReader reader, LiveCaptureService liveCapture) =>
    analyzer.Analyze(reader.Read(dataFilePath).Concat(liveCapture.GetEvents())));

app.Run();
