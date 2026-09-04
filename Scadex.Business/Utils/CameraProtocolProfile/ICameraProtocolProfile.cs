using Scadex.Model.Entities;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.CameraProtocolProfile;

/// <summary> <c>Camera.Manufacturer</c> kolonundan yola çıkarak ISAPI yolu ve RTSP yol bilgisi sağlar. </summary>
public interface ICameraProtocolProfile
{
    string Manufacturer { get; }

    /// <summary> Medya Gateway'e verilecek RTSP adresi. Client'a ASLA gitmez icinde kamera parolasi var. Yalnizca sunucudan Media Gateway'e iletilebilir. </summary>
    string BuildRtspUrl(Camera camera, StreamProfile profile);

    /// <summary> Anlik goruntu ucunun YOL kismi Digest imzasi bu yolun uzerinden hesaplandigi icin tam URL degil yol donuyor. </summary>
    string BuildSnapshotPath(Camera camera);
}
