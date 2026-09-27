using FluentValidation;
using Scadex.Core.Model;
using Scadex.Core.Utils.CriticalData;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Camera.Commands;

public class CameraCreateDto : IDto
{
    public Guid CabinetId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public CameraBrand Brand { get; set; }
    public string? Model { get; set; }

    public string IpAddress { get; set; } = null!;

    public string? Username { get; set; }

    [CriticalData]
    public string? Password { get; set; }

    // --- monitoring ile ilgili alanlar (IpAddress hem monitoring için kullanılır hem de kamera ile haberleşmek için) ---
    /// <summary> null ise servis markanın RTSP portunu yazar. </summary>
    public int? MonitoringPort { get; set; }
    public int PingIntervalSec { get; set; } = 300;
    public bool IsMonitoringEnabled { get; set; } = true;
}

public class CameraCreateDtoValidator : AbstractValidator<CameraCreateDto>
{
    public CameraCreateDtoValidator()
    {
        RuleFor(v => v.CabinetId).NotEqual(Guid.Empty).WithMessage("Kabin bilgisi zorunlu");
        RuleFor(v => v.Name).NotEmpty().WithMessage("İsim bilgisi girilmeli");
        RuleFor(v => v.Name).MaximumLength(150).WithMessage("İsim en fazla 150 karakter olabilir");

        // JSON'dan gelen tanimsiz bir sayi enum'a sorunsuz deserialize edilir; profili olmayan marka burada durdurulur.
        RuleFor(x => x.Brand).IsInEnum().WithMessage("Kamera markası seçilmeli");

        RuleFor(x => x.IpAddress).NotEmpty().WithMessage("IP adresi girilmeli");
        RuleFor(x => x.IpAddress).MaximumLength(64).WithMessage("IP adresi en fazla 64 karakter olabilir");

        // Port araligi 1..65535 — 0 gecerli bir TCP portu degil.
        RuleFor(x => x.MonitoringPort!.Value).InclusiveBetween(1, 65535).When(x => x.MonitoringPort.HasValue).WithMessage("İzleme portu 1-65535 arasında olmalı");

        // 5 sn'nin altinda bir yoklama araligi, kameraya faydasiz yuk bindirir
        RuleFor(x => x.PingIntervalSec).InclusiveBetween(5, 86400).WithMessage("Yoklama aralığı 5 saniye ile 24 saat(86400sn) arasında olmalı");

        RuleFor(x => x.Username).MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir");
        RuleFor(x => x.Model).MaximumLength(64).WithMessage("Model en fazla 64 karakter olabilir");
        RuleFor(x => x.Description).MaximumLength(512).WithMessage("Açıklama en fazla 512 karakter olabilir");
    }
}
