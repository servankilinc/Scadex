using Scadex.Signalization.Model.Entities;
using Scadex.Signalization.Model.Utils;

namespace Scadex.Signalization.Services.Abstract;

/// <summary>
/// Modülün kapı/siren/aydınlatma/kilit durumunu öğrenmesi için her sorgu ilgili kanalın scadex çekirdeğindeki son değerinden (<c>IoChannel.CurrentValue</c>) okunup yorumlanır.
/// <list type="bullet">
/// <item> Inputs: kapı switch değerleri <c>SwitchOpenValue</c>'suyla karşılaştırılır. </item>
/// <item> Outputs: son başarılı komutun değeri, kanalın NO/NC kablolamasına göre çekirdekte (<c>IIoChannelService.GetOutputStatesAsync</c>) mantıksal değere çevrilir; kilitte ayrıca <c>UnlockTurnsOn</c> uygulanır. </item>
/// </list>
/// </summary>
public interface ISignalChannelStateService
{
    #region I/O Çözümlemeleri      
    /// <summary> Dış kapının ardındaki TÜM kapılar kilitli ve kapalı mı (başka işlem yapan yok mu)? Kilidi "bilinmiyor" olan kapı kilitli sayılır; </summary>
    Task<bool> AreAllInnerDoorsLockedAsync(Guid outerDoorId, CancellationToken cancellationToken = default);

    /// <summary> Dış kapı açık mı? </summary>
    Task<ChannelReading> GetSwitchStateAsync(SignalOuterDoor outerDoor, CancellationToken cancellationToken = default);

    /// <summary> İç kapı açık mı? </summary>
    Task<ChannelReading> GetSwitchStateAsync(SignalInnerDoor innerDoor, CancellationToken cancellationToken = default);

    /// <summary> İç kapının kilidi AÇIK mı? </summary>
    Task<ChannelReading> GetLockStateAsync(SignalInnerDoor innerDoor, CancellationToken cancellationToken = default);

    /// <summary> Dış kapı aydınlatması yanıyor mu? Aydınlatma tanımsızsa "bilinmiyor". </summary>
    Task<ChannelReading> GetLightStateAsync(SignalOuterDoor outerDoor, CancellationToken cancellationToken = default);

    /// <summary> Kabin sireni çalıyor mu? Siren tanımsızsa "bilinmiyor". </summary>
    Task<ChannelReading> GetSirenStateAsync(SignalCabinet cabinet, CancellationToken cancellationToken = default);
    #endregion

    #region Toplu okuma (ui için, N+1'siz)
    /// <summary> Kabinin tüm kapı ve çıkış durumlarını okur </summary>
    Task<SignalCabinetReadings> ReadCabinetAsync(SignalCabinet cabinet, CancellationToken cancellationToken = default);

    /// <summary> Output kanallarının mantıksal durumu ("yük açık mı"). İstenen her kanal sözlükte bulunur. </summary>
    Task<IReadOnlyDictionary<Guid, ChannelReading>> ReadOutputsAsync(IReadOnlyCollection<Guid> ioChannelIds, CancellationToken cancellationToken = default);
    #endregion
}

/// <summary> <see cref="ISignalChannelStateService.ReadCabinetAsync"/> sonucu; sözlükler kapı kimliğiyle anahtarlanır. </summary>
public sealed record SignalCabinetReadings(
    ChannelReading Siren,
    IReadOnlyDictionary<Guid, ChannelReading> OuterDoorSwitches,
    IReadOnlyDictionary<Guid, ChannelReading> OuterDoorLights,
    IReadOnlyDictionary<Guid, ChannelReading> InnerDoorSwitches,
    IReadOnlyDictionary<Guid, ChannelReading> InnerDoorLocks
);
