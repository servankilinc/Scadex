using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.DeviceCommand.Queries;

public class DeviceCommandResultDto : IDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? IoChannelId { get; set; }
    public int? ChannelNumber { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public DeviceCommandType CommandType { get; set; }

    /// <summary>Gonderilen payload (<c>{"value":"1"}</c>).</summary>
    public string? PayloadJson { get; set; }
    public CommandStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>SCADA'nin cevap suresi. <see cref="SentAt"/>/<see cref="RespondedAt"/> farkindan turetilir.</summary>
    public int? ElapsedMs { get; set; }
}
