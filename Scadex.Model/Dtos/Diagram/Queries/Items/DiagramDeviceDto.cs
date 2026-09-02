using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Diagram.Queries.Items;

public class DiagramDeviceDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; }
    public bool IsActive { get; set; }
    public Guid ComponentTemplateId { get; set; }
    /// <summary>SCADA tarafindaki kimlik; ingest bu kodla cihaz cozumler.</summary>
    public string? ExternalCode { get; set; }
    public int? DeviceStatusId { get; set; }
    public string? DeviceStatusName { get; set; }
    public DateTime? LastSeen { get; set; }
    public DiagramComponentTemplateDto Template { get; set; } = null!;
    public ICollection<DiagramPinDto> Pins { get; set; } = [];
    public ICollection<DiagramIoChannelDto> IoChannels { get; set; } = [];
}

public class DiagramComponentTemplateDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int DeviceTypeId { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string BackgroundColor { get; set; } = null!;
    public string? BackgroundImageUrl { get; set; }
}


public class DiagramPinDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
    public HandleSide Side { get; set; }
    public PinFunction Function { get; set; }
    public PinDirection Direction { get; set; }
    public VoltageLevel? VoltageLevel { get; set; }
    public int? ChannelNumber { get; set; }
    public Guid? ComponentTemplatePinId { get; set; }
    public Guid? IoChannelId { get; set; }
}



/// <summary> Cihazin  <c>CurrentValue</c> ve <c>ValueUpdatedAt</c> bilgileri BILEREK YOK. Canli deger SignalR kanalından akar </summary>
public class DiagramIoChannelDto : IDto
{
    public Guid Id { get; set; }
    public int ChannelNumber { get; set; }
    public PinDirection Direction { get; set; }
    public bool IsEnabled { get; set; }
    public string Name { get; set; } = null!;
}
