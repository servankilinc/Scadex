/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { DiagramAnnotationItemDto } from './diagramAnnotationItemDto';
import type { DiagramAutomationDto } from './diagramAutomationDto';
import type { DiagramCanvasSettingsDto } from './diagramCanvasSettingsDto';
import type { DiagramConnectionDto } from './diagramConnectionDto';
import type { DiagramDeviceDto } from './diagramDeviceDto';

/**
 * `GET /api/Diagram/automation/{id}` yanıtı — editörün açılışı için gereken her şey
 * tek istekte.
 *
 * Burada OLMAYAN iki şey bilinçli:
 *   - PALET: her otomasyonda aynı olduğu için ayrı query key + uzun staleTime
 *   - CANLI DEĞERLER: `ioChannels` yalnızca statik tanım; anlık değer SignalR'dan
 */
export interface DiagramDto {
  automation: DiagramAutomationDto;
  devices: DiagramDeviceDto[];
  connections: DiagramConnectionDto[];
  annotations: DiagramAnnotationItemDto[];
  /** Otomasyon bazlıdır; kayıtlı satır yoksa sunucu VARSAYILAN döner ve satır oluşturmaz. */
  canvasSettings: DiagramCanvasSettingsDto;
  fetchedAtUtc: string;
}
