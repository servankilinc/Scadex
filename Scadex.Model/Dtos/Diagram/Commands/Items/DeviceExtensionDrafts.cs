using FluentValidation;
using Scadex.Core.Model;
using Scadex.Core.Utils;
using Scadex.Core.Utils.CriticalData;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Diagram.Commands.Items;

// Device ek tablolarinin taslaklari. Hangisinin gonderilecegini sablonun DeviceType'i belirler; eslesmeyen
// gonderi ya da eksik nesne DiagramService.SaveDeviceExtensions'ta 400'dur. Her kayitta EKSIKSIZ gonderilir (cihaz
// govdesi gibi) — tek istisna parola: null "dokunma", "" "temizle", deger "yaz" demektir; okuma DTO'su parolayi
// dondurmedigi icin istemci onu geri gonderemez.

/// <summary> <see cref="DeviceType.Camera"/> sablonlu cihazin ek alanlari. </summary>
public class CameraDraft : IDto
{
    public CameraBrand Brand { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string? Username { get; set; }

    /// <summary> <c>null</c> = mevcut parolaya dokunma, <c>""</c> = temizle, deger = yaz. </summary>
    [CriticalData]
    public string? Password { get; set; }
}

public class CameraDraftValidator : AbstractValidator<CameraDraft>
{
    public CameraDraftValidator()
    {
        // JSON'dan gelen tanimsiz bir sayi enum'a sorunsuz deserialize edilir; profili olmayan marka burada durdurulur.
        RuleFor(x => x.Brand).IsInEnum().WithMessage("Kamera markası seçilmeli");
        RuleFor(x => x.Model).MaximumLength(64).WithMessage("Model en fazla 64 karakter olabilir");
        RuleFor(x => x.Description).MaximumLength(512).WithMessage("Açıklama en fazla 512 karakter olabilir");
        RuleFor(x => x.Username).MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir");
        RuleFor(x => x.Password).MaximumLength(128).WithMessage("Parola en fazla 128 karakter olabilir");
    }
}

/// <summary> <see cref="DeviceType.ControlModule"/> sablonlu cihazin ek alanlari. </summary>
public class ControlCardDraft : IDto
{
    private string? _macAddress;

    /// <summary> Gelirken tek tipe çevrilir (<c>AA:BB:CC:DD:EE:FF</c>, <see cref="MacAddressFormat"/>). </summary>
    public string? MacAddress { get => _macAddress; set => _macAddress = MacAddressFormat.NormalizeOrKeep(value); }

    public string? Username { get; set; }

    /// <summary> <c>null</c> = mevcut parolaya dokunma, <c>""</c> = temizle, deger = yaz. </summary>
    [CriticalData]
    public string? Password { get; set; }
}

public class ControlCardDraftValidator : AbstractValidator<ControlCardDraft>
{
    public ControlCardDraftValidator()
    {
        RuleFor(v => v.MacAddress)
            .Must(mac => MacAddressFormat.TryNormalize(mac) is not null)
            .When(v => v.MacAddress is not null)
            .WithMessage("MAC adresi 12 harf/rakam olmalı, ör. AA:BB:CC:DD:EE:FF (ayraç ':' ya da '-' olabilir)");
        RuleFor(x => x.Username).MaximumLength(128).WithMessage("Kullanıcı adı en fazla 128 karakter olabilir");
        RuleFor(x => x.Password).MaximumLength(128).WithMessage("Parola en fazla 128 karakter olabilir");
    }
}

/// <summary> <see cref="DeviceType.Pc"/> sablonlu cihazin ek alanlari. </summary>
public class PcDeviceDraft : IDto
{
    private string? _macAddress;

    /// <summary> Gelirken tek tipe çevrilir (<c>AA:BB:CC:DD:EE:FF</c>, <see cref="MacAddressFormat"/>). </summary>
    public string? MacAddress { get => _macAddress; set => _macAddress = MacAddressFormat.NormalizeOrKeep(value); }
}

public class PcDeviceDraftValidator : AbstractValidator<PcDeviceDraft>
{
    public PcDeviceDraftValidator()
    {
        RuleFor(v => v.MacAddress)
            .Must(mac => MacAddressFormat.TryNormalize(mac) is not null)
            .When(v => v.MacAddress is not null)
            .WithMessage("MAC adresi 12 harf/rakam olmalı, ör. AA:BB:CC:DD:EE:FF (ayraç ':' ya da '-' olabilir)");
    }
}
