using Scadex.Model.Dtos.MediaGateway;

namespace Scadex.Business.Utils.MediaGateway;

/// <summary>
/// MediaMTX auth kancasına (<c>POST /api/MediaGateway/auth</c>) gelen her yolu yetkilendirir. Scadex çekirdeği yolları bilmez; yolu üreten
/// modül kendi uygulamasını DI'a kaydeder (CameraManagement <c>cam_</c>, RemoteDesk <c>pc_</c>).
/// <para/>
/// Her yolun TEK sahibi olmalı: hiçbir yetkilendiricinin üstlenmediği ya da birden fazlasının üstlendiği yol reddedilir (401) —
/// bir modül başka bir modülün yolunu üstlenemesin diye. Önekler bu yüzden ayrık tutulur.
/// </summary>
public interface IMediaPathAuthorizer
{
    /// <summary> Bu yol bu yetkilendiricinin mi? Her istekte sorulur; yalnızca ada bakmalı (veritabanı/önbellek yok). </summary>
    bool CanHandle(string path);

    /// <summary> MediaMTX'in isteğini yetkilendirir. Eylem (<c>read</c>, <c>publish</c>…) ayrımını yetkilendirici yapar; tanımadığı eylemi reddetmelidir. </summary>
    Task<bool> AuthorizeAsync(MediaMtxAuthDto request, CancellationToken cancellationToken = default);
}
