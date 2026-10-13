namespace Scadex.Signalization.Realtime;

/// <summary> <see cref="Hubs.ISignalizationHubClientContract.SignalCardPresented"/> modeli </summary>
/// <remarks>
/// <para>
/// Kabinin kart okuyucusuna kart okutuldu ve doğrulama sonucu belirledi.
/// </para>
/// <para>
/// Kart numarasını ve sahibini de taşır: kabin "Kart tanımlama" penceresi okunan kartı bu yayından gösterir.
/// </para>
/// </remarks>
/// <param name="CabinetId"> Yayın bu kabinin grubuna gider (<see cref="Hubs.SignalizationHub.CabinetGroupName"/>). </param>
/// <param name="IsAccepted"> Kart kabul edildi mi (<c>CardPresented</c>) yoksa reddedildi mi (<c>AccessDenied</c>). </param>
/// <param name="OccurredAtUtc"> SCADA'nın bildirdiği okuma anı. </param>
/// <param name="CardIdRaw"> SCADA'nın gönderdiği ham kart numarası. </param>
/// <param name="UserId"> Kart aktif bir kullanıcıya tanımlıysa sahibi; değilse <c>null</c>. </param>
/// <param name="UserFullName"> Sahibinin adı. </param>
public sealed record SignalCardPresentedMessage(Guid CabinetId, bool IsAccepted, DateTime OccurredAtUtc, string CardIdRaw, Guid? UserId, string? UserFullName);
