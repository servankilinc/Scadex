using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Camera.Commands;

/// <summary>
/// Medya gecidinin (MediaMTX) kimlik dogrulama kancasina gonderdigi govde.
///
/// <b>Bu BIZIM sozlesmemiz DEGILDIR</b> — alan adlari ve anlamlari MediaMTX
/// tarafindan belirlenir (<c>authHTTPAddress</c> ayari). Bu yuzden burada
/// kod tabaninin adlandirma tercihleri gecerli degil; sinif yalnizca gelen
/// JSON'un seklini yansitir. Alanlarin tamami nullable: guvenilmeyen bir
/// kaynaktan geliyor ve eksik gelmesi bir istisna degil, reddedilecek bir
/// istek olmalidir.
///
/// Yeni bir alan gerekirse once MediaMTX surumunun dokumantasyonu kontrol
/// edilmeli; buraya tahminle alan eklemek sessizce null kalir.
/// </summary>
public class MediaMtxAuthDto : IDto
{
    /// <summary>
    /// Basic kimlik dogrulamanin kullanici kismi. Bizim akisimizda sabit
    /// <c>"ticket"</c> metnidir; anlamli olan <see cref="Password"/>'dur.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Basic kimlik dogrulamanin parola kismi — <b>bizim biletimiz burada gelir</b>.
    /// Istemci <c>Basic base64("ticket:" + bilet)</c> gonderir, MediaMTX bunu
    /// ikiye ayirir ve parola kismini buraya yazar.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>MediaMTX'in kendi token akisi — kullanilmiyor.</summary>
    public string? Token { get; set; }

    /// <summary>Istemcinin IP'si.</summary>
    public string? Ip { get; set; }

    /// <summary>
    /// Istenen eylem: <c>read</c>, <c>publish</c>, <c>playback</c>, <c>api</c>,
    /// <c>metrics</c>, <c>pprof</c>. Yalnizca <c>read</c> kabul edilir.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Erisilmek istenen yol adi (orn. <c>cam_{guid}_sub</c>). Biletin bagli
    /// oldugu yolla BIREBIR eslesmeli.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>Protokol (<c>webrtc</c>, <c>rtsp</c> ...).</summary>
    public string? Protocol { get; set; }

    /// <summary>MediaMTX'in oturum kimligi.</summary>
    public string? Id { get; set; }

    /// <summary>Istegin sorgu dizesi.</summary>
    public string? Query { get; set; }
}
