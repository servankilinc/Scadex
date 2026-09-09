using System.Globalization;
using Scadex.Model.Enums;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary>  Kartin nokta dili — cerceve basligi ('I'/'A'/'O') ile bizim <see cref="EntityEnums.PinDirection"/>'imiz arasindaki ceviri. </summary>
public readonly record struct ScadaPinAddress(EntityEnums.PinDirection Direction, int ChannelNumber)
{
    private const int MaxDigits = 4;

    /// <summary>
    /// <c>"IN"</c> / <c>"OUT"</c> / <c>"A"</c> analog girison, eki -> yon. Buyuk/kucuk harf duyarsiz.
    /// LED icin AYRI bir on ek YOK  (role 1-16, LED 17-24) ve ikisine de ayni 'O' basligini yaziyor. LED = <c>OUT17..OUT24</c>.
    /// </summary>
    private static readonly (string Prefix, EntityEnums.PinDirection Direction)[] Prefixes =
    [
        ("OUT", EntityEnums.PinDirection.Output),
        ("IN", EntityEnums.PinDirection.Input)
    ];

    public static bool TryParseType(string? type, out EntityEnums.PinDirection direction)
    {
        direction = default;
        if (string.IsNullOrWhiteSpace(type)) return false;

        switch (type.Trim().ToUpperInvariant())
        {
            case "I":
                direction = EntityEnums.PinDirection.Input;
                return true;
            case "A":
                direction = EntityEnums.PinDirection.AnalogInput;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Kanal referansindan metinsel adres — giden komut govdesi icin.</summary>
    public static string Format(EntityEnums.PinDirection direction, int channelNumber) =>
        direction switch
        {
            EntityEnums.PinDirection.Input => $"IN{channelNumber}",
            EntityEnums.PinDirection.AnalogInput => $"AI{channelNumber}",
            EntityEnums.PinDirection.Output => $"OUT{channelNumber}",
            // Bidirectional'in kart karsiligi yok: kart her noktayi ya giris ya
            // cikis olarak adresler. komut yolu bu kanali cikis olarak surer, adres de oyle yazilir.
            _ => $"OUT{channelNumber}"
        };
}
