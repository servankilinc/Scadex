using Scadex.Core.Model;

namespace Scadex.RemoteDesk.Model.Dtos.Report.Queries;

/// <summary>
/// Rapor filtrelerinin seçenekleri: denetim tablolarında GEÇEN PC'ler ve kullanıcılar. PC listesi <c>/pcs</c> ucundan alınmaz —
/// o uç <c>RemotePcView</c> ister ve yalnızca aktif PC'leri döner; silinmiş/pasif PC'nin geçmişi de filtrelenebilemli.
/// </summary>
public class RemoteAccessFilterOptionsDto : IDto
{
    public List<ReportPcOptionDto> Pcs { get; set; } = [];
    public List<ReportUserOptionDto> Users { get; set; } = [];
}

public class ReportPcOptionDto : IDto
{
    public Guid DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? CabinetName { get; set; }
}

public class ReportUserOptionDto : IDto
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? UserName { get; set; }
}
