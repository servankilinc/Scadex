using FluentValidation;
using Scadex.Core.Model;
using Scadex.Model.Dtos.Diagram.Commands.Abstract;
using Scadex.Model.Enums;

namespace Scadex.Model.Dtos.Diagram.Commands.Items;

public class DeviceDraft : IDto, IIdentifiableDraft
{
    public Guid Id { get; set; }
    public Guid ComponentTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public double CoordinateX { get; set; }
    public double CoordinateY { get; set; }

    /// <summary> <c>null</c> "dokunma" DEGIL, "sablon  boyutuna don" demektir. </summary>
    public double? Width { get; set; }
    public double? Height { get; set; }

    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public bool IsLocked { get; set; }
    public bool IsVisible { get; set; } = true;
    public string? ExternalCode { get; set; }

    public string? MacAddress { get; set; }

    public string? IpAddress { get; set; }

    public int? MonitoringPort { get; set; }
    public int PingIntervalSec { get; set; } = 300;
    public bool IsMonitoringEnabled { get; set; }

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
   
        RuleFor(v => v.MacAddress).MaximumLength(17).WithMessage("MAC adresi en fazla 17 karakter olabilir");
        RuleFor(v => v.IpAddress).MaximumLength(45).WithMessage("IP adresi en fazla 45 karakter olabilir");

        RuleFor(v => v.MonitoringPort!.Value).InclusiveBetween(1, 65535).When(v => v.MonitoringPort.HasValue).WithMessage("İzleme portu 1-65535 arasında olmalı");
        RuleFor(v => v.PingIntervalSec).InclusiveBetween(5, 86400).WithMessage("Yoklama aralığı 5 saniye ile 24 saat(86400sn) arasında olmalı");
        // Izleme özelliği açık ama Ip eksikse worker her turda uyari loglar bu nedenle reddedilir
        RuleFor(v => v.IpAddress).NotEmpty().When(v => v.IsMonitoringEnabled).WithMessage("İzleme açıkken IP adresi zorunlu");
        RuleFor(v => v.MonitoringPort).NotNull().When(v => v.IsMonitoringEnabled).WithMessage("İzleme açıkken izleme portu zorunlu");

        RuleFor(v => v.Width).GreaterThan(0).When(v => v.Width.HasValue).WithMessage("Genislik sifirdan buyuk olmali");
        RuleFor(v => v.Height).GreaterThan(0).When(v => v.Height.HasValue).WithMessage("Yukseklik sifirdan buyuk olmali");

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
    /// <summary> Yalnizca KIMLIK eslemesi icin gonderilir; Kanalın yönü. benzersizliğin parçası cunku kartta <c>IN1</c> ile <c>OUT1</c> AYRI noktalardir. </summary>
    public EntityEnums.PinDirection Direction { get; set; }
    public int ChannelNumber { get; set; }
}

public class DeviceIoChannelDraftValidator : AbstractValidator<DeviceIoChannelDraft>
{
    public DeviceIoChannelDraftValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Kanal kimligi zorunlu");
        RuleFor(v => v.Direction).IsInEnum().WithMessage("Gecersiz kanal yonu");
    }
}
