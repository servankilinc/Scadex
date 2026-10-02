using Scadex.Core.Model;

namespace Scadex.Model.Dtos.ComponentTemplate.Queries;

/// <summary> DeviceTypeId özelinde ile ComponenetTemplate bilgileri </summary>
public class ComponentTemplateListItemDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int DeviceTypeId { get; set; }
    public bool IsSystemTemplate { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string BackgroundColor { get; set; } = null!;
    /// <summary> Tablodaki küçük ön izleme için eklendi </summary>
    public string? BackgroundImageUrl { get; set; }
    public bool IsMonitorable { get; set; }
    public int PinCount { get; set; }
}
