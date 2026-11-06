using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Diagram.Queries.Items;

public class DiagramCameraDto : IDto
{
    public CameraBrand Brand { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string? Username { get; set; }
    public bool HasPassword { get; set; }
}
