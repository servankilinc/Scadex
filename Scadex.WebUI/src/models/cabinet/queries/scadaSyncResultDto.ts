/**
 * Ayna: Scadex.Model/Dtos/Scada/Queries/ScadaSyncResultDto.cs
 *
 * "Kartı oku"nun özeti (`POST /api/Cabinet/{id}/scada-sync`). Değişen değerlerin kendisi burada DEĞİL: sunucu onları `DiagramHub`
 * `ChannelValuesChanged` ile yayınlar, tuval canlı akıştan güncellenir. Kart okunamazsa yanıt 200 değil hatadır (ProblemDetails).
 */
export interface ScadaSyncResultDto {
  /** Kartın bildirdiği kanal sayısı. */
  readCount: number;
  /** Kayıtlı değerinden farklı çıkıp yazılan kanal sayısı. */
  changedCount: number;
  /** Kartın bildirdiği ama bu kabinde tanımlı (etkin) karşılığı olmayan kanal sayısı. */
  unmatchedCount: number;
  syncedAtUtc: string;
}
