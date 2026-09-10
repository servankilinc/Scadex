using Scadex.Core.Model;
using Scadex.Model.Enums;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary>
/// Scadex'in karta GONDERDIGI komutun parametreleri — ingest'in ters yönü.
/// Kart komutu query string ile aldigi icin buradaki alanlarin YALNIZCA <see cref="ChannelNumber"/> ve
/// <see cref="Value"/> alanlari tele cikar; digerleri <c>DeviceCommand</c> satirinda kayit altinda kalir.
/// </summary>
public class ScadaCommandEnvelope : IDto
{
    /// <summary>Tele cikmaz: bir kabin = bir kart oldugu icin hedef zaten kartin adresinde ortuktur.</summary>
    public Guid CabinetId { get; set; }

    /// <summary>
    /// Tele cikmaz — kart bunu gormez, yalnizca kayit icindir.
    /// Araya TEKRAR TESPITI yapabilen bir SCADA katmani girerse tasinacak kimlik budur: biz kac kez
    /// gonderirsek gonderelim degismeyen bir kimlik olmadan tekrarlanan bir paketin roleyi iki kez
    /// surmesi engellenemez. Bugun boyle bir katman da, retry de yok.
    /// </summary>
    public Guid CommandId { get; set; }


    /// <summary>
    /// Hedef nokta numarasi — kartin <c>output=</c> parametresine ham olarak gider.
    /// Bu aşamada tek komut turu (<c>SetOutput</c>) old için her zaman bir kanali hedefler ve hep doludur.
    /// Adres uzayi duzdur: <c>1-16</c> role, <c>17-24</c> LED.
    /// </summary>
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
