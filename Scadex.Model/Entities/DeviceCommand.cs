using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

public class DeviceCommand : IEntity, ISoftDeletableEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? IoChannelId { get; set; }
    public DeviceCommandType CommandType { get; set; }
    public string? PayloadJson { get; set; }
    public CommandStatus Status { get; set; }
    public Guid? RequestedByUserId { get; set; }
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
    public bool IsDeleted { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? DeletedDateUtc { get; set; }
    #endregion

    #region *** EF Core Navigation ***
    public virtual Device? Device { get; set; }
    public virtual IoChannel? IoChannel { get; set; }
    public virtual User? RequesterUser { get; set; }
    #endregion
}