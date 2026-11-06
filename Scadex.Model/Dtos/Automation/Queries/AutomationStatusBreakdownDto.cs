using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Automation.Queries;

/// <summary>
/// Otomasyon durumunun dökümü: otomasyon bu durumda ÇÜNKÜ hangi cihaz ne katkı veriyor (<c>GET /api/Automation/{id}/status-breakdown</c>).
/// Katkılar <c>AutomationStatusService</c>'in durumu hesaplayan kuralının AYNISIYLA sunucuda üretilir; ön yüz kuralı kopyalamaz.
/// Salt okunurdur — durumu yine yalnızca <c>IAutomationStatusService</c>'in yazım yolları değiştirir.
/// </summary>
public class AutomationStatusBreakdownDto : IDto
{
    public Guid AutomationId { get; set; }
    /// <summary> Otomasyonun SAKLI durumu (haritadaki renk). <c>null</c> = bilinmiyor. </summary>
    public int? StatusId { get; set; }
    /// <summary> Bakım elle verilmiş yapışkan karardır; hesaplama bu otomasyona dokunmaz. </summary>
    public bool IsMaintenance { get; set; }
    /// <summary> Aktif kontrol modülü (SCADA kartı) var mı — yoksa otomasyon hiçbir zaman Çevrimiçi olamaz. </summary>
    public bool HasControlModule { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? ScadaLastIngestAt { get; set; }

    /// <summary> Hesaplamanın gördüğü her kayıt: katkı şiddeti azalan, kontrol modülü önce, ada göre. </summary>
    public List<AutomationStatusContributorDto> Items { get; set; } = [];
}

/// <summary> Otomasyon durumuna katkı veren (ya da verebilecek) tek bir cihaz — kameralar dahil, hepsi <c>Device</c>'tır. </summary>
public class AutomationStatusContributorDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    /// <summary> Cihazın şablon adı. </summary>
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
    /// Kaydın otomasyona YANSIYAN durumu (kuralın çıktısı): kontrol modülünde kendi durumu, diğer cihazlarda (kameralar dahil)
    /// <c>Offline → Warning</c>. <c>null</c> = otomasyona katkısı yok (örn. Çevrimiçi bir POS otomasyonu Çevrimiçi yapamaz).
    /// </summary>
    public int? Contribution { get; set; }
}
