using Scadex.Core.Model;
using Scadex.Model.Dtos.Common;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Diagram.Queries;

public class DiagramConnectionDto : IDto
{
    public Guid Id { get; set; }
    public Guid AutomationId { get; set; }
    public Guid SourcePinId { get; set; }
    public Guid TargetPinId { get; set; }
    public Guid SourceDeviceId { get; set; }
    public Guid TargetDeviceId { get; set; }
    public string? Label { get; set; }
    public string Color { get; set; } = null!;
    public LineStyle LineStyle { get; set; }
    public double StrokeWidth { get; set; }
    public EdgeRouting Routing { get; set; }
    /// <summary>Ara kirilma noktalari: kaynak -> hedef sirali, IKI UC NOKTA HARIC. Bos olabilir.</summary>
    public ICollection<PointDto> Waypoints { get; set; } = [];
    public int ZIndex { get; set; }
}
