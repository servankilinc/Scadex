using Scadex.Core.Model;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Model.Entities;

/// <summary>
/// Bir PC monitörünün bir yayın ömrü (istemcide bir FFmpeg). (PC, monitör) başına aynı anda en fazla bir açık oturum olur.
/// Cihazın adı/kabini/MAC'i buraya KOPYALANMAZ — her zaman çekirdekteki <c>Device</c>'tan okunur.
/// </summary>
public class ScreenSession : IEntity
{
    /// <summary> Sunucu üretir; istemciye <c>sessionId</c> olarak gider, komutların idempotency anahtarıdır. </summary>
    public Guid Id { get; set; }

    /// <summary> Çekirdekteki <c>Device.Id</c> (FK DEĞİL — modül kuralı). </summary>
    public Guid DeviceId { get; set; }

    /// <summary> İstemcinin monitör sırası (<c>MonitorInfo.Index</c>). </summary>
    public int MonitorIndex { get; set; }

    /// <summary> MediaMTX yolu, <c>pc_{deviceId:N}_{monitör}</c>. </summary>
    public string MediaPath { get; set; } = null!;

    public ScreenSessionStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary> Yolun MediaMTX'te hazır olduğu an. </summary>
    public DateTime? StartedUtc { get; set; }

    public DateTime? StoppedUtc { get; set; }

    public ScreenStopReason? StopReason { get; set; }

    /// <summary> Yalnızca sabit metinler (istemcinin <c>FailureExplainer</c>'ı); ham FFmpeg çıktısı ASLA (içinde bilet olabilir). </summary>
    public string? FailureReason { get; set; }
}
