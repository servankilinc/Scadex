using Scadex.Core.Model;
using Scadex.Core.Utils.CriticalData;
using FluentValidation;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Camera.Commands;

public class CameraUpdateDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public CameraBrand Brand { get; set; }
    public string? Model { get; set; }

    public string IpAddress { get; set; } = null!;

    public string? Username { get; set; }

    /// <summary>null ise "dokunmaz", mevcut parola korunur. Bos string veya dolu ise parola güncellenir.</summary>
    [CriticalData]
    public string? Password { get; set; }

    /// <summary> null ise servis markanın RTSP portunu yazar. </summary>
    public int? MonitoringPort { get; set; }
    public int PingIntervalSec { get; set; }
    public bool IsMonitoringEnabled { get; set; }

    public bool IsActive { get; set; }
}

public class CameraUpdateDtoValidator : AbstractValidator<CameraUpdateDto>
{
    public CameraUpdateDtoValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Geçersiz kamera bilgisi");
        RuleFor(v => v.Name).NotEmpty().WithMessage("İsim bilgisi girilmeli");
        RuleFor(v => v.Name).MaximumLength(150).WithMessage("İsim en fazla 150 karakter olabilir");

        // JSON'dan gelen tanimsiz bir sayi enum'a sorunsuz deserialize edilir; profili olmayan marka burada durdurulur.
        RuleFor(x => x.Brand).IsInEnum().WithMessage("Kamera markası seçilmeli");

        RuleFor(x => x.IpAddress).NotEmpty().WithMessage("IP adresi girilmeli");
        RuleFor(x => x.IpAddress).MaximumLength(64).WithMessage("IP adresi en fazla 64 karakter olabilir");

        // Port araligi 1..65535 — 0 gecerli bir TCP portu degil.
        RuleFor(x => x.MonitoringPort!.Value).InclusiveBetween(1, 65535).When(x => x.MonitoringPort.HasValue).WithMessage("İzleme portu 1-65535 arasında olmalı");

        // 5 sn'nin altinda bir yoklama araligi, kameraya faydasiz yuk bindirir;
        RuleFor(x => x.PingIntervalSec).InclusiveBetween(5, 86400).WithMessage("Yoklama aralığı 5 saniye ile 24 saat(86400sn) arasında olmalı");

        RuleFor(x => x.Username).MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir");
        RuleFor(x => x.Model).MaximumLength(64).WithMessage("Model en fazla 64 karakter olabilir");
        RuleFor(x => x.Description).MaximumLength(512).WithMessage("Açıklama en fazla 512 karakter olabilir");
    }
}
