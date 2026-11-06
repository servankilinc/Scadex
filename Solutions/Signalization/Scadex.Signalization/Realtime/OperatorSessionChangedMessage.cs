using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Realtime;

/// <summary>
/// <see cref="Hubs.ISignalizationHubClientContract.OperatorSessionChanged"/> govdesi. Frontend aynasi:
/// <c>modules/signalization/models/realtime.ts</c>.
/// </summary>
/// <remarks>
/// Yayin bir BILDIRIMDIR: istemci veriyi HTTP ucundan yeniden okur. Alanlar yalnizca istemcinin "bu olay beni ilgilendiriyor mu"
/// kararini sorgusuz verebilmesi icindir.
/// </remarks>
public sealed record OperatorSessionChangedMessage(long SessionId, Guid CabinetId, OperatorSessionStatus Status, bool IsOpen);
