using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using Scadex.RemoteDesk.Contracts.Hub;
using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Services.Monitors;
using Scadex.RemoteDesk.Windows.Services.Network;

namespace Scadex.RemoteDesk.Windows.Services.Connection;

public interface ICentralConnection
{
    ConnectionStatus Status { get; }

    /// <summary> Durum değişince — iş parçacığı havuzundan gelir, UI Dispatcher'a taşımalıdır. </summary>
    event Action<ConnectionStatus>? StatusChanged;

    /// <summary> Beklemeyi keser ve hemen yeniden bağlanır (bağlıysa bağlantıyı kapatıp <c>Hello</c>'yu yeniler). </summary>
    void Reconnect();
}

/// <summary>
/// Merkezle tek kalıcı bağlantı — <c>PcHub</c>. Döngü: bağlan → <c>Hello</c> → kabul edildiyse kopana kadar bekle →
/// geri çekilerek yeniden dene (1 → 2 → 5 → 10 → 30 sn, sonsuz). Tanımsız / çakışan cihazda 60 sn'de bir dener.
/// <para/>
/// SignalR'ın kendi otomatik yeniden bağlanması KULLANILMAZ: yeni bağlantı her seferinde <c>Hello</c> ile yeniden eşlenmeli.
/// Ayrı heartbeat yoktur; canlılık SignalR keep-alive'ıdır (15 sn / 30 sn). Boştayken zamanlayıcı ve yoklama yoktur.
/// </summary>
public sealed class CentralConnectionService : BackgroundService, ICentralConnection
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];
    private static readonly TimeSpan RejectedRetry = TimeSpan.FromSeconds(60);

    private readonly RemoteDeskClientOptions _options;
    private readonly INetworkAdapterService _adapters;
    private readonly IMonitorService _monitors;
    private readonly ILogger<CentralConnectionService> _logger;

    private readonly Lock _gate = new();
    private CancellationTokenSource _wake = new();
    private HubConnection? _accepted;

    public CentralConnectionService(IOptions<RemoteDeskClientOptions> options, INetworkAdapterService adapters, IMonitorService monitors,
        ILogger<CentralConnectionService> logger)
    {
        _options = options.Value;
        _adapters = adapters;
        _monitors = monitors;
        _logger = logger;
        Status = new ConnectionStatus(ConnectionState.Connecting, CentralUrl, "Başlatılıyor…");
    }

    public ConnectionStatus Status { get; private set; }
    public event Action<ConnectionStatus>? StatusChanged;

    private string CentralUrl => _options.CentralApiUrl.Trim().TrimEnd('/');

    public void Reconnect()
    {
        lock (_gate) _wake.Cancel();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(CentralUrl))
        {
            Publish(new ConnectionStatus(ConnectionState.NotConfigured, "", "appsettings.json > RemoteDesk:CentralApiUrl boş."));
            return;
        }

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        try
        {
            int failures = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                CancellationToken wake;
                lock (_gate)
                {
                    if (_wake.IsCancellationRequested) { _wake.Dispose(); _wake = new CancellationTokenSource(); }
                    wake = _wake.Token;
                }

                TimeSpan wait;
                switch (await RunOnceAsync(failures, wake, stoppingToken))
                {
                    case RunOutcome.WasConnected:   // kabul edilmiş bağlantı koptu: geri çekilme baştan
                        failures = 0;
                        wait = Backoff[0];
                        break;
                    case RunOutcome.Rejected:       // tanımsız / çakışan / izinsiz ağ: teknisyen düzeltene kadar seyrek dene
                        failures = 0;
                        wait = RejectedRetry;
                        break;
                    default:
                        wait = Backoff[Math.Min(failures, Backoff.Length - 1)];
                        failures++;
                        break;
                }

                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, wake);
                    await Task.Delay(wait, linked.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // "Yeniden bağlan" — beklemeden devam.
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }
    }

    private enum RunOutcome { Failed, Rejected, WasConnected }

    private async Task<RunOutcome> RunOnceAsync(int failures, CancellationToken wake, CancellationToken stoppingToken)
    {
        await using var hub = new HubConnectionBuilder()
            .WithUrl(CentralUrl + PcHubContract.Path)
            .AddJsonProtocol(o =>
            {
                // Scadex'in JSON'u: camelCase, enum sayı (varsayılan), büyük/küçük harf duyarsız okuma.
                o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
            })
            .Build();

        var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.Closed += ex => { closed.TrySetResult(ex); return Task.CompletedTask; };

        try
        {
            Publish(new ConnectionStatus(ConnectionState.Connecting, CentralUrl, failures == 0 ? "Bağlanıyor…" : $"Bağlanıyor… ({failures + 1}. deneme)"));
            await hub.StartAsync(stoppingToken);

            var adapters = _adapters.GetPhysicalAdapters();
            var request = new HelloRequest(
                [.. adapters.Select(a => a.Mac)],
                ClientVersion,
                Environment.OSVersion.VersionString,
                Environment.MachineName,
                Environment.UserName,
                ReadMonitors());

            var response = await hub.InvokeAsync<HelloResponse>(PcHubContract.Hello, request, stoppingToken);
            var matched = response.MatchedMacAddresses.Select(m => MacAddress.Normalize(m) is { } n ? MacAddress.Format(n) : m).ToList();

            if (response.Status != HelloStatus.Accepted)
            {
                _logger.LogWarning("Merkez bağlantıyı kabul etmedi: {Status}", response.Status);
                Publish(Rejected(response.Status, matched));
                await hub.StopAsync(CancellationToken.None);
                return response.Status == HelloStatus.AlreadyConnected ? RunOutcome.Failed : RunOutcome.Rejected;
            }

            _logger.LogInformation("Merkeze bağlandı: {Cabinet} / {Device}", response.CabinetName, response.DeviceName);
            Publish(new ConnectionStatus(ConnectionState.Connected, CentralUrl, $"Bağlandı: {DateTime.Now:HH:mm:ss}",
                response.DeviceId, response.DeviceName, response.CabinetName, matched));
            lock (_gate) _accepted = hub;

            // Kopana ya da "Yeniden bağlan"a kadar bekle.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, wake);
            Exception? reason;
            try
            {
                reason = await closed.Task.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                reason = null;
            }

            lock (_gate) _accepted = null;
            _logger.LogInformation("Merkez bağlantısı kapandı: {Reason}", reason?.Message ?? "yeniden bağlanılıyor");
            Publish(new ConnectionStatus(ConnectionState.Disconnected, CentralUrl,
                reason is null ? "Yeniden bağlanılıyor…" : $"Bağlantı koptu: {Explain(reason)}"));
            return RunOutcome.WasConnected;
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            lock (_gate) _accepted = null;
            _logger.LogWarning(ex, "Merkeze bağlanılamadı");
            TimeSpan next = Backoff[Math.Min(failures, Backoff.Length - 1)];
            Publish(new ConnectionStatus(ConnectionState.Disconnected, CentralUrl, $"{Explain(ex)} — {next.TotalSeconds:0} sn sonra yeniden denenecek."));
            return RunOutcome.Failed;
        }
    }

    private ConnectionStatus Rejected(HelloStatus status, IReadOnlyList<string> matched) => status switch
    {
        HelloStatus.UnknownDevice => new(ConnectionState.UnknownDevice, CentralUrl,
            "Aşağıdaki MAC adreslerinden birini kabin diyagramında bu PC'nin cihazına (tip: Bilgisayar) girin. 60 sn'de bir yeniden denenir."),
        HelloStatus.Ambiguous => new(ConnectionState.Ambiguous, CentralUrl,
            "Bu PC'nin MAC adresleri Scadex'te birden fazla cihaza yazılmış. Fazla olanı diyagramdan silin. 60 sn'de bir yeniden denenir.", MatchedMacs: matched),
        HelloStatus.AlreadyConnected => new(ConnectionState.AlreadyConnected, CentralUrl,
            "Bu cihaz başka bir bağlantıda (ilk bağlanan kazanır). Az önce kopan bağlantıysa merkez ≈30 sn içinde bırakır.", MatchedMacs: matched),
        HelloStatus.NetworkNotAllowed => new(ConnectionState.NetworkNotAllowed, CentralUrl,
            "Merkez bu ağdan bağlantı kabul etmiyor (Modules:RemoteDesk:AllowedNetworks). 60 sn'de bir yeniden denenir."),
        _ => new(ConnectionState.Disconnected, CentralUrl, $"Beklenmeyen yanıt: {status}")
    };

    /// <summary> Sahada okunacak kısa neden; ham istisna metni yalnızca loga gider. </summary>
    private string Explain(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "Merkezde RemoteDesk modülü kapalı (hub bulunamadı)",
        HttpRequestException { StatusCode: { } code } => $"Merkez {(int)code} döndü",
        HttpRequestException => $"Merkeze ulaşılamıyor ({CentralUrl})",
        TimeoutException => "Merkez yanıt vermedi (zaman aşımı)",
        _ => ex.Message
    };

    private MonitorInfo[] ReadMonitors()
    {
        try { return [.. _monitors.GetMonitors()]; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Monitörler okunamadı");
            return [];
        }
    }

    /// <summary> Monitör takılıp çıkarılınca / çözünürlük değişince merkeze bildirilir (yalnızca olayla). </summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        HubConnection? hub;
        lock (_gate) hub = _accepted;
        if (hub is null) return;

        _ = Task.Run(async () =>
        {
            try { await hub.InvokeAsync(PcHubContract.ReportMonitors, ReadMonitors()); }
            catch (Exception ex) { _logger.LogWarning(ex, "Monitör listesi merkeze bildirilemedi"); }
        });
    }

    private void Publish(ConnectionStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    private static string ClientVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";
}
