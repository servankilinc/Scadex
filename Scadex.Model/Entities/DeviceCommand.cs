using Scadex.Core.Model;
using Scadex.Core.Utils;
using Scadex.Model.Dtos.DeviceCommand.Queries;
using Scadex.Model.Scada.Commands;
using System.Text.Json;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Entities;

public class DeviceCommand : IEntity, ISoftDeletableEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? IoChannelId { get; set; }
    /// <summary>
    /// Tek komut türü vardır: çıkış kanalını aç/kapat. Komut tipi kolonu 2026-10-31'de kaldırıldı (tek değerliydi, hiçbir karar ona
    /// bakmıyordu). Niyet (<c>turnOn</c>), gönderilen değer ve kutup <see cref="ScadaCommandPayload"/> olarak burada saklanır.
    /// </summary>
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


    public DeviceCommandResultDto ToResultDto(int? channelNumber, string? requestedByName)
    {
        ScadaCommandPayload? payload = null;
        try
        {
            payload = string.IsNullOrWhiteSpace(this.PayloadJson) ? null :
                JsonSerializer.Deserialize<ScadaCommandPayload>(this.PayloadJson, ProjectJsonOptions.SerializerOptions);
        }
        finally { }

        return new DeviceCommandResultDto
        {
            Id = this.Id,
            DeviceId = this.DeviceId,
            IoChannelId = this.IoChannelId,
            ChannelNumber = channelNumber,
            TurnOn = payload?.TurnOn,
            PayloadJson = this.PayloadJson,
            SentValue = payload?.Value,
            ResolvedPolarity = payload?.Polarity,
            Status = this.Status,
            ResultMessage = this.ResultMessage,
            SentAt = this.SentAt,
            RespondedAt = this.RespondedAt,
            ElapsedMs =
                (this.SentAt is not DateTime sent || this.RespondedAt is not DateTime responded) ? 0 : (int)Math.Max(0, (responded - sent).TotalMilliseconds),
            RequestedByUserId = this.RequestedByUserId,
            RequestedByName = requestedByName
        };
    }
}