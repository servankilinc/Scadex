import http from '@/lib/axios-helper';
import type { CanvasSettingsUpsertRequest } from '@/models/canvasSettings';
import type { DiagramCanvasSettingsDto } from '@/models/diagram';

/** Büyük/küçük harfe DUYARLI — küçük harfli `/api/canvassettings` eşleşmez. */
const CANVAS_SETTINGS_ROUTE = '/api/CanvasSettings';

/**
 * Canvas tercihlerini yazar (upsert). `automationId` rotadan gider, gövdede yoktur.
 *
 * Dönüş tipi diyagram aggregate'indeki blokla AYNI (`DiagramCanvasSettingsDto`):
 * yanıt doğrudan o cache'in üzerine yazılıyor.
 */
export async function upsertCanvasSettings(automationId: string, request: CanvasSettingsUpsertRequest): Promise<DiagramCanvasSettingsDto> {
  return http.put<DiagramCanvasSettingsDto>(`${CANVAS_SETTINGS_ROUTE}/automation/${automationId}`, request);
}
