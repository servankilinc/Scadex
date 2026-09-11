using Scadex.Core.Model;

namespace Scadex.Signalization.Entities;

/// <summary>
/// Oturumda kart okutan yetkili operator. Bir oturumda birden fazla operator olabilir (ayni dis kapinin ardinda
/// farkli kurumlarin ic kapilari). Ad, kurum ve kart bilincli ENSTANTANEDIR: kart devredilse ya da kullanici
/// yeniden adlandirilsa da rapor O GUN kimin girdigini gostermelidir.
/// </summary>
public class OperatorSessionOperator : IEntity
{
    public long SessionId { get; set; }

    /// <summary> Cekirdekteki <c>User.Id</c> — FK DEGIL. </summary>
    public Guid UserId { get; set; }
    public string FullNameSnapshot { get; set; } = null!;
    public string AuthorityNameSnapshot { get; set; } = null!;
    public string CardIdRaw { get; set; } = null!;
    public DateTime FirstCardAtUtc { get; set; }
    public DateTime LastCardAtUtc { get; set; }

    #region *** EF Core Navigation ***
    public virtual OperatorSession? Session { get; set; }
    #endregion
}
