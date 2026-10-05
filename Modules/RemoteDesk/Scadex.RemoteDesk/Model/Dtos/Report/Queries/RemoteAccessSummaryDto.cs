using Scadex.Core.Model;

namespace Scadex.RemoteDesk.Model.Dtos.Report.Queries;

/// <summary>
/// Raporun özet kartları — liste ile AYNI filtreye göre. Süreler saniyedir; açık kayıt "şu ana kadar" sayılır.
/// </summary>
public class RemoteAccessSummaryDto : IDto
{
    public int ViewCount { get; set; }
    public long ViewSeconds { get; set; }
    public int ControlCount { get; set; }
    public long ControlSeconds { get; set; }

    /// <summary> İzleme süresi en uzun PC; kayıt yoksa <c>null</c>. </summary>
    public ReportPcTotalDto? TopPc { get; set; }

    /// <summary> İzleme süresi en uzun kullanıcı; kayıt yoksa <c>null</c>. </summary>
    public ReportUserTotalDto? TopUser { get; set; }
}

public class ReportPcTotalDto : IDto
{
    public Guid DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? CabinetName { get; set; }
    public long Seconds { get; set; }
}

public class ReportUserTotalDto : IDto
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? UserName { get; set; }
    public long Seconds { get; set; }
}
