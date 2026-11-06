using Scadex.Core.Model;
using System.Text.Json.Serialization;
using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Scada.Commands;

/// <summary> Scadex'in karta GONDERDIGI komutun parametreleri — ingest'in ters yönü. </summary>
public class ScadaCommandEnvelope : IDto
{
    /// <summary>Tele cikmaz: bir otomasyon = bir kart oldugu icin hedef zaten kartin adresinde ortuktur.</summary>
    public Guid AutomationId { get; set; }

    /// <summary> Hedef nokta numarasi — gövdedeki <c>channelNumber</c>'a ham olarak gider. Adresler örenğin: <c>1-16</c> role, <c>17-24</c> LED. </summary>
    public int ChannelNumber { get; set; }

    /// <summary>Gövdedeki <c>value</c>'ya giden deger (<c>"1"</c> / <c>"0"</c>). NO/NC cozumu ustte yapilmistir; burada tersleme YOKTUR.</summary>
    public string? Value { get; set; }

    /// <summary>Tele cikmaz. Komutun SUNUCUDA olustugu an.</summary>
    public DateTime IssuedAtUtc { get; set; }
}

/// <summary>
/// Karta giden çıkış komutunun gövdesi: <c>POST {ScadaBaseUrl}/command</c>.
/// </summary>
/// <param name="ChannelNumber"> Çıkış numarası (röle 1–16, LED 17–24). </param>
/// <param name="Value"> <c>"1"</c> aç, <c>"0"</c> kapat (fiziksel değer; NO/NC çözümü üstte yapılmıştır). </param>
/// <param name="Delay">
/// Saniye. Verilirse kart komutu HEMEN uygular, bu süre kadar bekler ve çıkışı ESKİ hâline geri alır — ör. <c>value: "0", delay: 10</c>
/// bir cihazın gücünü keser ve 10 sn sonra yeniden verir (güç döngüsü). Gecikmeli başlatma DEĞİLDİR.
/// Scadex şimdilik göndermez. Gönderilmeye başlandığında kart geri dönüşü BİLDİRMEZ: Merkez süre dolunca kanalı bir arka plan işiyle
/// eski değere kendisi çeker (kullanıcı kararı 2026-10-31, PROJECT_OVERVIEW.md § Komut). <c>null</c> tele hiç çıkmaz: projenin <c>DefaultIgnoreCondition = Never</c> ayarı burada bilerek ezilir,
/// <c>"delay": null</c> kartın ayrıştırıcısını yormasın.
/// </param>
public sealed record ScadaCommandRequest(
    int ChannelNumber,
    string? Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Delay = null
);

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
