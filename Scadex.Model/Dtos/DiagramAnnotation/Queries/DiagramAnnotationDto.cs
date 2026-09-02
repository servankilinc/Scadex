using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.DiagramAnnotation.Queries;

public class DiagramAnnotationDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public Guid CabinetId { get; set; }
    public string CabinetName { get; set; } = null!;
    public string Text { get; set; } = null!;
    public AnnotationShape Shape { get; set; }
    public string FontColor { get; set; } = null!;
    public double FontSize { get; set; }
    public bool IsBold { get; set; }
    public string BorderColor { get; set; } = null!;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
}