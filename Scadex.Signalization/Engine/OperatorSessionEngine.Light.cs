using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary>
/// Dis kapi aydinlatmasi: kapi basina TEK, istege bagli bir cikis kanali. Karanlik saatlerde
/// (<see cref="IsLightingHours"/>) kapi acilinca yakilir, kapi kapaninca SAAT BAKILMADAN sondurulur —
/// 17:30'da yanan LED sabah 08:10'da kapanan kapiyla da sonmeli.
/// <para/>
/// SCADA cikis durumunu geri vermedigi icin son gonderdigimiz komut <see cref="SignalOuterDoorState"/>
/// satirinda tutulur; istenen durum son bilinen durumla ayniysa komut GITMEZ (sirenle ayni uzlastirma).
/// <para/>
/// Saat esigi kodda SABITTIR (yapilandirma alani yok, 2026-09-18 karari). Polarite alani da yoktur:
/// NO/NC cevrimi <c>DeviceCommandService.ResolvePolarityAsync</c> icinde pinin fonksiyonundan yapiliyor,
/// ikinci bir polarite bayragi onu yalanlardi.
/// </summary>
public partial class OperatorSessionEngine
{
    /// <summary> Aydinlatmanin yakilacagi saat araligi: 17:00–08:00 (sunucu YEREL saati). </summary>
    private static bool IsLightingHours(DateTime localNow) => localNow.Hour >= 17 || localNow.Hour < 8;

    /// <summary>
    /// Dis kapinin aydinlatmasini istenen duruma uzlastirir. Kanal tanimsizsa ya da istenen durum son
    /// bilinen durumla ayniysa komut gonderilmez.
    /// </summary>
    private async Task ReconcileOuterDoorLightAsync(SignalOuterDoor outer, OperatorSession session, bool desired, CancellationToken cancellationToken)
    {
        // 1) Aydinlatma tanimli degilse bu kapida yonetilecek bir sey yok.
        if (outer.LightIoChannelId is not Guid lightChannelId)
            return;

        // 2) Son bilinen durum (kayit yoksa LED sonuk sayilir).
        var state = await _db.OuterDoorStates.FirstOrDefaultAsync(s => s.OuterDoorId == outer.Id, cancellationToken);
        if (state == null)
        {
            state = new SignalOuterDoorState
            {
                OuterDoorId = outer.Id,
                LightIsOn = false
            };
            _db.OuterDoorStates.Add(state);
        }

        // 3) Istenen durum zaten saglanmis: komut gitmez (tekrarlanan role darbesi olurdu).
        if (state.LightIsOn == desired)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // 4) Komut SCADA'nin zaman asimi kadar surebilir: oncesindeki olaylar kalici olsun.
        await _db.SaveChangesAsync(cancellationToken);

        var outcome = await SendOutputAsync(lightChannelId, turnOn: desired, cancellationToken);
        var now = DateTime.UtcNow;

        if (outcome.IsSuccess)
        {
            state.LightIsOn = desired;
            state.ChangedAtUtc = now;
            state.LastCommandId = outcome.CommandId;

            AddEvent(
                session: session,
                type: desired ? SessionEventType.LightOn : SessionEventType.LightOff,
                occurredAtUtc: now,
                deviceCommandId: outcome.CommandId
            );
        }
        else
        {
            _logger.LogWarning("Kabin {CabinetId}: '{Door}' aydinlatma {Action} komutu basarisiz: {Message}",
                session.CabinetId, outer.Name, desired ? "yakma" : "sondurme", outcome.Message);

            MarkFlag(session, SessionFlags.CommandFailed, true);
            AddEvent(
                session: session,
                type: SessionEventType.CommandFailed,
                occurredAtUtc: now,
                deviceCommandId: outcome.CommandId,
                detail: $"{SessionEventDetail.Light}: {outcome.Message}"
            );
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
