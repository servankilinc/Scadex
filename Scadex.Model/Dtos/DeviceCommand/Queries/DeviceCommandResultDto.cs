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

    /// <summary> Gonderilen payload — <c>{"turnOn":true,"value":"0","polarity":2}</c>. </summary>
    public string? PayloadJson { get; set; }

    /// <summary> SCADA'ya giden ham deger (<c>"1"</c> / <c>"0"</c>). </summary>
    public string? SentValue { get; set; }

    /// <summary> Cozulen kontak kutbu: <c>NO</c>, <c>NC</c>, ya da kutup sorusu olmayan kanallarda (LED, duz dijital cikis) <c>null</c>. </summary>
    public PinFunction? ResolvedPolarity { get; set; }

    public CommandStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>SCADA'nin cevap suresi. <see cref="SentAt"/>/<see cref="RespondedAt"/> farkindan turetilir.</summary>
    public int? ElapsedMs { get; set; }
}
