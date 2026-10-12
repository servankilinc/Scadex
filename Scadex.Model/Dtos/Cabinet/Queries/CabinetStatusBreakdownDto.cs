using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Cabinet.Queries;

/// <summary>
/// Kabin durumunun dökümü: kabin bu durumda ÇÜNKÜ hangi cihaz/kamera ne katkı veriyor (<c>GET /api/Cabinet/{id}/status-breakdown</c>).
/// Katkılar <c>CabinetStatusService</c>'in durumu hesaplayan kuralının AYNISIYLA sunucuda üretilir; ön yüz kuralı kopyalamaz.
/// Salt okunurdur — durumu yine yalnızca <c>ICabinetStatusService</c>'in yazım yolları değiştirir.
/// </summary>
public class CabinetStatusBreakdownDto : IDto
{
    public Guid CabinetId { get; set; }
    /// <summary> Kabinin SAKLI durumu (haritadaki renk). <c>null</c> = bilinmiyor. </summary>
    public int? StatusId { get; set; }
    /// <summary> Bakım elle verilmiş yapışkan karardır; hesaplama bu kabine dokunmaz. </summary>
    public bool IsMaintenance { get; set; }
    /// <summary> Aktif kontrol modülü (SCADA kartı) var mı — yoksa kabin hiçbir zaman Çevrimiçi olamaz. </summary>
    public bool HasControlModule { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? ScadaLastIngestAt { get; set; }

    /// <summary> Hesaplamanın gördüğü her kayıt: katkı şiddeti azalan, kontrol modülü önce, ada göre. </summary>
    public List<CabinetStatusContributorDto> Items { get; set; } = [];
}

/// <summary> Kabin durumuna katkı veren (ya da verebilecek) tek bir cihaz/kamera. </summary>
public class CabinetStatusContributorDto : IDto
{
    public CabinetStatusContributorKind Kind { get; set; }
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    /// <summary> Cihazın şablon adı; kamerada <c>null</c>. </summary>
    public string? TemplateName { get; set; }
    public bool IsControlModule { get; set; }

    public bool IsMonitoringEnabled { get; set; }
    public string? IpAddress { get; set; }
    public int? MonitoringPort { get; set; }
    public int PingIntervalSec { get; set; }

    /// <summary> Kaydın KENDİ durumu. <c>null</c> = bilinmiyor. </summary>
    public int? StatusId { get; set; }
    public DateTime? LastSeen { get; set; }
    public string? LastConnectionError { get; set; }

    /// <summary>
    /// Kaydın kabine YANSIYAN durumu (kuralın çıktısı): kontrol modülünde kendi durumu, diğer cihaz ve izlenen kamerada
    /// <c>Offline → Warning</c>. <c>null</c> = kabine katkısı yok (örn. Çevrimiçi bir POS kabini Çevrimiçi yapamaz).
    /// </summary>
    public int? Contribution { get; set; }
}
