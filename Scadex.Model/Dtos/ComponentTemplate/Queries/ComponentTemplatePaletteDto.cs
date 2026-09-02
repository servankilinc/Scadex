using Scadex.Core.Model;

namespace Scadex.Model.Dtos.ComponentTemplate.Queries;

public class ComponentTemplatePaletteDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int DeviceTypeId { get; set; }
    public bool IsSystemTemplate { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public string? BackgroundImageUrl { get; set; }

    /// <summary> Bos olabilir: pano cercevesi gibi dekoratif bir sablonun pini olmayabilir, o zaman cihaz da pinsiz dogar. </summary>
    public List<ComponentTemplatePalettePinDto> Pins { get; set; } = [];
}
