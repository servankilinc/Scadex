using CabinetOs.Core.Utils.ResultPattern;
using CabinetOs.Model.Entities;
using static CabinetOs.Model.Enums.EntityEnums;

namespace CabinetOs.Business.Utils.MediaGateway;

/// <summary>
/// Medya gecidinin (MediaMTX) yonetim yuzu.
///
/// <b>Medya buradan GECMEZ.</b> Bu gecit yalnizca yapilandirma yazar; goruntu
/// <c>Kamera -> MediaMTX -> Tarayici</c> yolunu izler ve hicbir zaman ASP.NET
/// uzerinden akmaz. Tek istisna anlik goruntudur (kisa omurlu JPEG proxy'si).
///
/// <b>Yollar <c>mediamtx.yml</c>'de STATIK TANIMLI DEGILDIR</b> ve calisma
/// aninda yazilir: kaynak adres kameranin satirindan turuyor, dolayisiyla
/// yapilandirma dosyasina yazilsaydi veritabaninin kopyasi olurdu.
/// </summary>
public interface IMediaGateway
{
    /// <summary>
    /// Canli izleme yolunu kurar (varsa gunceller).
    ///
    /// <b>Idempotenttir ve kendi kendini onarir:</b> MediaMTX yeniden
    /// baslatildiginda tum yollarini kaybeder; bir sonraki bilet istegi yolu
    /// yeniden kurar. Bu yuzden ayri bir "senkronizasyon" servisi yok.
    /// </summary>
    Task<Result> EnsureLivePathAsync(Camera camera, StreamProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Klip cekimi icin KAYIT ACIK, gecici bir yol kurar.
    ///
    /// <b>Canli yol kullanilamaz:</b> uzerinde kaydi acmak yapilandirmayi
    /// degistirmek demektir ve o an izleyen herkesin akisini kopartirdi.
    /// </summary>
    /// <param name="recordPath">
    /// MediaMTX'in segmentleri yazacagi sablon. Zaman yer tutucusu
    /// (<c>%Y</c>, <c>%M</c> ...) icermek ZORUNDA.
    /// </param>
    /// <param name="segmentDuration">
    /// Segment suresi (<c>"13s"</c> gibi). Klip suresinden uzun tutulur ki
    /// istenen sure tek bir dosyaya dussun.
    /// </param>
    Task<Result> EnsureClipPathAsync(
        Camera camera,
        long captureId,
        string recordPath,
        string segmentDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Yolu siler. <b>404 BASARI sayilir</b> — silinmek istenen sey zaten yoksa
    /// cagiran acisindan sonuc aynidir.
    /// </summary>
    Task<Result> DeletePathAsync(string pathName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Canli izleme yolunun adi. <b>Kolon degildir</b>, <c>Id</c>'den turetilir
    /// (bkz. <c>Camera</c> XML dokumani).
    /// </summary>
    static string LivePathName(Guid cameraId, StreamProfile profile) =>
        $"cam_{cameraId:N}_{profile.ToString().ToLowerInvariant()}";

    /// <summary>Klip cekiminin gecici yol adi.</summary>
    static string ClipPathName(long captureId) => $"clip_{captureId}";
}
