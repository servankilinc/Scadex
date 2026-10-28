using System.Globalization;
using Scadex.Model.Enums;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary>  Kartin nokta dili — cerceve basligi ('I'/'A'/'O') ile bizim <see cref="EntityEnums.PinDirection"/>'imiz arasindaki ceviri. </summary>
public readonly record struct ScadaPinAddress(EntityEnums.PinDirection Direction, int ChannelNumber)
{
    /// <summary>
    /// <c>"I"</c> / <c>"O"</c> / <c>"A"</c> Buyuk/kucuk harf duyarsiz.
    /// LED icin AYRI bir on ek YOK  (role 1-16, LED 17-24) ve ikisine de ayni 'O' basligini yaziyor.
    /// </summary>
    public static bool CheckAndParseIngestPin(string? type, out EntityEnums.PinDirection direction)
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

    /// <summary>
    /// Okuma ucunun (<c>/status</c>) tipi: <c>"I"</c> / <c>"A"</c> ingest'teki gibi, ayrica <c>"O"</c> cikis
    /// (role ve LED ayni baslikla). Buyuk/kucuk harf duyarsiz.
    /// </summary>
    public static bool TryParseStatusType(string? type, out EntityEnums.PinDirection direction)
    {
        if (CheckAndParseIngestPin(type, out direction))
            return true;

        if (string.Equals(type?.Trim(), "O", StringComparison.OrdinalIgnoreCase))
        {
            direction = EntityEnums.PinDirection.Output;
            return true;
        }
        return false;
    }

    /// <summary>Kanal referansindan metinsel adres (<c>IN3</c>, <c>OUT1</c>) — ekran ve gunluk icin; tele cikmaz.</summary>
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
