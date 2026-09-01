using Scadex.Core.Model;
using Scadex.Core.Utils.CriticalData;
using FluentValidation;

namespace Scadex.Model.Dtos.Camera.Commands;

public class CameraUpdateDto : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }

    public string IpAddress { get; set; } = null!;
    public int RtspPort { get; set; }
    public int HttpPort { get; set; }
    public int? HttpsPort { get; set; }

    public string? Username { get; set; }

    /// <summary>null ise "dokunmaz", mevcut parola korunur. Bos string veya dolu ise parola güncellenir.</summary>
    [CriticalData]
    public string? Password { get; set; }

    public int MainStreamChannel { get; set; }
    public int SubStreamChannel { get; set; }
    public bool MainStreamEnabled { get; set; }
    public bool SubStreamEnabled { get; set; }
    public int SnapshotChannel { get; set; }

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

        // 5 sn'nin altinda bir yoklama araligi, kameraya faydasiz yuk bindirir;
        RuleFor(x => x.PingIntervalSec).InclusiveBetween(5, 86400).WithMessage("Yoklama aralığı 5 saniye ile 24 saat(86400sn) arasında olmalı");

        RuleFor(x => x.Username).MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir");
        RuleFor(x => x.Manufacturer).NotEmpty().MaximumLength(512).WithMessage("Üretici bilgisi girilmeli ve en fazla 512 karakter olabilir");
        RuleFor(x => x.Model).MaximumLength(64).WithMessage("Model en fazla 64 karakter olabilir");
        RuleFor(x => x.Description).MaximumLength(512).WithMessage("Açıklama en fazla 512 karakter olabilir");
    }
}
