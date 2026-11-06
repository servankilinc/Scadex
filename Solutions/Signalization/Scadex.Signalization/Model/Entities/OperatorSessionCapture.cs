using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary> Dış kapı açılışında çekilen kamera kaydının işlem oturumuyla ilişikisi. Dosya Scadex çekirdeğindeki <c>CameraCapture</c>'da </summary>
public class OperatorSessionCapture : IEntity
{
    public long SessionId { get; set; }

    /// <summary> Cekirdekteki <c>CameraCapture.Id</c> </summary>
    public long CameraCaptureId { get; set; }

    /// <summary> 1..N — çekim sırası. </summary>
    public int Sequence { get; set; }

    #region *** EF Core Navigation ***
    public virtual OperatorSession? Session { get; set; }
    #endregion
}
