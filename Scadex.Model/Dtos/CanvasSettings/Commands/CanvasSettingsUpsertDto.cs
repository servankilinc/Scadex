using Scadex.Core.Model;
using FluentValidation;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.CanvasSettings.Commands;

public class CanvasSettingsUpsertDto : IDto
{
    public int GridSize { get; set; }
    public bool SnapToGrid { get; set; }
    public BackgroundVariant BackgroundVariant { get; set; }
    public string GridColor { get; set; } = null!;
    public string BackgroundColor { get; set; } = null!;
    public double MinZoom { get; set; }
    public double MaxZoom { get; set; }
}

public class CanvasSettingsUpsertDtoValidator : AbstractValidator<CanvasSettingsUpsertDto>
{
    public CanvasSettingsUpsertDtoValidator()
    {
        RuleFor(v => v.GridSize).InclusiveBetween(1, 500).WithMessage("Grid boyutu 1 ile 500 arasinda olmali");
        RuleFor(v => v.BackgroundVariant).IsInEnum().WithMessage("Gecersiz arka plan deseni");
        RuleFor(v => v.GridColor).NotEmpty().WithMessage("Grid rengi zorunlu");
        RuleFor(v => v.GridColor).MaximumLength(32).WithMessage("Grid rengi en fazla 32 karakter olabilir");
        RuleFor(v => v.BackgroundColor).NotEmpty().WithMessage("Arka plan rengi zorunlu");
        RuleFor(v => v.BackgroundColor).MaximumLength(32).WithMessage("Arka plan rengi en fazla 32 karakter olabilir");
        // MinZoom = 0 React Flow'da sonsuz uzaklasmaya izin verir ve canvas kaybolur.
        RuleFor(v => v.MinZoom).InclusiveBetween(0.05, 1).WithMessage("En kucuk yakinlastirma 0.05 ile 1 arasinda olmali");
        RuleFor(v => v.MaxZoom).InclusiveBetween(1, 10).WithMessage("En buyuk yakinlastirma 1 ile 10 arasinda olmali");
        RuleFor(v => v.MaxZoom).GreaterThan(v => v.MinZoom).WithMessage("En buyuk yakinlastirma, en kucukten buyuk olmali");
    }
}
