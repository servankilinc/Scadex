using Scadex.Core.Model;

namespace Scadex.RemoteDesk.Model.Entities;

/// <summary>
/// İzleme denetim kaydı: <b>kim, hangi PC'nin hangi monitörünü, ne zaman</b> izledi. İzleyici (kiralama) başına bir satır;
/// birden fazla izleyici aynı <see cref="ScreenSession"/>'ı paylaşır. Saklama/temizlik işi yoktur
/// </summary>
public class ScreenViewLog : IEntity
{
    public long Id { get; set; }

    public Guid ScreenSessionId { get; set; }

    /// <summary> Çekirdekteki <c>Device.Id</c> (FK DEĞİL). Oturumdan da okunabilir; rapor sorgusu JOIN'siz olsun diye tekrarlanır. </summary>
    public Guid DeviceId { get; set; }

    public int MonitorIndex { get; set; }

    /// <summary> Çekirdekteki <c>User.Id</c> (FK DEĞİL). </summary>
    public Guid UserId { get; set; }

    public DateTime StartedUtc { get; set; }

    /// <summary> Kiralama bırakıldı ya da düştü; <c>null</c> = hâlâ izliyor (ya da sunucu kapanırken açık kaldı). </summary>
    public DateTime? EndedUtc { get; set; }

    #region *** EF Core Navigation ***
    public virtual ScreenSession? ScreenSession { get; set; }
    #endregion
}
