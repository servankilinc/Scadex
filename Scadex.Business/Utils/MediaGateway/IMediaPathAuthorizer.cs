using Scadex.Model.Dtos.Camera.Commands;

namespace Scadex.Business.Utils.MediaGateway;

/// <summary>
/// MediaMTX auth kancasına (<c>POST /api/MediaGateway/auth</c>) kamera dışındaki Pathleri yetkilendirir Scadex çekirdeği bilmez, external modüller(RemoteDesk) kendi uygulamasını DI'a kaydeder).
/// <para/>
/// Kamera path (<c>cam_</c>) BU ARAYÜZE HİÇ SORULMAZ: onları her zaman <c>ICameraService.ValidateStreamTokenAsync</c> yetkilendirir  ve yalnızca <c>read</c> kabul edilir.
/// </summary>
public interface IMediaPathAuthorizer
{
    /// <summary> Bu yol bu scadex'in mi? her istekte sırayla sorulur. </summary>
    bool CanHandle(string path);

    /// <summary> MediaMTX'in isteğini yetkilendirir. Eylem (<c>read</c>, <c>publish</c>…) ayrımını yetkilendirici yapar; tanımadığı eylemi reddetmelidir. </summary>
    Task<bool> AuthorizeAsync(MediaMtxAuthDto request, CancellationToken cancellationToken = default);
}
