using Scadex.RemoteDesk.Model.Dtos.Control;

namespace Scadex.RemoteDesk.Model.Dtos.Pc.Queries;

/// <summary> PC ekranı: liste satırı + bağlı istemcinin monitörleri ve her monitörün yayın durumu. </summary>
public class PcDetailDto : PcListItemDto
{
    public string? MachineName { get; set; }
    public string? UserName { get; set; }
    public string? OsVersion { get; set; }
    public DateTime? ConnectedUtc { get; set; }

    /// <summary> Bağlı değilken boş. </summary>
    public List<PcMonitorDto> Monitors { get; set; } = [];

    /// <summary> PC'yi şu an uzaktan kontrol eden kullanıcı; yoksa <c>null</c>. </summary>
    public PcControlDto? Control { get; set; }
}

public class PcMonitorDto
{
    /// <summary> İzleme ucu bu numarayı alır (<c>monitors/{index}/view</c>). </summary>
    public int Index { get; set; }
    public string DeviceName { get; set; } = null!;
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
    public string GpuName { get; set; } = null!;

    /// <summary> Yayın varsa: istemcinin son bildirdiği durum (<c>ScreenStreamState</c>, sayı); yoksa <c>null</c>. </summary>
    public int? StreamState { get; set; }

    /// <summary> Yayını yapan kodlayıcı (istemcinin bildirdiği görünen ad). </summary>
    public string? Encoder { get; set; }

    public int ViewerCount { get; set; }
}
