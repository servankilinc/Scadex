using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Connection;
using Scadex.RemoteDesk.Windows.Services.Input;
using Scadex.RemoteDesk.Windows.Services.Streaming;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary>
/// Ana ekrandaki merkez bağlantısı kartı: durum, eşlenen kabin/cihaz ve bu PC'nin cihaz kimliği (Basic Auth kullanıcı adı). Merkez bu PC'nin ekranını
/// izlerken belirgin "izleniyor" uyarısı da buradadır (tepsi ikonu da buna bakar) — izleme ve uzaktan kontrol fark edilmeden yapılmaz.
/// </summary>
public class ConnectionVM : BaseViewModel
{
    private static readonly Brush Green = Frozen("#1E8E3E"), Amber = Frozen("#C78A10"), Red = Frozen("#C5221F"), Gray = Frozen("#6B7580");

    private readonly ICentralConnection _connection;

    /// <summary> Merkezin yayın yaptığı monitörler (yalnızca UI iş parçacığında değişir). </summary>
    private readonly SortedSet<int> _watched = [];
    private string _connectionShort = "";

    public ConnectionVM(ICentralConnection connection, IScreenStreamService streams, IRemoteInputService input, Services.FileTransfer.IFileTransferService files)
    {
        files.ActiveChanged += active => Application.Current?.Dispatcher.BeginInvoke(() => ApplyFileTransfer(active));
        streams.StatusChanged += status => Application.Current?.Dispatcher.BeginInvoke(() => ApplyStream(status));
        input.ControllerChanged += user => Application.Current?.Dispatcher.BeginInvoke(() => ApplyController(user));
        _connection = connection;

        ReconnectCommand = new RelayCommand(_ => _connection.Reconnect());
        DeviceIdText = connection.DeviceId.Length > 0 ? connection.DeviceId : "(tanımlı değil — appsettings.json > RemoteDesk:DeviceId)";

        _connection.StatusChanged += status => Application.Current?.Dispatcher.BeginInvoke(() => Apply(status));
        Apply(_connection.Status);
    }

    public ICommand ReconnectCommand { get; }

    public string MachineName { get; } = Environment.MachineName;

    /// <summary> Yapılandırmadaki cihaz kimliği (Basic Auth kullanıcı adı) — diyagramdaki PC cihazının Id'siyle aynı olmalı. Sır gösterilmez. </summary>
    public string DeviceIdText { get; }

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

    /// <summary> Uzaktan kontrol eden kullanıcı (yalnızca UI iş parçacığında değişir). </summary>
    private string? _controller;
    /// <summary> Bir dosya transferi sürüyor mu (yalnızca UI iş parçacığında değişir). </summary>
    private bool _fileTransfer;

    private void ApplyController(string? user)
    {
        if (_controller == user) return;
        _controller = user;
        OnPropertyChanged(nameof(IsWatching));
        OnPropertyChanged(nameof(WatchingText));
        OnPropertyChanged(nameof(ShortText));
    }

    private void ApplyFileTransfer(bool active)
    {
        if (_fileTransfer == active) return;
        _fileTransfer = active;
        OnPropertyChanged(nameof(IsWatching));
        OnPropertyChanged(nameof(WatchingText));
        OnPropertyChanged(nameof(ShortText));
    }

    private void ApplyStream(StreamStatus status)
    {
        bool active = status.SessionId is not null && status.State is not (StreamState.Idle or StreamState.Failed);
        bool changed = active ? _watched.Add(status.MonitorIndex) : _watched.Remove(status.MonitorIndex);
        if (!changed) return;

        OnPropertyChanged(nameof(IsWatching));
        OnPropertyChanged(nameof(WatchingText));
        OnPropertyChanged(nameof(ShortText));
    }

    private void Apply(ConnectionStatus status)
    {
        CentralUrl = status.CentralUrl.Length > 0 ? status.CentralUrl : "(tanımlı değil)";
        Detail = status.Detail;

        (Title, StateBrush, _connectionShort) = status.State switch
        {
            ConnectionState.Connected => ($"Bağlı — {status.CabinetName} / {status.DeviceName}", Green, $"Bağlı: {status.DeviceName}"),
            ConnectionState.Connecting => ("Bağlanıyor…", Amber, "Bağlanıyor"),
            ConnectionState.Unauthorized => ("Kimlik bilgisi reddedildi", Red, "Kimlik reddedildi"),
            ConnectionState.AlreadyConnected => ("Bu cihaz başka bir bağlantıda", Amber, "Başka bağlantıda"),
            ConnectionState.NetworkNotAllowed => ("Bu ağdan bağlantı kabul edilmiyor", Red, "Ağ izinli değil"),
            ConnectionState.NotConfigured => ("Yapılandırma eksik", Red, "Yapılandırılmamış"),
            _ => ("Bağlantı yok", Red, "Bağlantı yok"),
        };
        OnPropertyChanged(nameof(ShortText));
    }

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
