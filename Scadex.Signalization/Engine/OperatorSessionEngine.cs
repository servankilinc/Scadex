using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Business.Abstract;
using Scadex.Core.Utils;
using Scadex.DataAccess.UoW;
using Scadex.Model.Dtos.DeviceCommand.Commands;
using Scadex.Signalization.Data;
using Scadex.Signalization.Entities;
using Scadex.Signalization.Runtime;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Operator islemi durum makinesi. Yalnizca <see cref="SignalEventWorker"/>'in kabin seridinden cagrilir: ayni kabinin
/// olaylari sirayla gelir, bu yuzden burada kilit/yaris kontrolu yoktur.
/// <para><b>Tek dogruluk kaynaklari:</b> kapinin acik/kapali oldugu anahtar kanalinin <c>IoChannel.CurrentValue</c>'su
/// (cekirdekten salt okunur), kilidin durumu <see cref="SignalInnerDoorState"/>, sirenin fiziksel durumu
/// <see cref="SignalCabinetState"/>.</para>
/// <para><b>Cekirdege yazmaz:</b> komutlar <c>IDeviceCommandService.SendAsync</c>'ten gecer (retry YOK — tekrarlanan role
/// darbesi basarisiz komuttan kotudur). Cekirdek okumalari projeksiyondur; cekirdek context'inde izlenen varlik birakmaz.</para>
/// </summary>
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
        // Zamanlayici isleri kabin sonradan kapatilmis olsa da islenir: acik kalmis bir siren talebi her kosulda kapanmali.
        var cabinet = await _db.Cabinets.AsNoTracking().FirstOrDefaultAsync(c => c.CabinetId == item.CabinetId, cancellationToken);
        if (cabinet == null)
            return; // Bu kabin modulun degil — sessizce gecilir, cekirdek olayi zaten isledi.

        if (item is TimerWork timer)
        {
            await HandleTimerAsync(cabinet, timer, cancellationToken);
            return;
        }

        if (!cabinet.IsEnabled)
            return;

        switch (item)
        {
            case ChannelChangedWork channelWork:
                await HandleChannelChangedAsync(cabinet, channelWork, cancellationToken);
                break;
            case CardPresentedWork cardWork:
                await HandleCardAsync(cabinet, cardWork.Notification, cancellationToken);
                break;
        }
    }

    private async Task HandleChannelChangedAsync(SignalCabinet cabinet, ChannelChangedWork work, CancellationToken cancellationToken)
    {
        var notification = work.Notification;

        // null = "kanal var ama okunamadi": kapinin durumu bilinmiyor, karar verilmez.
        if (notification.Value == null)
            return;

        var outer = await _db.OuterDoors.AsNoTracking().FirstOrDefaultAsync(d =>
            d.CabinetId == cabinet.CabinetId &&
            d.IsActive &&
            d.SwitchIoChannelId == notification.IoChannelId, cancellationToken);

        if (outer != null)
        {
            bool isOpen = string.Equals(notification.Value, outer.SwitchOpenValue, StringComparison.Ordinal);
            await HandleOuterSwitchAsync(cabinet, outer, isOpen, notification.OccurredAtUtc, notification.ReceivedAtUtc, cancellationToken);
            return;
        }

        var inner = await _db.InnerDoors.AsNoTracking().Include(i => i.OuterDoor).FirstOrDefaultAsync(i =>
            i.IsActive &&
            i.SwitchIoChannelId == notification.IoChannelId &&
            i.OuterDoor!.IsActive &&
            i.OuterDoor.CabinetId == cabinet.CabinetId, cancellationToken);

        if (inner != null)
        {
            bool isOpen = string.Equals(notification.Value, inner.SwitchOpenValue, StringComparison.Ordinal);
            await HandleInnerSwitchAsync(cabinet, inner, isOpen, notification.OccurredAtUtc, notification.ReceivedAtUtc, cancellationToken);
        }

        // Ne dis ne ic kapi anahtari: bu kanal modulu ilgilendirmiyor.
    }

    #region Oturum yardimcilari
    private Task<OperatorSession?> GetOpenSessionAsync(Guid outerDoorId, CancellationToken cancellationToken)
        => _db.OperatorSessions.FirstOrDefaultAsync(s => s.OuterDoorId == outerDoorId && s.EndedAtUtc == null, cancellationToken);

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
            // Sureler SUNUCU saatinden hesaplanir: SCADA'nin saati kaymis olabilir, zamanlayici sunucu saatiyle tarar.
            AwaitingCardDueAtUtc = cabinet.AwaitingCardTimeoutSec > 0 ? now.AddSeconds(cabinet.AwaitingCardTimeoutSec) : null,
            MaxDurationDueAtUtc = cabinet.SessionMaxDurationMin > 0 ? now.AddMinutes(cabinet.SessionMaxDurationMin) : null
        };

        _db.OperatorSessions.Add(session);
        return session;
    }

    /// <summary> Oturumu kapatir. Operator satirlari okunacagi icin cagirmadan ONCE kaydedilmis olmalidir. </summary>
    private async Task CloseSessionAsync(OperatorSession session, DateTime endedAtUtc, bool timedOut, CancellationToken cancellationToken)
    {
        bool hasOperator = await _db.OperatorSessionOperators.AnyAsync(o => o.SessionId == session.Id, cancellationToken);
        if (!hasOperator)
            session.Flags |= SessionFlags.NoCardPresented;

        if (endedAtUtc < session.StartedAtUtc)
            endedAtUtc = session.StartedAtUtc;

        session.EndedAtUtc = endedAtUtc;
        session.DurationSec = (int)Math.Round((endedAtUtc - session.StartedAtUtc).TotalSeconds);
        session.AwaitingCardDueAtUtc = null;
        session.MaxDurationDueAtUtc = null;

        if (timedOut)
        {
            session.Flags |= SessionFlags.TimedOut;
            session.Status = OperatorSessionStatus.TimedOut;
        }
        else
        {
            session.Status = session.Flags == SessionFlags.None ? OperatorSessionStatus.Completed : OperatorSessionStatus.CompletedWithWarning;
        }
    }

    /// <summary> Kapanmis bir oturuma sonradan bayrak eklendiyse (orn. siren komutu basarisiz) durum uyarili olur. </summary>
    private static void MarkFlag(OperatorSession session, SessionFlags flag)
    {
        session.Flags |= flag;
        if (session.Status == OperatorSessionStatus.Completed)
            session.Status = OperatorSessionStatus.CompletedWithWarning;
    }

    private OperatorSessionEvent AddEvent(OperatorSession session, SessionEventType type, DateTime occurredAtUtc, DateTime? receivedAtUtc = null,
        Guid? innerDoorId = null, Guid? userId = null, string? cardIdRaw = null, Guid? deviceCommandId = null, string? detail = null)
    {
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
    #endregion

    #region Cekirdek okumalari ve komut
    /// <summary> Anahtar kanalinin son degeri "acik" mi? Kanal yoksa ya da okunamadiysa <c>null</c> (bilinmiyor). </summary>
    private async Task<bool?> IsSwitchOpenAsync(Guid switchIoChannelId, string openValue, CancellationToken cancellationToken)
    {
        var channel = await _unitOfWork.IoChannels.GetAsync(
            select: c => new { c.CurrentValue },
            where: c => c.Id == switchIoChannelId,
            cancellationToken: cancellationToken);

        if (channel?.CurrentValue == null)
            return null;

        return string.Equals(channel.CurrentValue, openValue, StringComparison.Ordinal);
    }

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
