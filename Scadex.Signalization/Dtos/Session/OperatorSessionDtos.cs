using FluentValidation;
using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;
using static Scadex.Signalization.Enums.SignalEnums;

namespace Scadex.Signalization.Dtos.Session.Queries
{
    /// <summary> Sayfali oturum listesi filtresi. Tarih araligi <c>StartedAtUtc</c>'ye gore uygulanir. </summary>
    public class OperatorSessionQueryRequest : IDto
    {
        public Guid? CabinetId { get; set; }
        public Guid? OuterDoorId { get; set; }
        public Guid? UserId { get; set; }

        /// <summary> Kurum filtresi; oturumdaki operatorun o gunku kurum ENSTANTANESINE (ada) gore uygulanir. </summary>
        public Guid? AuthorityId { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public OperatorSessionStatus? Status { get; set; }

        /// <summary> Verilen bayraklardan EN AZ BIRINI tasiyan oturumlar. </summary>
        public SessionFlags? Flags { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class OperatorSessionQueryRequestValidator : AbstractValidator<OperatorSessionQueryRequest>
    {
        public OperatorSessionQueryRequestValidator()
        {
            RuleFor(v => v.Page).GreaterThanOrEqualTo(1).WithMessage("Sayfa 1'den küçük olamaz");
            RuleFor(v => v.PageSize).InclusiveBetween(1, 200).WithMessage("Sayfa boyutu 1-200 arasında olmalı");
            RuleFor(v => v.ToUtc).GreaterThanOrEqualTo(v => v.FromUtc).When(v => v.FromUtc != null && v.ToUtc != null).WithMessage("Bitiş tarihi başlangıçtan önce olamaz");
        }
    }

    public class OperatorSessionSummaryRequest : IDto
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public Guid? CabinetId { get; set; }
    }

    public class OperatorSessionSummaryRequestValidator : AbstractValidator<OperatorSessionSummaryRequest>
    {
        public OperatorSessionSummaryRequestValidator()
        {
            RuleFor(v => v.FromUtc).NotEmpty().WithMessage("Başlangıç tarihi zorunlu");
            RuleFor(v => v.ToUtc).NotEmpty().WithMessage("Bitiş tarihi zorunlu");
            RuleFor(v => v.ToUtc).GreaterThan(v => v.FromUtc).WithMessage("Bitiş tarihi başlangıçtan sonra olmalı");
            RuleFor(v => v).Must(v => (v.ToUtc - v.FromUtc).TotalDays <= 366).WithMessage("Özet en fazla 366 günlük aralık için alınabilir");
        }
    }

    public class OperatorSessionOperatorDto : IDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string AuthorityName { get; set; } = null!;
        public string CardIdRaw { get; set; } = null!;
        public DateTime FirstCardAtUtc { get; set; }
        public DateTime LastCardAtUtc { get; set; }
    }

    public class OperatorSessionListItemDto : IDto
    {
        public long Id { get; set; }
        public Guid CabinetId { get; set; }
        public string? CabinetName { get; set; }
        public Guid OuterDoorId { get; set; }
        public string OuterDoorName { get; set; } = null!;
        public OperatorSessionStatus Status { get; set; }
        public SessionFlags Flags { get; set; }

        /// <summary> <c>Z</c> soneki YOK (<c>datetime2</c>) — istemci <c>toUtcDate</c> ile okumali. </summary>
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndedAtUtc { get; set; }
        public int? DurationSec { get; set; }
        public int CaptureCount { get; set; }

        /// <summary> Oturumda guvenlik uyarisi bayragi var mi (<c>UnauthorizedEntry</c> / <c>ForcedOpen</c>). Onay akisi yoktur; bayrak kalicidir. </summary>
        public bool HasAlert { get; set; }
        public List<OperatorSessionOperatorDto> Operators { get; set; } = [];
    }

    /// <summary> Canli panelin satiri: yalnizca ACIK (dis kapisi kapanmamis) oturumlar. </summary>
    public class OperatorSessionOpenDto : OperatorSessionListItemDto
    {
        /// <summary> Saklanmaz, kapi ve operator satirlarindan turetilir. </summary>
        public SessionPhase Phase { get; set; }
        public int ElapsedSec { get; set; }
        public bool SirenRequested { get; set; }
        public bool CabinetSirenIsOn { get; set; }
    }

    public class OperatorSessionDetailDto : OperatorSessionListItemDto
    {
        public DateTime? SirenRequestedAtUtc { get; set; }
        public DateTime? SirenReleasedAtUtc { get; set; }
        public List<OperatorSessionEventDto> Events { get; set; } = [];
        public List<OperatorSessionDoorDto> Doors { get; set; } = [];
        public List<OperatorSessionCaptureDto> Captures { get; set; } = [];
    }

    public class OperatorSessionEventDto : IDto
    {
        public long Id { get; set; }
        public SessionEventType Type { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public DateTime ReceivedAtUtc { get; set; }
        public Guid? InnerDoorId { get; set; }
        public string? InnerDoorName { get; set; }
        public Guid? UserId { get; set; }
        public string? UserFullName { get; set; }
        public string? CardIdRaw { get; set; }
        public Guid? DeviceCommandId { get; set; }
        public long? CameraCaptureId { get; set; }
        public string? Detail { get; set; }
    }

    /// <summary> Ic kapinin oturumdaki ozeti — olaylardan turetilir (kapi basina kolon tutulmaz). </summary>
    public class OperatorSessionDoorDto : IDto
    {
        public Guid InnerDoorId { get; set; }
        public string Name { get; set; } = null!;
        public string? AuthorityName { get; set; }
        public DateTime? FirstUnlockedAtUtc { get; set; }
        public DateTime? FirstOpenedAtUtc { get; set; }
        public DateTime? LastClosedAtUtc { get; set; }
        public DateTime? LastLockedAtUtc { get; set; }
        public int OpenCount { get; set; }
        public bool WasForcedOpen { get; set; }
    }

    public class OperatorSessionCaptureDto : IDto
    {
        public long CameraCaptureId { get; set; }
        public int Sequence { get; set; }
        public CaptureStatus? Status { get; set; }
        public DateTime? CapturedAtUtc { get; set; }

        /// <summary> <c>wwwroot</c> altindaki yol; saklama suresi dolan cekimde <c>null</c> (satir kalir, dosya gider). </summary>
        public string? RelativePath { get; set; }
        public string? FailureReason { get; set; }
    }

    public class OperatorSessionSummaryDto : IDto
    {
        public int SessionCount { get; set; }
        public int TotalDurationSec { get; set; }
        public int WarningCount { get; set; }

        /// <summary> Operator basina: oturum suresi oturumdaki HER operatore sayilir (ayni anda iki operator calisabilir). </summary>
        public List<OperatorSessionSummaryRowDto> ByOperator { get; set; } = [];
        public List<OperatorSessionSummaryRowDto> ByAuthority { get; set; } = [];
        public List<OperatorSessionSummaryRowDto> ByCabinet { get; set; } = [];
    }

    public class OperatorSessionSummaryRowDto : IDto
    {
        public string Key { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int SessionCount { get; set; }
        public int TotalDurationSec { get; set; }
        public int AverageDurationSec { get; set; }
        public int WarningCount { get; set; }
    }
}
