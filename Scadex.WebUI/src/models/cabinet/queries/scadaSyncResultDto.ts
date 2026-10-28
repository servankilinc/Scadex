import type { CommandStatus } from '@/models/enums';

/**
 * Ayna: Scadex.Model/Dtos/Scada/Queries/ScadaSyncResultDto.cs
 *
 * "Kartı oku"nun özeti (`POST /api/Cabinet/{id}/scada-sync`). Değişen değerlerin kendisi burada DEĞİL: sunucu onları `DiagramHub`
 * `ChannelValuesChanged` ile yayınlar, tuval canlı akıştan güncellenir.
 *
 * **200 "kart okundu" DEMEK DEĞİLDİR** (komut sonucuyla aynı sözleşme): kart hata döndürdüyse ya da yanıt vermediyse yine 200 gelir,
 * sonuç `status`'tadır. Hata (ProblemDetails) yalnızca kabin/SCADA yapılandırması içindir (SCADA kapalı, adres yok, kabin yok).
 */
export interface ScadaSyncResultDto {
  status: CommandStatus;
  /** Başarısızlıkta kartın / bağlantının mesajı. */
  message: string | null;
  /** Kartın bildirdiği kanal sayısı (yalnızca `Succeeded`'te anlamlı). */
  readCount: number;
  /** Kayıtlı değerinden farklı çıkıp yazılan kanal sayısı. */
  changedCount: number;
  /** Kartın bildirdiği ama bu kabinde tanımlı (etkin) karşılığı olmayan kanal sayısı. */
  unmatchedCount: number;
  syncedAtUtc: string;
}
