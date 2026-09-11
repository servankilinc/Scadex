using Scadex.Core.Model;

namespace Scadex.Signalization.Dtos.Operator.Queries
{
    /// <summary> Operator = aktif kullanici. Kurum, sahip oldugu kurum rolunden turetilir (saklanmaz). </summary>
    public class SignalOperatorDto : IDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string? UserName { get; set; }
        public string? IdentityCardId { get; set; }
        public Guid? AuthorityId { get; set; }
        public string? AuthorityName { get; set; }

        /// <summary>
        /// Kullanicinin birden fazla kurum rolu var (/admin/users'tan elle atanmis). Bu durumda kartla kapi ACILMAZ
        /// (<c>MultipleAuthorities</c>); operator ekranindan tek kurum secilerek duzeltilir.
        /// </summary>
        public bool HasMultipleAuthorities { get; set; }
    }
}

namespace Scadex.Signalization.Dtos.Operator.Commands
{
    /// <summary> Kullanicinin tek kurumu; <c>null</c> = kurum rolunu kaldir. Diger (kurum disi) rolleri korunur. </summary>
    public class SignalOperatorAuthorityRequest : IDto
    {
        public Guid? AuthorityId { get; set; }
    }
}
