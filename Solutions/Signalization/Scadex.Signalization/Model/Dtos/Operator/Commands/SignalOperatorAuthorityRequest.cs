using Scadex.Core.Model;

namespace Scadex.Signalization.Model.Dtos.Operator.Commands;

/// <summary> Kullanıcının kurumu; <c>null</c> = kurum rolunü kaldırır </summary>
public class SignalOperatorAuthorityRequest : IDto
{
    public Guid? AuthorityId { get; set; }
}