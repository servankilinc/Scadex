using FluentValidation;
using Scadex.Core.Model;

namespace Scadex.Signalization.Dtos.Config.Commands;

/// <summary>
/// Kabin yapilandirmasinin TAM agaci: kabin → dis kapilar → ic kapilar. Kucuk bir aggregate oldugu icin delta degil
/// tam agac gonderilir; Guid'i istemci uretir, sunucu kimlige gore upsert eder, gövdede olmayan kapiyi pasife alir
/// (fiziksel silme yok — oturum olaylari kapi kimligini gosterir). Siren kabin duzeyindedir.
/// </summary>
public class SignalCabinetSaveRequest : IDto
{
    public bool IsEnabled { get; set; }
    public Guid? SirenIoChannelId { get; set; }
    public int SirenDurationSec { get; set; } = 120;
    public int EntrySnapshotCount { get; set; } = 5;
    public int EntrySnapshotIntervalMs { get; set; } = 1000;
    public int AwaitingCardTimeoutSec { get; set; } = 120;
    public int SessionMaxDurationMin { get; set; } = 240;
    public List<SignalOuterDoorDraft> OuterDoors { get; set; } = [];
}

public class SignalOuterDoorDraft : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid SwitchIoChannelId { get; set; }
    public string SwitchOpenValue { get; set; } = "1";
    public Guid? CameraId { get; set; }
    public List<SignalInnerDoorDraft> InnerDoors { get; set; } = [];
}

public class SignalInnerDoorDraft : IDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid AuthorityId { get; set; }
    public Guid SwitchIoChannelId { get; set; }
    public string SwitchOpenValue { get; set; } = "1";
    public Guid LockIoChannelId { get; set; }
    public bool UnlockTurnsOn { get; set; } = true;
}

public class SignalCabinetSaveRequestValidator : AbstractValidator<SignalCabinetSaveRequest>
{
    public SignalCabinetSaveRequestValidator()
    {
        RuleFor(v => v.SirenDurationSec).InclusiveBetween(1, 3600).WithMessage("Siren süresi 1-3600 saniye arasında olmalı");
        RuleFor(v => v.EntrySnapshotCount).InclusiveBetween(0, 10).WithMessage("Kare sayısı 0-10 arasında olmalı");
        RuleFor(v => v.EntrySnapshotIntervalMs).InclusiveBetween(200, 10000).WithMessage("Kare aralığı 200-10000 ms arasında olmalı");
        RuleFor(v => v.AwaitingCardTimeoutSec).InclusiveBetween(0, 3600).WithMessage("Kart bekleme süresi 0-3600 saniye arasında olmalı (0 = kapalı)");
        RuleFor(v => v.SessionMaxDurationMin).InclusiveBetween(1, 1440).WithMessage("Oturum süresi 1-1440 dakika arasında olmalı");

        RuleForEach(v => v.OuterDoors).SetValidator(new SignalOuterDoorDraftValidator());

        RuleFor(v => v.OuterDoors)
            .Must(doors => doors.Select(d => d.Id).Distinct().Count() == doors.Count)
            .WithMessage("Aynı dış kapı gönderide birden fazla kez var");

        RuleFor(v => v.OuterDoors)
            .Must(doors =>
            {
                var innerIds = doors.SelectMany(d => d.InnerDoors).Select(i => i.Id).ToList();
                return innerIds.Distinct().Count() == innerIds.Count;
            })
            .WithMessage("Aynı iç kapı gönderide birden fazla kez var");
    }
}

public class SignalOuterDoorDraftValidator : AbstractValidator<SignalOuterDoorDraft>
{
    public SignalOuterDoorDraftValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("Dış kapı kimliği zorunlu");
        RuleFor(v => v.Name).NotEmpty().WithMessage("Dış kapı adı zorunlu");
        RuleFor(v => v.Name).MaximumLength(128).WithMessage("Dış kapı adı en fazla 128 karakter olabilir");
        RuleFor(v => v.SwitchIoChannelId).NotEqual(Guid.Empty).WithMessage("Dış kapı anahtar kanalı seçilmeli");
        RuleFor(v => v.SwitchOpenValue).NotEmpty().MaximumLength(16).WithMessage("Anahtarın 'açık' değeri zorunlu (en fazla 16 karakter)");
        RuleFor(v => v.InnerDoors).NotEmpty().WithMessage("Her dış kapının en az bir iç kapısı olmalı");
        RuleForEach(v => v.InnerDoors).SetValidator(new SignalInnerDoorDraftValidator());
    }
}

public class SignalInnerDoorDraftValidator : AbstractValidator<SignalInnerDoorDraft>
{
    public SignalInnerDoorDraftValidator()
    {
        RuleFor(v => v.Id).NotEqual(Guid.Empty).WithMessage("İç kapı kimliği zorunlu");
        RuleFor(v => v.Name).NotEmpty().WithMessage("İç kapı adı zorunlu");
        RuleFor(v => v.Name).MaximumLength(128).WithMessage("İç kapı adı en fazla 128 karakter olabilir");
        RuleFor(v => v.AuthorityId).NotEqual(Guid.Empty).WithMessage("Kurum seçilmeli");
        RuleFor(v => v.SwitchIoChannelId).NotEqual(Guid.Empty).WithMessage("İç kapı anahtar kanalı seçilmeli");
        RuleFor(v => v.SwitchOpenValue).NotEmpty().MaximumLength(16).WithMessage("Anahtarın 'açık' değeri zorunlu (en fazla 16 karakter)");
        RuleFor(v => v.LockIoChannelId).NotEqual(Guid.Empty).WithMessage("Kilit kanalı seçilmeli");
    }
}
