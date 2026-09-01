using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.DeviceCommand.Queries;

public class DeviceCommandDto : IDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = null!;
    public Guid? IoChannelId { get; set; }
    public DeviceCommandType CommandType { get; set; }
    public string? PayloadJson { get; set; }
    public CommandStatus Status { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public string UserFullName { get; set; } = null!;
    public DateTime? SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? ResultMessage { get; set; }
    
    #region --- IAuditableEntity ---
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; } 
    #endregion

    #region --- ISoftDeletableEntity ---
    public string? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDateUtc { get; set; } 
    #endregion
}