using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Core.Utils;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Signalization.DataAccess;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;
using Scadex.Signalization.Runtime;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

public partial class OperatorSessionEngine
{
    private readonly SignalizationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeviceCommandService _commandService;
    private readonly IUserRoleService _userRoleService;
    private readonly EntrySnapshotQueue _snapshotQueue;
    private readonly ILogger<OperatorSessionEngine> _logger;

    public OperatorSessionEngine(SignalizationDbContext db, IUnitOfWork unitOfWork, IDeviceCommandService commandService, IUserRoleService userRoleService, EntrySnapshotQueue snapshotQueue, ILogger<OperatorSessionEngine> logger)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _commandService = commandService;
        _userRoleService = userRoleService;
        _snapshotQueue = snapshotQueue;
        _logger = logger;
    }

    public async Task HandleAsync(SignalWorkItem item, CancellationToken cancellationToken)
    {
        var cabinet = await _db.Cabinets.AsNoTracking().FirstOrDefaultAsync(c => c.CabinetId == item.CabinetId, cancellationToken);
        if (cabinet == null)
            return;

        // NOT IsEnabled kontrolü Zamanlayıcı harici "kart okutma" veya "input kanal değişimi" ile üretilen işlere uygulanmalı.
        // çünkü pasife alınmış bir kabinin operatör işlem sistem tarafından kapatılabilmeli vs. nedenler var
        switch (item)
        {
            case TimerWork timerWork:
                await HandleTimerAsync(cabinet, timerWork, cancellationToken);
                break;
            case ChannelChangedWork channelWork:
                if (cabinet.IsEnabled)
                    await HandleChannelChangedAsync(cabinet, channelWork, cancellationToken);
                break;
            case CardPresentedWork cardWork:
                if (cabinet.IsEnabled)
                    await HandleCardAsync(cabinet, cardWork.Notification, cancellationToken);
                break;
        }
    }


    #region Helpers
    private OperatorSession StartSession(SignalCabinet cabinet, SignalOuterDoor outer, DateTime startedAtUtc, SessionFlags flags)
    {
        var now = DateTime.UtcNow;
        var session = new OperatorSession
        {
            CabinetId = cabinet.CabinetId,
            OuterDoorId = outer.Id,
            OuterDoorNameSnapshot = outer.Name,
            Status = OperatorSessionStatus.Open,
            Flags = flags,
            StartedAtUtc = startedAtUtc,
            AwaitingCardDueAtUtc = cabinet.AwaitingCardTimeoutSec > 0 ? now.AddSeconds(cabinet.AwaitingCardTimeoutSec) : null,
            MaxDurationDueAtUtc = cabinet.SessionMaxDurationMin > 0 ? now.AddMinutes(cabinet.SessionMaxDurationMin) : null
        };

        _db.OperatorSessions.Add(session);
        return session;
    }


    private OperatorSessionEvent AddEvent(
        OperatorSession session,
        SessionEventType type,
        DateTime occurredAtUtc,
        DateTime? receivedAtUtc = null,
        Guid? innerDoorId = null,
        Guid? userId = null,
        string? cardIdRaw = null,
        Guid? deviceCommandId = null,
        string? detail = null
    ) {
        var sessionEvent = new OperatorSessionEvent
        {
            // Navigasyon uzerinden: yeni oturumun Id'si henuz yok, EF kayitta doldurur.
            Session = session,
            Type = type,
            OccurredAtUtc = occurredAtUtc,
            ReceivedAtUtc = receivedAtUtc ?? DateTime.UtcNow,
            InnerDoorId = innerDoorId,
            UserId = userId,
            CardIdRaw = cardIdRaw,
            DeviceCommandId = deviceCommandId,
            Detail = detail?.Truncate(512)
        };

        _db.OperatorSessionEvents.Add(sessionEvent);
        return sessionEvent;
    }


    /// <summary> Falg ekler ayrıca kapanmis bir oturuma sonradan flag eklendiyse (orn. siren komutu basarisiz) status uyarıyla tamamlandı olur. </summary>
    private static void MarkFlag(OperatorSession session, SessionFlags flag, bool setCompletedWithWarning = false)
    {
        session.Flags |= flag;
        if (setCompletedWithWarning && session.Status == OperatorSessionStatus.Completed)
            session.Status = OperatorSessionStatus.CompletedWithWarning;
    }


    /// <summary> Switch input kanalının son değeri. Okunamadıysa <c>null</c> (bilinmiyor). </summary>
    private async Task<bool?> IsSwitchOpenAsync(Guid switchIoChannelId, string openValue, CancellationToken cancellationToken)
    {
        var channel = await _unitOfWork.IoChannels.GetAsync(
            select: c => new { c.CurrentValue },
            where: c => c.Id == switchIoChannelId,
            cancellationToken: cancellationToken
        );

        if (channel?.CurrentValue == null)
            return null;

        return string.Equals(channel.CurrentValue, openValue, StringComparison.Ordinal);
    }


    /// <summary> Oturumu kapatır. </summary>
    private async Task CloseSessionAsync(OperatorSession session, DateTime endedAtUtc, bool timedOut, CancellationToken cancellationToken)
    {
        bool hasOperator = await _db.OperatorSessionOperators.AnyAsync(o => o.SessionId == session.Id, cancellationToken);
        if (!hasOperator)
            MarkFlag(session, SessionFlags.NoCardPresented);

        if (endedAtUtc < session.StartedAtUtc)
            endedAtUtc = session.StartedAtUtc;

        session.EndedAtUtc = endedAtUtc;
        session.DurationSec = (int)Math.Round((endedAtUtc - session.StartedAtUtc).TotalSeconds);
        
        // zamanlayıcı alanları sıfırlanır ki kontrol edildiğinde işlemesin
        session.AwaitingCardDueAtUtc = null;
        session.MaxDurationDueAtUtc = null; 

        if (timedOut)
        {
            MarkFlag(session, SessionFlags.TimedOut);
            session.Status = OperatorSessionStatus.TimedOut;
        }
        else
        {
            session.Status = session.Flags == SessionFlags.None ? OperatorSessionStatus.Completed : OperatorSessionStatus.CompletedWithWarning;
        }
    }


    /// <summary> Dis kapinin ardindaki TUM aktif ic kapilar kilitli mi (baska islem yapan yok mu)? </summary>
    private async Task<bool> AreAllInnerDoorsLockedAsync(Guid outerDoorId, CancellationToken cancellationToken)
    {
        var doorIds = await _db.InnerDoors
            .Where(i => i.OuterDoorId == outerDoorId && i.IsActive)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        return !await _db.InnerDoorStates.AnyAsync(s => doorIds.Contains(s.InnerDoorId) && s.IsUnlocked, cancellationToken);
    }
    #endregion


    #region Cekirdek okumalari ve komut



    /// <summary>
    /// Bir cikis kanalina komut gonderir. Cihaz kanaldan turetilir (kanalin karti). Retry YOKTUR.
    /// Basari = SCADA 2xx dondu (<see cref="CommandStatus.Succeeded"/>); aksi her durum basarisizliktir.
    /// </summary>
    private async Task<OutputCommandOutcome> SendOutputAsync(Guid ioChannelId, bool turnOn, CancellationToken cancellationToken)
    {
        var channel = await _unitOfWork.IoChannels.GetAsync(
            select: c => new { c.DeviceId },
            where: c => c.Id == ioChannelId,
            cancellationToken: cancellationToken);

        if (channel == null)
            return new OutputCommandOutcome(false, null, $"Kanal bulunamadi ({ioChannelId})");

        var result = await _commandService.SendAsync(channel.DeviceId, new DeviceCommandSendRequest
        {
            CommandType = DeviceCommandType.SetOutput,
            IoChannelId = ioChannelId,
            TurnOn = turnOn
        }, cancellationToken);

        if (!result.IsSuccess)
            return new OutputCommandOutcome(false, null, result.Error.Description ?? result.Message);

        return result.Data.Status == CommandStatus.Succeeded
            ? new OutputCommandOutcome(true, result.Data.Id, null)
            : new OutputCommandOutcome(false, result.Data.Id, $"{result.Data.Status}: {result.Data.ResultMessage}");
    }

    private readonly record struct OutputCommandOutcome(bool IsSuccess, Guid? CommandId, string? Message);
    #endregion
}
