using System.Text.Json.Serialization;

namespace Scadex.Model.Dtos.Scada.Commands;

/// <summary>
/// Karta giden çıkış komutunun gövdesi: <c>POST {ScadaBaseUrl}/command</c>.
/// </summary>
/// <param name="ChannelNumber"> Çıkış numarası (röle 1–16, LED 17–24). </param>
/// <param name="Value"> <c>"1"</c> aç, <c>"0"</c> kapat (fiziksel değer; NO/NC çözümü üstte yapılmıştır). </param>
/// <param name="Delay">
/// Saniye. Verilirse kart komutu HEMEN uygular, bu süre kadar bekler ve çıkışı ESKİ hâline geri alır — ör. <c>value: "0", delay: 10</c>
/// bir cihazın gücünü keser ve 10 sn sonra yeniden verir (güç döngüsü). Gecikmeli başlatma DEĞİLDİR.
/// Scadex şimdilik göndermez. <c>null</c> tele hiç çıkmaz: projenin <c>DefaultIgnoreCondition = Never</c> ayarı burada bilerek ezilir,
/// <c>"delay": null</c> kartın ayrıştırıcısını yormasın.
/// </param>
public sealed record ScadaCommandRequest(
    int ChannelNumber,
    string? Value,
    int? Delay = null
);
