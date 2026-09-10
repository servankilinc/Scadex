using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Model.Dtos.CameraCaptureSetting.Commands;

public class CameraCaptureSettingUpdateDto : IDto
{
    public int SnapshotTimeoutMs { get; set; }
    public int SnapshotCacheSeconds { get; set; }
    public string CaptureRoot { get; set; } = null!;

    /// <summary>Çekimin saklanma süresi (gün). <c>0</c> = süresiz.</summary>
    public int CaptureRetentionDays { get; set; }

    public int MaxClipDurationSec { get; set; }
    public int ClipFinalizeGraceMs { get; set; }
}

public class CameraCaptureSettingUpdateDtoValidator : AbstractValidator<CameraCaptureSettingUpdateDto>
{
    public CameraCaptureSettingUpdateDtoValidator()
    {
        RuleFor(v => v.SnapshotTimeoutMs).InclusiveBetween(500, 60000).WithMessage("Anlık görüntü zaman aşımı 500-60.000 ms arasında olmalı");
        RuleFor(v => v.SnapshotCacheSeconds).InclusiveBetween(0, 300).WithMessage("Anlık görüntü önbelleği 0-300 saniye arasında olmalı");

        RuleFor(v => v.CaptureRoot).NotEmpty().WithMessage("Çekim kök dizini zorunlu");
        // kök, wwwroot ALTINDA relative bir yol olmak zorunda.
        // Kontrol KIRPILMIS deger uzerinde: "/uploads/captures/" gibi bir girdi mapping'te
        // zaten Trim('/') ediliyor, ama Windows'ta bastaki tek slash Path.IsPathRooted'i
        // true yapip gecerli bir yolu reddettiriyordu.
        RuleFor(v => v.CaptureRoot)
            .Must(v =>
            {
                string trimmed = v?.Trim().Trim('/') ?? string.Empty;
                return trimmed.Length > 0 && !Path.IsPathRooted(trimmed) && !trimmed.Contains("..");
            })
            .WithMessage("Çekim kök dizini wwwroot altında göreli bir yol olmalı");

        RuleFor(v => v.CaptureRetentionDays).InclusiveBetween(0, 3650).WithMessage("Saklama süresi 0-3650 gün arasında olmalı");
        RuleFor(v => v.MaxClipDurationSec).InclusiveBetween(1, 3600).WithMessage("Klip süresi üst sınırı 1-3600 saniye arasında olmalı");
        RuleFor(v => v.ClipFinalizeGraceMs).InclusiveBetween(0, 60000).WithMessage("Sonlandırma payı 0-60.000 ms arasında olmalı");
    }
}
