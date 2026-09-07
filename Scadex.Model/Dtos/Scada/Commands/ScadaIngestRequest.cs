using Scadex.Core.Model;
using Scadex.Model.Enums;
using FluentValidation;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> SCADA'nın HTTP uzerinden Bize push ettiği telemetri bilgisi. </summary>
public class ScadaIngestRequest : IDto
{
    public Guid CabinetId { get; set; }

    /// <summary> Geldigi nokta — <c>"IN1"</c>, <c>"IN7"</c>. </summary>
    public string Pin { get; set; } = null!;

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
        RuleFor(v => v.CabinetId).NotEmpty().WithMessage("cabinetId zorunlu");

        RuleFor(v => v.Pin).NotEmpty().WithMessage("pin zorunlu");

        RuleFor(v => v.Pin)
            .Must(pin => ScadaPinAddress.TryParse(pin, out _))
            .When(v => !string.IsNullOrWhiteSpace(v.Pin))
            .WithMessage("Gecersiz pin adresi. Beklenen bicim: IN1, IN2, ...");

        RuleFor(v => v.Pin)
            .Must(pin => !ScadaPinAddress.TryParse(pin, out var address) || address.Direction == EntityEnums.PinDirection.Input)
            .When(v => !string.IsNullOrWhiteSpace(v.Pin))
            .WithMessage("Out pininden telemetri kabul edilmiyor; yalnizca Input pinleri (IN...) veri gonderir");
    }
}
