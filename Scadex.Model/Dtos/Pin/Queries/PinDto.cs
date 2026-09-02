using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Pin.Queries;

public class PinDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
    public PinFunction Function { get; set; }
    public VoltageLevel? VoltageLevel { get; set; }
    public Guid DeviceId { get; set; }
}