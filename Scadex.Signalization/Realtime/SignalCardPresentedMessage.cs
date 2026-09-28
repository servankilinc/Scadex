namespace Scadex.Signalization.Realtime;

/// <summary> <see cref="Hubs.ISignalizationHubClientContract.SignalCardPresented"/> modeli </summary>
/// <remarks>
/// <para>
/// Kabinin kart okuyucusuna kart okutuldu ve doğrulama sonucu belirledi.
/// </para>
/// </remarks>
/// <param name="CabinetId"> Yayın bu kabinin grubuna gider (<see cref="Hubs.SignalizationHub.CabinetGroupName"/>). </param>
/// <param name="IsAccepted"> Kart kabul edildi mi (<c>CardPresented</c>) yoksa reddedildi mi (<c>AccessDenied</c>). </param>
/// <param name="OccurredAtUtc"> SCADA'nın bildirdiği okuma anı. </param>
public sealed record SignalCardPresentedMessage(Guid CabinetId, bool IsAccepted, DateTime OccurredAtUtc);
