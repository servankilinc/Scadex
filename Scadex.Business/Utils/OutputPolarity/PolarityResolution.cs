using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Business.Utils.OutputPolarity;

/// <summary>
/// NC/NO çözümlemesinin sonucu. <see cref="Contact"/> <c>null</c> ise kanalın NO/NC pini yoktur (düz dijital çıkış):
/// mantıksal ve fiziksel değer aynıdır. true => "1" ve false => "0"
/// </summary>
public readonly record struct PolarityResolution(bool IsSuccess, PinFunction? Contact, string? Description)
{
    public static PolarityResolution Resolved(PinFunction? contact) => new(true, contact, null);
    public static PolarityResolution Reject(string description) => new(false, null, description);

    /// <summary> Mantıksal "aç" isteğinin karta gidecek fiziksel karşılığı (NC'de ters). </summary>
    public bool ToPhysical(bool turnOn) => Contact == PinFunction.NC ? !turnOn : turnOn;

    /// <summary> Karttaki fiziksel değerin mantıksal karşılığı ("yük açık mı"); dönüşüm simetriktir. </summary>
    public bool ToLogical(bool physicalOn) => ToPhysical(physicalOn);
}
