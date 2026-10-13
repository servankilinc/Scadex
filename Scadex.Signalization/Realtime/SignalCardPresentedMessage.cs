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
/// <param name="CardId"> SCADA'nın gönderdiği ham kart numarası. </param>
/// <param name="UserId"> Kart aktif bir kullanıcıya tanımlıysa sahibi; değilse <c>null</c>. </param>
/// <param name="UserFullName"> Sahibinin adı. </param>
/// <param name="Detail">
/// Olay satırına yazılanla aynı <see cref="Enums.SessionEventDetail"/> kodu: redde gerekçe (<c>UnknownCard</c>, <c>NoAuthority</c>…),
/// kabulde varsa <c>OuterSwitchClosed</c>, yoksa <c>null</c>.
/// </param>
public sealed record SignalCardPresentedMessage(Guid CabinetId, bool IsAccepted, DateTime OccurredAtUtc, string CardId, Guid? UserId, string? UserFullName, string? Detail);
