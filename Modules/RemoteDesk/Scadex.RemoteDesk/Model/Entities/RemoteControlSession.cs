using Scadex.Core.Model;
using static Scadex.RemoteDesk.Enums.RemoteDeskEnums;

namespace Scadex.RemoteDesk.Model.Entities;

/// <summary>
/// Uzaktan kontrol denetim kaydı (Faz 8): <b>kim, hangi PC'yi, ne zaman, ne kadar</b> kontrol etti. Kontrol verildiği an yazılır, bitince kapanır.
/// Canlı durum bellektedir (<c>RemoteControlCoordinator</c>); bu satır yalnızca denetim içindir. Girdinin kendisi (tıklama/konum) KAYDEDİLMEZ,
/// yalnızca sayısı. Saklama/temizlik işi yoktur.
/// </summary>
public class RemoteControlSession : IEntity
{
    /// <summary> Tarayıcıya ve PC'ye giden <c>controlSessionId</c>. </summary>
    public Guid Id { get; set; }

    /// <summary> Çekirdekteki <c>Device.Id</c> (FK DEĞİL). </summary>
    public Guid DeviceId { get; set; }

    /// <summary> Çekirdekteki <c>User.Id</c> (FK DEĞİL). </summary>
    public Guid UserId { get; set; }

    public DateTime StartedUtc { get; set; }

    /// <summary> <c>null</c> = hâlâ kontrol ediyor (ya da sunucu kapanırken açık kaldı; açılışta <c>ServerRestart</c> ile kapanır). </summary>
    public DateTime? EndedUtc { get; set; }

    public RemoteControlEndReason? EndReason { get; set; }

    /// <summary> PC'ye iletilen olay sayısı (hareketler tarayıcıda seyreltildikten sonra). </summary>
    public int InputEventCount { get; set; }
}
