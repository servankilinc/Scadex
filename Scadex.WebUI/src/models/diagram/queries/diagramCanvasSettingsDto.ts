/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramCanvasSettingsDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { BackgroundVariant } from '@/models/enums';

/**
 * Kabinin canvas tercihleri. Aggregate'in İÇİNDE gelir — ayrı çağrı yoktur.
 *
 * `id` ve audit alanları BİLEREK yok: kabinin kayıtlı ayarı yoksa sunucu
 * varsayılan döner ve satır oluşturmaz, o yanıtta bir kimlik olamaz.
 */
export interface DiagramCanvasSettingsDto {
  gridSize: number;
  snapToGrid: boolean;
  backgroundVariant: BackgroundVariant;
  gridColor: string;
  backgroundColor: string;
  minZoom: number;
  maxZoom: number;
}
