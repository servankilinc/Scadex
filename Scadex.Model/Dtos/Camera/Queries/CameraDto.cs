using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Camera.Queries;

/// <summary>
/// Kameranin okuma sekli.
///
/// <b>Parola bu DTO'da DUZ METIN olarak doner</b> (kullanici karari): sistem
/// kapali agda calisiyor ve bu asamada kamera kimlik bilgilerinin gizlenmesi
/// istenmiyor. Onceki tasarimda yerine <c>HasPassword: bool</c> gidiyordu.
///
/// <b>Sonucu acikca:</b> kamera parolasi API cevabinda ve tarayicida gorunur.
/// Uretime cikarken geri alinacaksa degisecek yer yalnizca bu alan ile
/// <c>CameraService.Projection</c>'dir.
///
/// RTSP URL'i yine YOKTUR ve olmayacaktir — tarayici kameraya dogrudan
/// baglanmaz. Canli izleme <c>POST /api/Camera/{id}/stream-ticket</c> ile alinan
/// kisa omurlu bir bilet uzerinden, medya gecidi araciligiyla yapilir.
///
/// Sozlesme: <c>docs/api-contract/11-camera.md</c>
/// </summary>
public class CameraDto : IDto
{
    public Guid Id { get; set; }
    public Guid CabinetId { get; set; }
    public string? CabinetName { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    public string IpAddress { get; set; } = null!;
    public int RtspPort { get; set; }
    public int HttpPort { get; set; }
    public int? HttpsPort { get; set; }

    public string? Username { get; set; }

    /// <summary>Kamera parolasi — duz metin, tanimsizsa null.</summary>
    public string? Password { get; set; }

    public int MainStreamChannel { get; set; }
    public int SubStreamChannel { get; set; }
    public bool MainStreamEnabled { get; set; }
    public bool SubStreamEnabled { get; set; }
    public int SnapshotChannel { get; set; }

    public int? MonitoringPort { get; set; }
    public int? DeviceStatusId { get; set; }

    /// <summary>Durum adi — hic yoklanmamis kamerada null.</summary>
    public string? DeviceStatusName { get; set; }
    public DateTime? LastSeen { get; set; }
    public int PingIntervalSec { get; set; }
    public bool IsMonitoringEnabled { get; set; }
    public string? LastConnectionError { get; set; }

    public bool IsActive { get; set; }
    public DateTime? CreateDateUtc { get; set; }
    public DateTime? UpdateDateUtc { get; set; }
}
