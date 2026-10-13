using Microsoft.EntityFrameworkCore;
using Scadex.Model.Dtos.Scada.Events;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Realtime;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Bir kullanıcının elle gönderdiği çıkış komutu (sanal kabin ya da diyagram): o kanalı kullanan kapının AÇIK işlemi varsa
/// zaman çizelgesine <see cref="SessionEventType.ManualCommand"/> / <see cref="SessionEventType.ManualCommandFailed"/> yazılır.
/// <para>
/// Yalnızca kayıttır: motor durumu, siren uzlaştırması ve bayraklar değişmez (elle komutun başarısızlığı işlemi uyarılı yapmaz).
/// Motorun kendi komutları buraya gelmez — gözlemci göndereni olmayan komutu kuyruğa almaz.
/// </para>
/// </summary>
public partial class OperatorSessionEngine
{
    private async Task HandleManualCommandAsync(SignalCabinet cabinet, OutputCommandNotification command, CancellationToken cancellationToken)
    {
        var channelId = command.IoChannelId;
        var targets = new List<(Guid? OuterDoorId, Guid? InnerDoorId, string Action)>();

        // 1) Kanalın kabindeki rolü. Siren kabin geneline aittir: kabindeki tüm açık işlemlere yazılır (OuterDoorId = null).
        if (cabinet.SirenIoChannelId == channelId)
            targets.Add((null, null, command.TurnOn ? SessionEventDetail.ManualSirenOn : SessionEventDetail.ManualSirenOff));

        var lightDoorIds = await _db.OuterDoors.AsNoTracking()
            .Where(d => d.CabinetId == cabinet.CabinetId && d.IsActive && d.LightIoChannelId == channelId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);
        foreach (var outerDoorId in lightDoorIds)
            targets.Add((outerDoorId, null, command.TurnOn ? SessionEventDetail.ManualLightOn : SessionEventDetail.ManualLightOff));

        // Kilidi açan mantıksal değer kilidin kendi ayarıdır (UnlockTurnsOn).
        var lockDoors = await _db.InnerDoors.AsNoTracking()
            .Where(i => i.IsActive && i.LockIoChannelId == channelId && i.OuterDoor!.IsActive && i.OuterDoor.CabinetId == cabinet.CabinetId)
            .Select(i => new { i.Id, i.OuterDoorId, i.UnlockTurnsOn })
            .ToListAsync(cancellationToken);
        foreach (var door in lockDoors)
            targets.Add((door.OuterDoorId, door.Id, command.TurnOn == door.UnlockTurnsOn ? SessionEventDetail.ManualUnlock : SessionEventDetail.ManualLock));

        // Modülü ilgilendirmeyen bir çıkış (ör. diyagramdan bir fan).
        if (targets.Count == 0)
            return;

        // 2) Kabinin açık işlemleri.
        var openSessions = await _db.OperatorSessions
            .Where(s => s.CabinetId == cabinet.CabinetId && s.EndedAtUtc == null)
            .ToListAsync(cancellationToken);
        if (openSessions.Count == 0)
            return;

        // 3) Her hedefin işlemine bir satır. Başarısızlıkta motorun CommandFailed biçimi: "eylem: durum: mesaj".
        bool succeeded = command.Status == CommandStatus.Succeeded;
        var touched = new HashSet<OperatorSession>();

        foreach (var (outerDoorId, innerDoorId, action) in targets)
        {
            foreach (var session in openSessions.Where(s => outerDoorId == null || s.OuterDoorId == outerDoorId))
            {
                AddEvent(
                    session: session,
                    type: succeeded ? SessionEventType.ManualCommand : SessionEventType.ManualCommandFailed,
                    occurredAtUtc: command.OccurredAtUtc,
                    innerDoorId: innerDoorId,
                    userId: command.RequestedByUserId,
                    deviceCommandId: command.CommandId,
                    detail: succeeded ? action : $"{action}: {command.Status}: {command.ResultMessage}"
                );
                touched.Add(session);
            }
        }

        if (touched.Count == 0)
            return;

        await _db.SaveChangesAsync(cancellationToken);

        // Interceptor yalnızca OperatorSession satırı değişince yayınlar; salt olay eklemesinde açık detay ekranı tazelensin diye elle.
        foreach (var session in touched)
            await _notifier.OperatorSessionChangedAsync(new OperatorSessionChangedMessage(session.Id, session.CabinetId, session.Status, IsOpen: true), cancellationToken);
    }
}
