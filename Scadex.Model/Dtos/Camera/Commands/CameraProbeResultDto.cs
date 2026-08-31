using Scadex.Core.Model;
using FluentValidation;

namespace Scadex.Model.Dtos.Camera.Commands;

/// <summary> Bir kamera yoklama(ayakta mı) sonucu.</summary>
public class CameraProbeResultDto : IDto
{
    /// <summary>Kameraya ulaşıldı mı?</summary>
    public bool Reachable { get; set; }

    /// <summary>Gidiş-dönüş süresi (ms) — bilgi amaçlı, saklanmaz. </summary>
    public int? RttMs { get; set; }

    /// <summary>Ulaşılamadıysa sebep. Başarılı yoklamada yok sayılır ve temizlenir.</summary>
    public string? Error { get; set; }
}

public class CameraProbeResultDtoValidator : AbstractValidator<CameraProbeResultDto>
{
    public CameraProbeResultDtoValidator()
    {
        RuleFor(v => v.RttMs!.Value).GreaterThanOrEqualTo(0).When(v => v.RttMs.HasValue)
            .WithMessage("Gecikme negatif olamaz");
        RuleFor(v => v.Error).MaximumLength(512).WithMessage("Hata metni en fazla 512 karakter olabilir");
    }
}
