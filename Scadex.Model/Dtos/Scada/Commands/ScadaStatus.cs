using static Scadex.Model.Enums.EntityEnums;

namespace Scadex.Model.Dtos.Scada.Commands;

// Kartin okuma ucu: GET {ScadaBaseUrl}/status (tum kanallar) / POST {ScadaBaseUrl}/status (govdedeki kanallar)

/// <summary> <c>POST /status</c> gövdesindeki okunmak istenen kanal: <c>"I"</c> / <c>"A"</c> / <c>"O"</c> + kanal numarası. </summary>
public sealed record ScadaChannelRef(string Type, int ChannelNumber);

/// <summary>
/// Kartın bildirdiği bir kanal. <see cref="Value"/> metindir (<c>"0"</c>/<c>"1"</c>, analogda <c>"23.5"</c>); <c>"O"</c> için rölenin
/// O ANKİ FİZİKSEL durumudur (son komut değil, NC'de mantıksal isteğin tersi).
/// </summary>
public sealed record ScadaChannelValue(string Type, int ChannelNumber, string? Value);

/// <summary>
/// Okuma sonucu. Durum eşlemesi komutla aynıdır (<c>ScadaCommandResponse</c>): 2xx + çözülebilen gövde <see cref="CommandStatus.Succeeded"/>,
/// 4xx/5xx ya da bozuk gövde <see cref="CommandStatus.Failed"/>, zaman aşımı / bağlantı hatası <see cref="CommandStatus.NoResponse"/>.
/// <see cref="Values"/> yalnızca başarıda doludur.
/// </summary>
public sealed record ScadaStatusResponse(CommandStatus Status, IReadOnlyList<ScadaChannelValue>? Values, string? Message);
