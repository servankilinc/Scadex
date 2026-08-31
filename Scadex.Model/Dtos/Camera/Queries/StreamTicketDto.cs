using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Camera.Queries;

/// <summary>
/// Canli izleme bileti. Sozlesme: <c>docs/api-contract/11-camera.md</c>
///
/// <b>Icinde RTSP adresi, kullanici adi veya parola YOKTUR ve olmayacaktir.</b>
/// Tarayici kameraya asla dogrudan baglanmaz; medya gecidine baglanir ve
/// gecit de kameraya. Bu DTO o zincirin tarayiciya acilan tek halkasidir.
/// </summary>
public class StreamTicketDto : IDto
{
    /// <summary>
    /// Tarayicinin SDP teklifini gonderecegi WHEP adresi.
    ///
    /// Sunucunun degil <b>TARAYICININ</b> ulasacagi adres — <c>Mediamtx</c>
    /// bolumundeki <c>WebRtcPublicBaseUrl</c>'den uretilir.
    /// </summary>
    public string WhepUrl { get; set; } = null!;

    /// <summary>
    /// Tek kullanimlik olmayan ama KISA OMURLU, opak bilet.
    ///
    /// Istemci bunu <c>Authorization: Basic base64("ticket:" + ticket)</c>
    /// olarak gonderir; medya gecidi de dogrulamak icin bize geri sorar.
    ///
    /// <b>Yola baglidir</b>: A kamerasi icin alinmis bir bilet B kamerasinin
    /// adresinde calismaz.
    /// </summary>
    public string Ticket { get; set; } = null!;

    /// <summary>
    /// Biletin son gecerlilik ani (UTC). Istemci baglantisi koptugunda eski
    /// bileti tekrar kullanmaya calismamali, YENISINI istemelidir.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
