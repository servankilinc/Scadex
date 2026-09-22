using Microsoft.Extensions.Logging;
using Scadex.Signalization.Enums;
using Scadex.Signalization.Model.Entities;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Engine;

/// <summary> Aydınlatam  (<see cref="IsLightingHours"/>) saatlerinde kapi açılınca yanar ve kapı kapanınca saate bakılmadan söndürülür </summary>
public partial class OperatorSessionEngine
{
    /// <summary> Aydınlatmanın yakılacağı saat aralığı: 17:00–08:00 </summary>
    private static bool IsLightingHours(DateTime localNow) => localNow.Hour >= 17 || localNow.Hour < 8;

    /// <summary> Dış kapının aydınlatması istenen duruma göre komut gönderilir event kayıtları atılır.
    /// </summary>
    private async Task ReconcileOuterDoorLightAsync(SignalOuterDoor outer, OperatorSession session, bool desired, CancellationToken cancellationToken)
    {
        // 1) Aydinlatma tanimli degilse bu kapida yonetilecek bir sey yok.
        if (outer.LightIoChannelId is not Guid lightChannelId)
            return;

        // 2) Son bilinen durum kanaldan okunur ("bilinmiyor" ise komutla netlestirilir).
        var light = await _signalizationChannelState.GetLightStateAsync(outer, cancellationToken);

        // 3) İstenen durum zaten uygulanmış
        if (light.IsOn == desired)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // 4) Komut SCADA'nin zaman asimi kadar surebilir: oncesindeki olayları kaydet
        await _db.SaveChangesAsync(cancellationToken);

        var outcome = await SendOutputAsync(lightChannelId, turnOn: desired, cancellationToken);
        var now = DateTime.UtcNow;

        if (outcome.IsSuccess)
        {
            AddEvent(
                session: session,
                type: desired ? SessionEventType.LightOn : SessionEventType.LightOff,
                occurredAtUtc: now,
                deviceCommandId: outcome.CommandId
            );
        }
        else
        {
            _logger.LogWarning("Kabin {CabinetId}: '{Door}' aydinlatma {Action} komutu basarisiz: {Message}", session.CabinetId, outer.Name, desired ? "yakma" : "sondurme", outcome.Message);

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
