using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Dis kapi acilisinda cekilen karenin oturumla bagi. Dosyanin kendisi cekirdekteki <c>CameraCapture</c>'dadir ve
/// global saklama suresine (<c>CaptureRetentionDays</c>) tabidir: dosya silinse de bu bag ve cekim satiri kalir.
/// </summary>
public class OperatorSessionCapture : IEntity
{
    public long SessionId { get; set; }

    /// <summary> Cekirdekteki <c>CameraCapture.Id</c> — FK DEGIL. </summary>
    public long CameraCaptureId { get; set; }

    /// <summary> 1..N — cekim sirasi. </summary>
    public int Sequence { get; set; }

    #region *** EF Core Navigation ***
    public virtual OperatorSession? Session { get; set; }
    #endregion
}
