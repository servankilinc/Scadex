using FluentValidation;
using Scadex.Core.Model;
using Scadex.Core.Utils.CriticalData;

namespace Scadex.Model.Dtos.Camera.Commands;

public class CameraCreateDto : IDto
{
    public Guid CabinetId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    public string IpAddress { get; set; } = null!;
    public int RtspPort { get; set; } = 554;
    public int HttpPort { get; set; } = 80;
    public int? HttpsPort { get; set; }

    public string? Username { get; set; }

    [CriticalData]
    public string? Password { get; set; }

    public int MainStreamChannel { get; set; } = 101;
    public int SubStreamChannel { get; set; } = 102;
    public bool MainStreamEnabled { get; set; } = true;
    public bool SubStreamEnabled { get; set; } = true;
    public int SnapshotChannel { get; set; } = 101;

    // --- monitoring ile ilgili alanlar (IpAddress hem monitoring için kullanılır hem de kamera ile haberleşmek için) ---
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

        RuleFor(x => x.IpAddress).NotEmpty().WithMessage("IP adresi girilmeli");
        RuleFor(x => x.IpAddress).MaximumLength(64).WithMessage("IP adresi en fazla 64 karakter olabilir");

        // Port araligi 1..65535 — 0 gecerli bir TCP portu degil.
        RuleFor(x => x.RtspPort).InclusiveBetween(1, 65535).WithMessage("RTSP portu 1-65535 arasında olmalı");
        RuleFor(x => x.HttpPort).InclusiveBetween(1, 65535).WithMessage("HTTP portu 1-65535 arasında olmalı");
        RuleFor(x => x.HttpsPort!.Value).InclusiveBetween(1, 65535).When(x => x.HttpsPort.HasValue).WithMessage("HTTPS portu 1-65535 arasında olmalı");
        RuleFor(x => x.MonitoringPort!.Value).InclusiveBetween(1, 65535).When(x => x.MonitoringPort.HasValue).WithMessage("İzleme portu 1-65535 arasında olmalı");

        RuleFor(x => x.MainStreamChannel).GreaterThan(0).WithMessage("Ana akım kanalı sıfırdan büyük olmalı");
        RuleFor(x => x.SubStreamChannel).GreaterThan(0).WithMessage("Tali akım kanalı sıfırdan büyük olmalı");
        RuleFor(x => x.SnapshotChannel).GreaterThan(0).WithMessage("Anlık görüntü kanalı sıfırdan büyük olmalı");

        // En az bir akim acik olmali; ikisi de kapaliysa kamera hic izlenemez
        RuleFor(x => x.MainStreamEnabled).Must((x, _) => x.MainStreamEnabled || x.SubStreamEnabled).WithMessage("Main stream ve Sub stream aynı anda kapatılamaz");

        // 5 sn'nin altinda bir yoklama araligi, kameraya faydasiz yuk bindirir
        RuleFor(x => x.PingIntervalSec).InclusiveBetween(5, 86400).WithMessage("Yoklama aralığı 5 saniye ile 24 saat(86400sn) arasında olmalı");

        RuleFor(x => x.Username).MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir");
        RuleFor(x => x.Manufacturer).NotEmpty().MaximumLength(512).WithMessage("Üretici bilgisi girilmeli ve en fazla 512 karakter olabilir");
        RuleFor(x => x.Model).MaximumLength(64).WithMessage("Model en fazla 64 karakter olabilir");
        RuleFor(x => x.Description).MaximumLength(512).WithMessage("Açıklama en fazla 512 karakter olabilir");
    }
}
