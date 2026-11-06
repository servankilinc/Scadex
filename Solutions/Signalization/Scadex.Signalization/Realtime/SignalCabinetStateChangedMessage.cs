using Scadex.Signalization.Enums;

namespace Scadex.Signalization.Realtime;

/// <summary> <see cref="Hubs.ISignalizationHubClientContract.SignalCabinetStateChanged"/> modeli </summary>
/// <remarks>
/// <para>
/// Kabinin output (siren, dis kapi aydinlatmasi, ic kapi kilidi) durum değişimlerini temsil eden modeldir. 
/// </para>
/// <para>
/// Scadex çekirdeğinde başarılı bir gönderildiğinde <c>IScadaEventObserver.OnChannelChangedAsync</c> ile modulu haberdar eder,
/// <c>SignalRealtimeWorker</c> output kanalını siren/aydinlatma/kilide eşleyip bu mesajı gonderir. 
/// </para>
/// </remarks>
/// <param name="CabinetId"> Yayin bu kabinin grubuna gider (<see cref="Hubs.SignalizationHub.CabinetGroupName"/>). </param>
/// <param name="Target"> Hangi cikis degisti. </param>
/// <param name="TargetId"> <see cref="SignalCabinetOutput.Siren"/> icin <c>null</c> digerlerinde dış kapı Id'si (çünkü siren dış kapı ile ilişkili değil kabinde bir tane var kabin ile ilişkili); </param>
/// <param name="IsOn"> Siren caliyor / LED yaniyor / kilit ACIK. </param>
/// <param name="ChangedAtUtc"> Durumun bu degere gectigi an (<c>IoChannel.ValueUpdatedAt</c>). </param>
public sealed record SignalCabinetStateChangedMessage(
    Guid CabinetId,
    SignalCabinetOutput Target,
    Guid? TargetId,
    bool IsOn,
    DateTime ChangedAtUtc
);
