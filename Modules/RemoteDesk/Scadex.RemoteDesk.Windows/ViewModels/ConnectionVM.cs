using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Scadex.RemoteDesk.Windows.Helpers;
using Scadex.RemoteDesk.Windows.Services.Connection;
using Scadex.RemoteDesk.Windows.Services.Network;

namespace Scadex.RemoteDesk.Windows.ViewModels;

/// <summary> Ana ekrandaki merkez bağlantısı kartı: durum, eşlenen kabin/cihaz ve bu PC'nin gönderdiği MAC'ler. </summary>
public class ConnectionVM : BaseViewModel
{
    private static readonly Brush Green = Frozen("#1E8E3E"), Amber = Frozen("#C78A10"), Red = Frozen("#C5221F"), Gray = Frozen("#6B7580");

    private readonly ICentralConnection _connection;
    private readonly INetworkAdapterService _adapters;

    public ConnectionVM(ICentralConnection connection, INetworkAdapterService adapters)
    {
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
    public string ShortText { get; private set; } = "";

    private void Apply(ConnectionStatus status)
    {
        CentralUrl = status.CentralUrl.Length > 0 ? status.CentralUrl : "(tanımlı değil)";
        Detail = status.Detail;

        (Title, StateBrush, ShortText) = status.State switch
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
