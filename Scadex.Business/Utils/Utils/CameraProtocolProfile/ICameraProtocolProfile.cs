using CabinetOs.Model.Entities;
using static CabinetOs.Model.Enums.EntityEnums;

namespace CabinetOs.Business.Utils.CameraProtocolProfile;

/// <summary>
/// Bir kamera markasinin URL sablonlari.
///
/// <b>Neden ayri bir soyutlama:</b> <c>Camera.Manufacturer</c> kolonu tam olarak
/// bunun icin var — XML dokumaninda "ISAPI yolu ve RTSP yol sablonu ureticiye
/// gore degisir; ikinci bir marka geldiginde ayrim koda degil VERIYE bakarak
/// yapilabilsin" yaziyor. Yollari servis govdesine gommek o kolonu anlamsiz
/// kilar ve ikinci marka geldiginde her cagri yerine bir <c>if</c> ekletirdi.
///
/// Bugun tek uygulama <see cref="CameraProtocolProfile.HikvisionProtocolProfile"/>.
/// </summary>
public interface ICameraProtocolProfile
{
    /// <summary>
    /// <c>Camera.Manufacturer</c> ile eslesen ad (buyuk/kucuk harf duyarsiz).
    /// </summary>
    string Manufacturer { get; }

    /// <summary>
    /// Medya gecidine verilecek RTSP adresi.
    ///
    /// <b>Tarayiciya ASLA gitmez</b> — icinde kamera parolasi vardir. Yalnizca
    /// sunucudan MediaMTX'in loopback Control API'sine yazilir.
    /// </summary>
    string BuildRtspUrl(Camera camera, StreamProfile profile);

    /// <summary>
    /// Anlik goruntu ucunun YOL kismi (sema/host/port haric, bas taraftaki
    /// <c>/</c> dahil). Digest imzasi bu yolun uzerinden hesaplandigi icin
    /// tam URL degil yol donuyor.
    /// </summary>
    string BuildSnapshotPath(Camera camera);
}

/// <summary>
/// Kameranin ureticisine gore dogru profili secer.
/// </summary>
public interface ICameraProtocolProfileResolver
{
    ICameraProtocolProfile Resolve(Camera camera);
}
