using Scadex.Core.Model;
using Scadex.Model.Enums;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary> Scadex'in karta GONDERDIGI komutun parametreleri — ingest'in ters yönü. </summary>
public class ScadaCommandEnvelope : IDto
{
    /// <summary>Tele cikmaz: bir kabin = bir kart oldugu icin hedef zaten kartin adresinde ortuktur.</summary>
    public Guid CabinetId { get; set; }

    /// <summary>
    /// <see cref="CommandId"/>: SCADA tarafinda TEKRAR TESPITI icin tasinir yani biz bir retry mekanizması kurarsak ve scada içinde komut kontrolü varsa biz kaç kez 
    /// gönderirsek gönderelim sadece 1 kez çalıştırır. örenğin tekrarlanan bir paketin roleyi iki kez surmemesi SCADA'nin elindedir ve bunu ancak degismeyen bir kimlikle yapabilir.
    /// </summary>
    public Guid CommandId { get; set; }


    /// <summary> Hedef nokta numarasi — kartin <c>output=</c> parametresine ham olarak gider. Adresler örenğin: <c>1-16</c> role, <c>17-24</c> LED. </summary>
    public int ChannelNumber { get; set; }

    /// <summary>Tele cikmaz: tek komut turu (<c>SetOutput</c>) old icin kartin ayrica bilmesine gerek yok.</summary>
    public EntityEnums.DeviceCommandType CommandType { get; set; }

    /// <summary>Kartin <c>state=</c> parametresine giden deger. NO/NC cozumu ustte yapilmistir; burada tersleme YOKTUR.</summary>
    public string? Value { get; set; }

    /// <summary>Tele cikmaz. Komutun SUNUCUDA olustugu an.</summary>
    public DateTime IssuedAtUtc { get; set; }
}

#region RESULT AND DATA MODELS

/// <summary> SCADA komut yazma sonucu. </summary>
public readonly record struct ScadaCommandResponse(CommandStatus Status, string? Message);



/// <summary>
/// Gecmis kayıtların modeli. niyet ve taşınan data birlikte yazilir: yalnizca data saklansaydi, 
/// NC kabloli bir rolenin gecmisinde <c>"0"</c> goren biri bunun "kapat" mi yoksa "ac" mi oldugunu bir daha çıkaramazdı.
/// Pin'in fonksiyonu da gidiyor ki kablolama sonradan degisse bile o anki yorum sabit kalsın.
/// </summary>
public sealed record ScadaCommandPayload(bool TurnOn, string Value, PinFunction? Polarity); 
#endregion
