using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Monitoring.Commands;

/// <summary> (TCP connect ping) sonucu </summary>
public class MonitoredAssetProbeResultDto : IDto
{
    public bool Reachable { get; set; }

    /// <summary>Gidis-donus suresi (ms) — bilgi amaclidir, SAKLANMAZ.</summary>
    public int? RttMs { get; set; }

    /// <summary>Ulasilamadiysa sebep. Basarili yoklamada yok sayilir ve temizlenir.</summary>
    public string? Error { get; set; }
}

public class MonitoredAssetProbeResultDtoValidator : AbstractValidator<MonitoredAssetProbeResultDto>
{
    public MonitoredAssetProbeResultDtoValidator()
    {
        RuleFor(v => v.RttMs!.Value)
            .GreaterThanOrEqualTo(0)
            .When(v => v.RttMs.HasValue)
            .WithMessage("Gecikme ms bilgisi negatif olamaz");

        // Hata metni YALNIZCA ulasilamadiginda zorunludur.
        RuleFor(v => v.Error)
            .NotEmpty()
            .When(v => !v.Reachable)
            .WithMessage("Ulasilamayan varlik icin hata metni zorunludur");
    }
}
