using Scadex.Core.Model;
using Scadex.Core.Utils.CriticalData;
using FluentValidation;

namespace Scadex.Model.Dtos.Camera.Commands;

/// <summary>
/// Kamera guncelleme. Sozlesme: <c>docs/api-contract/11-camera.md</c>
///
/// <b><c>CabinetId</c> YOKTUR ve degistirilemez.</b> Kamera fiziksel olarak bir
/// kabinin icindedir; kabin degistirmek "ayni kamera" degil "baska bir kurulum"
/// demektir ve gecmis cekimlerini (<c>CameraCapture</c>) yanlis kabine baglardi.
/// Tasima gerekirse eski kayit pasife alinip yenisi acilir.
/// </summary>
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

    /// <summary>
    /// Yeni parola.
    ///
    /// <b><c>null</c> = "dokunma"</b>, mevcut parola korunur. Bos string ise
    /// parola SILINIR. Bu ayrim sart: okuma DTO'su parolayi hic dondurmedigi
    /// icin arayuz formu doldururken alani bos birakir; <c>null</c>'i "sil"
    /// saymak, her duzenlemede parolayi sessizce ucururdu.
    /// </summary>
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

    /// <summary>
    /// Pasife almak icin <c>false</c>. Ayri bir DELETE ucu YOKTUR — kod tabaninin
    /// B5'te aldigi kararla ayni: <c>Camera</c> <c>IActivatableEntity</c>'dir,
    /// fiziksel silme interceptor'da exception atar.
    /// </summary>
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

        // En az bir akim acik olmali; ikisi de kapaliysa kamera hic izlenemez ve
        // arayuz sebebini gosteremez.
        RuleFor(x => x.MainStreamEnabled).Must((x, _) => x.MainStreamEnabled || x.SubStreamEnabled).WithMessage("Ana akım ve tali akım aynı anda kapatılamaz");

        // 5 sn'nin altinda bir yoklama araligi, kameraya faydasiz yuk bindirir;
        // 24 saatin ustunde ise "izleniyor" demek anlamsizlasir.
        RuleFor(x => x.PingIntervalSec).InclusiveBetween(5, 86400).WithMessage("Yoklama aralığı 5 saniye ile 24 saat arasında olmalı");

        RuleFor(x => x.Username).MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir");
        RuleFor(x => x.Manufacturer).MaximumLength(64).WithMessage("Üretici en fazla 64 karakter olabilir");
        RuleFor(x => x.Model).MaximumLength(64).WithMessage("Model en fazla 64 karakter olabilir");
        RuleFor(x => x.Description).MaximumLength(512).WithMessage("Açıklama en fazla 512 karakter olabilir");
    }
}
