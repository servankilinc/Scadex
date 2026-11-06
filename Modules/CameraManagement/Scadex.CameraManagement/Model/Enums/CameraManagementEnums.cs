namespace Scadex.CameraManagement.Model.Enums;

/// <summary> Numaralar çekirdekteki eski karşılıklarıyla aynıdır (kolonlarda int olarak duruyor) — değiştirmeyin. </summary>
public static class CameraManagementEnums
{
    /// <summary> Yakalanan kamera kaydının cinsi. </summary>
    public enum CaptureType
    {
        /// <summary>Tek kare.</summary>
        Snapshot = 1,
        /// <summary>Kısa video klip.</summary>
        Clip = 2
    }

    public enum CaptureStatus
    {
        /// <summary>İstek alındı, dosya henüz depoda değil.</summary>
        Pending = 1,
        /// <summary>Dosya depoda, indirilebilir.</summary>
        Available = 2,
        /// <summary>Alınamadı..</summary>
        Failed = 3
    }
}
