using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Entities;

/// <summary> Bir işlem oturumunda kart okutan yetkili operatörler. </summary>
public class OperatorSessionOperator : IEntity
{
    public long SessionId { get; set; }

    /// <summary> Scadex çekirdeğindeki <c>User.Id</c>. </summary>
    public Guid UserId { get; set; }

    public string FullNameSnapshot { get; set; } = null!;
    public string AuthorityNameSnapshot { get; set; } = null!;
    public string CardIdRaw { get; set; } = null!;

    /// <summary> Operatörün kartını okuttup işlem oturumuna dahil olduğu an </summary>
    public DateTime FirstCardAtUtc { get; set; }
    /// <summary> Operatörün kartını okuttup kabini terk ettiği an (o an ayrılması gerekbilir farklı bir operatör devam ediyor olabilir) </summary>
    public DateTime LastCardAtUtc { get; set; }

    #region *** EF Core Navigation ***
    public virtual OperatorSession? Session { get; set; }
    #endregion
}
