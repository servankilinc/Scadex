using FluentValidation;
using Scadex.Core.Model;
using Scadex.Model.Dtos.Diagram.Commands.Items;

namespace Scadex.Model.Dtos.Diagram.Commands;

/// <summary>
/// Özellikler panelindeki cihaz formunun Kaydet'i: <c>PUT /api/Diagram/cabinet/{cabinetId}/devices/{id}</c>.
/// Konum, boyut, z-sırası, kilit ve görünürlük burada DEĞİL, <see cref="DiagramLayoutRequest"/>'tedir.
/// Ek nesne (kamera / kontrol kartı / PC) şablon tipiyle eşleşmek zorundadır; eksik ya da yanlış tipe giden nesne 400'dür.
/// </summary>
public class DeviceUpdateRequest : IDto
{
    public string Name { get; set; } = null!;

    public string? IpAddress { get; set; }

    public int? MonitoringPort { get; set; }
    public int PingIntervalSec { get; set; } = 300;
    public bool IsMonitoringEnabled { get; set; }

    public CameraDraft? Camera { get; set; }
    public ControlCardDraft? ControlCard { get; set; }
    public PcDeviceDraft? Pc { get; set; }
}

public class DeviceUpdateRequestValidator : AbstractValidator<DeviceUpdateRequest>
{
    public DeviceUpdateRequestValidator()
    {
        RuleFor(v => v.Name).NotEmpty().WithMessage("Cihaz adi zorunlu");
        RuleFor(v => v.Name).MaximumLength(128).WithMessage("Cihaz adi en fazla 128 karakter olabilir");
        RuleFor(v => v.IpAddress).MaximumLength(45).WithMessage("IP adresi en fazla 45 karakter olabilir");

        RuleFor(v => v.MonitoringPort!.Value).InclusiveBetween(1, 65535).When(v => v.MonitoringPort.HasValue).WithMessage("İzleme portu 1-65535 arasında olmalı");
        RuleFor(v => v.PingIntervalSec).InclusiveBetween(5, 86400).WithMessage("Yoklama aralığı 5 saniye ile 24 saat(86400sn) arasında olmalı");
        // Izleme özelliği açık ama Ip eksikse worker her turda uyari loglar bu nedenle reddedilir
        RuleFor(v => v.IpAddress).NotEmpty().When(v => v.IsMonitoringEnabled).WithMessage("İzleme açıkken IP adresi zorunlu");
        // Port bilerek zorunlu DEGIL boşsa worker ICMP ping atar, doluysa o porta TCP connect.

        RuleFor(v => v.Camera!).SetValidator(new CameraDraftValidator()).When(v => v.Camera is not null);
        RuleFor(v => v.ControlCard!).SetValidator(new ControlCardDraftValidator()).When(v => v.ControlCard is not null);
        RuleFor(v => v.Pc!).SetValidator(new PcDeviceDraftValidator()).When(v => v.Pc is not null);
    }
}
