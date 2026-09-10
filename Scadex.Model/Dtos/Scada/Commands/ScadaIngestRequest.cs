using Scadex.Core.Model;
using Scadex.Model.Enums;
using FluentValidation;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> SCADA'nın HTTP uzerinden Bize push ettiği telemetri bilgisi. </summary>
public class ScadaIngestRequest : IDto
{
    public string MacAddress { get; set; } = null!;

    /// <summary> <c>"I"</c> dijital giris, <c>"A"</c> analog giris. </summary>
    public string Type { get; set; } = null!;

    public int ChannelNumber { get; set; }

    /// <summary>
    /// Deger STRING olarak tasinir ve string olarak saklanir (<c>IoChannel.CurrentValue</c>). 
    /// <c>null</c> gecerlidir ve "kanal var ama okunamadi" demektir; <c>"0"</c> ile ayni şey DEGILDIR.
    /// </summary>
    public string? Value { get; set; }

    /// <summary> Ölçümün SCADA tarafindaki zamani. Kritik bir bilgi değil bilgi amaclidir cunku SCADA'nin saati kaymis olabilir veya (opsiyonel old için göndermiyor olabailir scada). </summary>
    public DateTime? TimestampUtc { get; set; }
}


public class ScadaIngestRequestValidator : AbstractValidator<ScadaIngestRequest>
{
    public ScadaIngestRequestValidator()
    {
        RuleFor(v => v.MacAddress).NotEmpty().WithMessage("macAddress zorunlu");
        RuleFor(v => v.MacAddress).MaximumLength(64).WithMessage("macAddress en fazla 64 karakter olabilir");
        RuleFor(v => v.Type).NotEmpty().WithMessage("type zorunlu");

        RuleFor(v => v.Type)
            .Must(type => ScadaPinAddress.CheckAndParseIngestPin(type, out _))
            .When(v => !string.IsNullOrWhiteSpace(v.Type))
            .WithMessage("Gecersiz tip. Beklenen: \"I\" (dijital giris) veya \"A\" (analog giris)");
    }
}
