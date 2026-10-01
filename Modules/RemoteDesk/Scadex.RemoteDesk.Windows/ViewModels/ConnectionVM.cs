using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Connection;
using Scadex.RemoteDesk.Windows.Services.Input;
using Scadex.RemoteDesk.Windows.Services.Network;
using Scadex.RemoteDesk.Windows.Services.Streaming;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary>
/// Ana ekrandaki merkez bağlantısı kartı: durum, eşlenen kabin/cihaz ve bu PC'nin gönderdiği MAC'ler. Merkez bu PC'nin ekranını
/// izlerken belirgin "izleniyor" uyarısı da buradadır (tepsi ikonu da buna bakar) — izleme ve uzaktan kontrol fark edilmeden yapılmaz (§ 9.2).
/// </summary>
public class ConnectionVM : BaseViewModel
{
    private static readonly Brush Green = Frozen("#1E8E3E"), Amber = Frozen("#C78A10"), Red = Frozen("#C5221F"), Gray = Frozen("#6B7580");

    private readonly ICentralConnection _connection;
    private readonly INetworkAdapterService _adapters;

    /// <summary> Merkezin yayın yaptığı monitörler (yalnızca UI iş parçacığında değişir). </summary>
    private readonly SortedSet<int> _watched = [];
    private string _connectionShort = "";

    public ConnectionVM(ICentralConnection connection, INetworkAdapterService adapters, IScreenStreamService streams, IRemoteInputService input)
    {
        streams.StatusChanged += status => Application.Current?.Dispatcher.BeginInvoke(() => ApplyStream(status));
        input.ControllerChanged += user => Application.Current?.Dispatcher.BeginInvoke(() => ApplyController(user));
        _connection = connection;
        _adapters = adapters;

        ReconnectCommand = new RelayCommand(_ => _connection.Reconnect());
        CopyMacsCommand = new RelayCommand(_ => Clipboard.SetText(string.Join(Environment.NewLine, Adapters.Select(a => a.DisplayMac))));

        _connection.StatusChanged += status => Application.Current?.Dispatcher.BeginInvoke(() => Apply(status));
        Apply(_connection.Status);
    }

    public ICommand ReconnectCommand { get; }
    public ICommand CopyMacsCommand { get; }

    public string MachineName { get; } = Environment.MachineName;

    /// <summary> Merkeze gönderilen kartlar — teknisyen bunlardan birini diyagramdaki PC cihazına girer. </summary>
    public ObservableCollection<NetworkAdapter> Adapters { get; } = [];

    private string _title = "";
    public string Title { get => _title; private set => SetProperty(ref _title, value); }

    private string _detail = "";
    public string Detail { get => _detail; private set => SetProperty(ref _detail, value); }

    private Brush _stateBrush = Gray;
    public Brush StateBrush { get => _stateBrush; private set => SetProperty(ref _stateBrush, value); }

    private string _centralUrl = "";
    public string CentralUrl { get => _centralUrl; private set => SetProperty(ref _centralUrl, value); }

    private string _matchedText = "";
    public string MatchedText { get => _matchedText; private set { if (SetProperty(ref _matchedText, value)) OnPropertyChanged(nameof(HasMatched)); } }
    public bool HasMatched => MatchedText.Length > 0;

    /// <summary> System Tray ipucu için kısa özet. </summary>
    public string ShortText => _controller is not null ? "KONTROL EDİLİYOR — " + _connectionShort
        : IsWatching ? "İZLENİYOR — " + _connectionShort : _connectionShort;

    /// <summary> Merkez ekranı izliyor ya da fareyi kontrol ediyor — kırmızı şerit ve tepsi ikonu buna bakar. </summary>
    public bool IsWatching => _watched.Count > 0 || _controller is not null;
    public string WatchingText => !IsWatching ? ""
        : (_watched.Count > 0 ? "Bu PC'nin ekranı merkezden izleniyor — " + string.Join(", ", _watched.Select(i => $"Monitör {i}")) : "Bu PC merkezden izleniyor")
          + (_controller is not null ? $" · {_controller} fareyi uzaktan kontrol ediyor" : "");

    /// <summary> Uzaktan kontrol eden kullanıcı (yalnızca UI iş parçacığında değişir). </summary>
    private string? _controller;

    private void ApplyController(string? user)
    {
        if (_controller == user) return;
        _controller = user;
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
            ConnectionState.UnknownDevice => ("Bu PC Scadex'te tanımlı değil", Red, "Tanımsız cihaz"),
            ConnectionState.Ambiguous => ("MAC adresi birden fazla cihazda", Red, "MAC çakışması"),
            ConnectionState.AlreadyConnected => ("Bu cihaz başka bir bağlantıda", Amber, "Başka bağlantıda"),
            ConnectionState.NetworkNotAllowed => ("Bu ağdan bağlantı kabul edilmiyor", Red, "Ağ izinli değil"),
            ConnectionState.NotConfigured => ("Merkez adresi tanımlı değil", Red, "Yapılandırılmamış"),
            _ => ("Bağlantı yok", Red, "Bağlantı yok"),
        };
        MatchedText = status.MatchedMacs is { Count: > 0 } macs
            ? (status.State == ConnectionState.Connected ? "Eşleşen MAC: " : "İlgili MAC'ler: ") + string.Join(", ", macs)
            : "";
        OnPropertyChanged(nameof(ShortText));

        // MAC listesi yalnızca bağlanırken/durum değişince okunur (zamanlayıcı yok).
        if (status.State is ConnectionState.Connecting or ConnectionState.UnknownDevice || Adapters.Count == 0)
            LoadAdapters();
    }

    private void LoadAdapters()
    {
        Adapters.Clear();
        try
        {
            foreach (var adapter in _adapters.GetPhysicalAdapters())
                Adapters.Add(adapter);
        }
        catch
        {
            // Ağ kartı okunamazsa liste boş kalır; bağlantı servisi aynı hatayı loglar.
        }
    }

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
