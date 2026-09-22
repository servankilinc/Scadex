using Scadex.Signalization.Enums;

namespace Scadex.Signalization.Realtime;

/// <summary> <see cref="Hubs.ISignalizationHubClientContract.SignalDoorSwitchChanged"/> modeli </summary>
/// <remarks>
/// İç/Dış Kapı switch (input) yeni değeri, kapının <c>SwitchOpenValue</c>'suyla yorumlanmış halde gelir.
/// </remarks>
/// <param name="CabinetId"> Yayın bu kabinin grubuna gider (<see cref="Hubs.SignalizationHub.CabinetGroupName"/>). </param>
/// <param name="DoorKind"> Dış kapı mı iç kapı mı. </param>
/// <param name="DoorId"> Kapının Id'si. </param>
/// <param name="IsOpen"> Kapı açık mı; kanal değeri okunamadıysa <c>null</c> ("bilinmiyor"). </param>
/// <param name="ChangedAtUtc"> Kanalın bu değere geçtiği an (<c>IoChannel.ValueUpdatedAt</c>). </param>
public sealed record SignalDoorSwitchChangedMessage(
    Guid CabinetId,
    SignalDoorKind DoorKind,
    Guid DoorId,
    bool? IsOpen,
    DateTime ChangedAtUtc
);
