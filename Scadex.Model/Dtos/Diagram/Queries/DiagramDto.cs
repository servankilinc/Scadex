using Scadex.Core.Model;
using Scadex.Model.Dtos.Diagram.Queries.Items;

namespace Scadex.Model.Dtos.Diagram.Queries;

public class DiagramDto : IDto
{
    public DiagramCabinetDto Cabinet { get; set; } = null!;
    public ICollection<DiagramDeviceDto> Devices { get; set; } = [];
    public ICollection<DiagramConnectionDto> Connections { get; set; } = [];
    public ICollection<DiagramAnnotationItemDto> Annotations { get; set; } = [];
    public DiagramCanvasSettingsDto CanvasSettings { get; set; } = null!;

    /// <summary>Anlik bilgilerin alindigi an; istemcinin güncellik durumunu takip etmek için.</summary>
    public DateTime FetchedAtUtc { get; set; }
}
