using Scadex.Core.Model;

namespace Scadex.RemoteDesk.Model.Dtos.Report.Queries;

/// <summary> İzleme raporunun satırı (<c>ScreenViewLog</c>). Ad alanları çekirdekten çözülür; <c>null</c> = kayıt artık çekirdekte yok (cihaz/kullanıcı silinmiş) — denetim satırı yine de durur. </summary>
public class ScreenViewReportItemDto : IDto
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? CabinetName { get; set; }
    public int MonitorIndex { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserName { get; set; }
    public DateTime StartedUtc { get; set; }

    /// <summary> <c>null</c> = hâlâ izliyor. </summary>
    public DateTime? EndedUtc { get; set; }
}
