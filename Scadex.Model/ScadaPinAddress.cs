using Scadex.Model.Enums;

namespace Scadex.Model;

/// <summary>  Kartin nokta dili — cerceve basligi ('I'/'A'/'O') ile bizim <see cref="EntityEnums.PinDirection"/>'imiz arasindaki ceviri. </summary>
public readonly record struct ScadaPinAddress(EntityEnums.PinDirection Direction, int ChannelNumber)
{
    public static bool CheckAndParsePin(string? type, out EntityEnums.PinDirection direction)
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
            case "O":
                direction = EntityEnums.PinDirection.Output;
                return true;
            default:
                return false;
        }
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
