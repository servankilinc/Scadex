using Scadex.Core.Model;

namespace Scadex.RemoteDesk.Model.Dtos.Pc.Queries;

/// <summary> PC listesinin satırı. Ad, kabin ve kimlik durumu çekirdekteki <c>Device</c> / <c>PcDevice</c>'tan okunur; bağlantı alanları bellekteki bağlantı kaydından gelir (Faz 3'e kadar hep "bağlı değil"). </summary>
public class PcListItemDto : IDto
{
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = null!;
    public Guid CabinetId { get; set; }
    public string CabinetName { get; set; } = null!;

    /// <summary> <c>false</c> = diyagramda Basic Auth sırrı üretilmemiş (ya da iptal edilmiş); istemci bağlanamaz (ekranda uyarı). </summary>
    public bool HasCredential { get; set; }

    public bool IsConnected { get; set; }

    /// <summary> Bağlı istemcinin sürümü; bağlı değilken <c>null</c>. </summary>
    public string? ClientVersion { get; set; }

    public int MonitorCount { get; set; }

    /// <summary> Bu PC'nin herhangi bir monitörünü şu an izleyen kiralama sayısı. </summary>
    public int ViewerCount { get; set; }
}
