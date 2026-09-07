using System.Globalization;
using Scadex.Model.Enums;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> Kart uzerindeki bir noktanin metinsel adresi — <c>"IN1"</c>, <c>"OUT17"</c>. </summary>
public readonly record struct ScadaPinAddress(EntityEnums.PinDirection Direction, int ChannelNumber)
{
    private const int MaxDigits = 4;

    /// <summary>
    /// <c>"IN"</c> / <c>"OUT"</c> on eki -> yon. Buyuk/kucuk harf duyarsiz.
    /// LED icin AYRI bir on ek YOK  (role 1-16, LED 17-24) ve ikisine de ayni 'O' basligini yaziyor. LED = <c>OUT17..OUT24</c>.
    /// </summary>
    private static readonly (string Prefix, EntityEnums.PinDirection Direction)[] Prefixes =
    [
        ("OUT", EntityEnums.PinDirection.Output),
        ("IN", EntityEnums.PinDirection.Input)
    ];

    public static bool TryParse(string? value, out ScadaPinAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();

        foreach (var (prefix, direction) in Prefixes)
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var digits = text.AsSpan(prefix.Length);
            if (digits.Length is 0 or > MaxDigits) return false;

            foreach (var c in digits)
                if (!char.IsAsciiDigit(c)) return false;

            var number = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

            // Kartta 0 diye bir nokta yok; "IN0" bir yazim hatasidir, gecerli bir adres degil.
            if (number <= 0) return false;

            address = new ScadaPinAddress(direction, number);
            return true;
        }

        return false;
    }

    /// <summary>Kanal referansindan metinsel adres — giden komut govdesi icin.</summary>
    public static string Format(EntityEnums.PinDirection direction, int channelNumber) =>
        direction switch
        {
            EntityEnums.PinDirection.Output => $"OUT{channelNumber}",
            EntityEnums.PinDirection.Input => $"IN{channelNumber}",
            _ => $"OUT{channelNumber}"
        };

    public override string ToString() => Format(Direction, ChannelNumber);
}
