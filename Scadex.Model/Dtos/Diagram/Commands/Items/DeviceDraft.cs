using FluentValidation;
using Scadex.Core.Model;
using Scadex.Model.Dtos.Diagram.Commands.Abstract;

namespace Scadex.Model.Dtos.Diagram.Commands.Items;

public class DeviceDraft : IDto, IIdentifiableDraft
{
    public Guid Id { get; set; }
    public Guid ComponentTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; } = true;
    public string? ExternalCode { get; set; }

    /// <summary> Olusacak pinlerin KIMLIKLERI — yalnizca OLUSTURMADA doldurulur. </summary>
    public List<DevicePinDraft> Pins { get; set; } = [];

    /// <summary> Olusacak IO kanallarinin KIMLIKLERI — yalnizca OLUSTURMADA. </summary>
    public List<DeviceIoChannelDraft> IoChannels { get; set; } = [];
}

public class DeviceDraftValidator : AbstractValidator<DeviceDraft>
{
    public DeviceDraftValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Cihaz kimligi zorunlu");
        RuleFor(v => v.ComponentTemplateId).NotEqual(Guid.Empty).WithMessage("Sablon secilmeli");
        RuleFor(v => v.Name).NotEmpty().WithMessage("Cihaz adi zorunlu");
        RuleFor(v => v.Name).MaximumLength(128).WithMessage("Cihaz adi en fazla 128 karakter olabilir");
        RuleFor(v => v.ExternalCode).MaximumLength(64).WithMessage("Dis kod en fazla 64 karakter olabilir");

        RuleForEach(v => v.Pins).SetValidator(new DevicePinDraftValidator());
        RuleForEach(v => v.IoChannels).SetValidator(new DeviceIoChannelDraftValidator());
    }
}



/// <summary> 
/// Ad, konum, fonksiyon, yon, gerilim vb. sunucuda <c>ComponentTemplatePin</c>'den kopyalanır;
/// istemciden gelen tek sey Guid ve o Guid'in hangi sablon pinine karsilik geldigi.
/// </summary>
public class DevicePinDraft : IDto
{
    public Guid Id { get; set; }
    public Guid ComponentTemplatePinId { get; set; }
}

public class DevicePinDraftValidator : AbstractValidator<DevicePinDraft>
{
    public DevicePinDraftValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Pin kimligi zorunlu");
        RuleFor(v => v.ComponentTemplatePinId).NotEqual(Guid.Empty).WithMessage("Sablon pini zorunlu");
    }
}





public class DeviceIoChannelDraft : IDto
{
    public Guid Id { get; set; }
    public int ChannelNumber { get; set; }
}

public class DeviceIoChannelDraftValidator : AbstractValidator<DeviceIoChannelDraft>
{
    public DeviceIoChannelDraftValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Kanal kimligi zorunlu");
    }
}
