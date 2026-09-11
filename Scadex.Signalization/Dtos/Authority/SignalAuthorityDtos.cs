using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Signalization.Dtos.Authority.Queries
{
    public class SignalAuthorityDto : IDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public Guid RoleId { get; set; }
        public string? RoleName { get; set; }

        /// <summary> Pasif rol yetki turetmez: kurum aktif olsa da rolu pasifse kimse bu kurumla kapi acamaz. </summary>
        public bool RoleIsActive { get; set; }
        public bool IsActive { get; set; }
    }
}

namespace Scadex.Signalization.Dtos.Authority.Commands
{
    /// <summary> Kurum listesinin TAM hali; gövdede olmayan kurum pasife alinir. </summary>
    public class SignalAuthoritySaveRequest : IDto
    {
        public List<SignalAuthorityDraft> Authorities { get; set; } = [];
    }

    public class SignalAuthorityDraft : IDto
    {
        /// <summary> Istemci uretir. </summary>
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public Guid RoleId { get; set; }
    }

    public class SignalAuthoritySaveRequestValidator : AbstractValidator<SignalAuthoritySaveRequest>
    {
        public SignalAuthoritySaveRequestValidator()
        {
            RuleForEach(v => v.Authorities).ChildRules(a =>
            {
                a.RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Kurum kimliği zorunlu");
                a.RuleFor(v => v.Name).NotEmpty().WithMessage("Kurum adı zorunlu");
                a.RuleFor(v => v.Name).MaximumLength(64).WithMessage("Kurum adı en fazla 64 karakter olabilir");
                a.RuleFor(v => v.RoleId).NotEqual(Guid.Empty).WithMessage("Rol seçilmeli");
            });

            RuleFor(v => v.Authorities)
                .Must(list => list.Select(a => a.Id).Distinct().Count() == list.Count)
                .WithMessage("Aynı kurum gönderide birden fazla kez var");
            RuleFor(v => v.Authorities)
                .Must(list => list.Select(a => a.RoleId).Distinct().Count() == list.Count)
                .WithMessage("Bir rol yalnızca bir kuruma bağlanabilir");
            RuleFor(v => v.Authorities)
                .Must(list => list.Select(a => a.Name?.Trim().ToUpperInvariant()).Distinct().Count() == list.Count)
                .WithMessage("Aynı adda iki kurum olamaz");
        }
    }
}
