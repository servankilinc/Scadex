using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Diagram.Queries;

public class DiagramAutomationDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public int? DeviceStatusId { get; set; }
    public string? DeviceStatusName { get; set; }
    public DateTime? LastSeen { get; set; }
    public bool IsActive { get; set; }
    public bool ScadaIsEnabled { get; set; }
    public DateTime? ScadaLastIngestAt { get; set; }
}
