using FluentValidation;
using Scadex.Core.Model;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Camera.Commands;

public class CameraCaptureCreateDto : IDto
{
    public CaptureType Type { get; set; } = CaptureType.Snapshot;
    public int? DurationSec { get; set; }
}

public class CameraCaptureCreateDtoValidator : AbstractValidator<CameraCaptureCreateDto>
{
    public CameraCaptureCreateDtoValidator()
    {
        RuleFor(v => v.Type).IsInEnum().WithMessage("Geçersiz çekim tipi");

        RuleFor(v => v.DurationSec).NotNull().WithMessage("Klip süresi belirtilmelidir").When(v => v.Type == CaptureType.Clip);

        // NOT: CameraCaptureSettings.MaxClipDurationSec ile ayni deger üzerinden bir ön kontrol yapılacak

        //RuleFor(v => v.DurationSec!.Value)
        //    .InclusiveBetween(1, AbsoluteMaxClipSeconds)
        //    .WithMessage($"Klip süresi 1-{AbsoluteMaxClipSeconds} saniye arasında olmalı")
        //    .When(v => v.Type == CaptureType.Clip && v.DurationSec.HasValue);
    }
}
