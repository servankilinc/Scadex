using Scadex.Core.Model;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Model.Dtos.Report.Queries;

/// <summary> Uzaktan kontrol raporunun satırı (<c>RemoteControlSession</c>): kim, hangi PC'yi, ne zaman, neden bitti. </summary>
public class RemoteControlReportItemDto : IDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? CabinetName { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserName { get; set; }
    public DateTime StartedUtc { get; set; }

    /// <summary> <c>null</c> = hâlâ kontrol ediyor. </summary>
    public DateTime? EndedUtc { get; set; }

    public RemoteControlEndReason? EndReason { get; set; }
}
