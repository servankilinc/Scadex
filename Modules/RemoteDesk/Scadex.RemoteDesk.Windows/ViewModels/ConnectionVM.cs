using System.Windows.Input;
using System.Windows.Media;
using Scadex.RemoteDesk.Contracts.Local;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Agent;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary>
/// Ana ekrandaki merkez bağlantısı kartı: Agent'ın merkez bağlantısı, eşlenen kabin/cihaz ve bu PC'nin cihaz kimliği. Merkez bu PC'nin
/// ekranını izlerken belirgin "izleniyor" uyarısı da buradadır (tepsi ikonu da buna bakar) — izleme ve uzaktan kontrol fark edilmeden
/// yapılmaz. Bütün veri Agent'ın özetinden gelir (<see cref="Apply"/>, yalnızca UI iş parçacığında çağrılır).
/// </summary>
public class ConnectionVM : BaseViewModel
{
    private static readonly Brush Green = Frozen("#1E8E3E"), Amber = Frozen("#C78A10"), Red = Frozen("#C5221F"), Gray = Frozen("#6B7580");

    private string _connectionShort = "";
    private IReadOnlyList<int> _watched = [];
    private string? _controller;
    private bool _fileTransfer;

    public ConnectionVM(IAgentLink agent)
    {
        ReconnectCommand = new RelayCommand(_ => agent.Reconnect(), _ => agent.State == AgentLinkState.Connected);
    }

    public ICommand ReconnectCommand { get; }

    private string _machineName = Environment.MachineName;
    public string MachineName { get => _machineName; private set => SetProperty(ref _machineName, value); }

    /// <summary> Yapılandırmadaki cihaz kimliği (Basic Auth kullanıcı adı) — diyagramdaki PC cihazının Id'siyle aynı olmalı. Sır gösterilmez. </summary>
    private string _deviceIdText = "";
    public string DeviceIdText { get => _deviceIdText; private set => SetProperty(ref _deviceIdText, value); }

    private string _agentText = "";
    public string AgentText { get => _agentText; private set => SetProperty(ref _agentText, value); }

    private string _title = "";
    public string Title { get => _title; private set => SetProperty(ref _title, value); }

    private string _detail = "";
    public string Detail { get => _detail; private set => SetProperty(ref _detail, value); }

    private Brush _stateBrush = Gray;
    public Brush StateBrush { get => _stateBrush; private set => SetProperty(ref _stateBrush, value); }

    private string _centralUrl = "";
    public string CentralUrl { get => _centralUrl; private set => SetProperty(ref _centralUrl, value); }

    /// <summary> System Tray ipucu için kısa özet. </summary>
    public string ShortText => _controller is not null ? "KONTROL EDİLİYOR — " + _connectionShort
        : _fileTransfer ? "DOSYA AKTARILIYOR — " + _connectionShort
        : IsWatching ? "İZLENİYOR — " + _connectionShort : _connectionShort;

    /// <summary> Merkez ekranı izliyor, kontrol ediyor ya da dosya aktarıyor — kırmızı şerit ve tepsi ikonu buna bakar. </summary>
    public bool IsWatching => _watched.Count > 0 || _controller is not null || _fileTransfer;
    public string WatchingText => !IsWatching ? ""
        : (_watched.Count > 0 ? "Bu PC'nin ekranı merkezden izleniyor — " + string.Join(", ", _watched.Select(i => $"Monitör {i}")) : "Bu PC merkezden izleniyor")
          + (_controller is not null ? $" · {_controller} PC'yi uzaktan kontrol ediyor (fare ve klavye)" : "")
          + (_fileTransfer ? " · dosya aktarılıyor" : "");

    public void Apply(AgentLinkState link, AgentSnapshot? snapshot)
    {
        ((RelayCommand)ReconnectCommand).RaiseCanExecuteChanged();

        if (link != AgentLinkState.Connected || snapshot is null)
        {
            (Title, Detail, _connectionShort) = link switch
            {
                AgentLinkState.Untrusted => ("Agent doğrulanamadı",
                    "Pipe'ın sunucusu kurulumdaki Scadex RemoteDesk servisi değil; bağlanılmadı. Yöneticinize bildirin (arayüz günlüğü).", "Agent doğrulanamadı"),
                AgentLinkState.VersionMismatch => ("Arayüz ile servis sürümü uyuşmuyor",
                    "Arayüz ve servis aynı sürümün yayınından kurulmalı. Service ve Tray klasörlerini birlikte güncelleyin.", "Sürüm uyuşmuyor"),
                _ => ("Servis bağlantısı yok",
                    "Bu oturumda Scadex RemoteDesk Agent'ı çalışmıyor. Servis (ScadexRemoteDesk) kapalı ya da Agent yeniden başlıyor olabilir.", "Servis yok"),
            };
            StateBrush = link == AgentLinkState.Connecting ? Amber : Red;
            // Bağlantı yokken izleme durumu bilinmez: eski "izleniyor" bilgisi gösterilmez.
            SetActivity([], null, false);
            return;
        }

        var c = snapshot.Connection;
        MachineName = snapshot.MachineName;
        CentralUrl = c.CentralUrl.Length > 0 ? c.CentralUrl : "(tanımlı değil)";
        DeviceIdText = snapshot.DeviceId.Length > 0 ? snapshot.DeviceId : "(tanımlı değil — servis klasöründeki appsettings.json > RemoteDesk:DeviceId)";
        AgentText = $"Agent {snapshot.AgentVersion} · oturum {snapshot.SessionUser}";
        Detail = c.Detail;

        (Title, StateBrush, _connectionShort) = c.State switch
        {
            AgentConnectionState.Connected => ($"Bağlı — {c.CabinetName} / {c.DeviceName}", Green, $"Bağlı: {c.DeviceName}"),
            AgentConnectionState.Connecting => ("Bağlanıyor…", Amber, "Bağlanıyor"),
            AgentConnectionState.Unauthorized => ("Kimlik bilgisi reddedildi", Red, "Kimlik reddedildi"),
            AgentConnectionState.AlreadyConnected => ("Bu cihaz başka bir bağlantıda", Amber, "Başka bağlantıda"),
            AgentConnectionState.NetworkNotAllowed => ("Bu ağdan bağlantı kabul edilmiyor", Red, "Ağ izinli değil"),
            AgentConnectionState.NotConfigured => ("Yapılandırma eksik", Red, "Yapılandırılmamış"),
            _ => ("Bağlantı yok", Red, "Bağlantı yok"),
        };

        var watched = snapshot.Monitors
            .Where(m => m.Stream.SessionId is not null && m.Stream.State is not (AgentStreamState.Idle or AgentStreamState.Failed))
            .Select(m => m.Monitor.Index)
            .Order()
            .ToList();
        SetActivity(watched, snapshot.Controller, snapshot.FileTransferActive);
    }

    private void SetActivity(IReadOnlyList<int> watched, string? controller, bool fileTransfer)
    {
        _watched = watched;
        _controller = controller;
        _fileTransfer = fileTransfer;
        OnPropertyChanged(nameof(IsWatching));
        OnPropertyChanged(nameof(WatchingText));
        OnPropertyChanged(nameof(ShortText));
    }

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
