using Scadex.Core.Model;

namespace Scadex.Model.Dtos.ComponentTemplate.Queries;

public class ComponentTemplateBaseDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int DeviceTypeId { get; set; }
    public bool IsSystemTemplate { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public string? BackgroundImageUrl { get; set; }
}