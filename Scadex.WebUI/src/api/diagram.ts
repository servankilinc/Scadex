import http from '@/lib/axios-helper';
import type { DiagramDto, DiagramSaveRequest } from '@/models/diagram';

const DIAGRAM_ROUTE = '/api/Diagram';

/** Editörün açılışı için gereken her şey tek istekte — canvas ayarları dahil, palet HARİÇ. */
export async function getCabinetDiagram(cabinetId: string): Promise<DiagramDto> {
  return http.get<DiagramDto>(`${DIAGRAM_ROUTE}/cabinet/${cabinetId}`);
}

/**
 * Editörün biriktirdiği tüm değişiklikleri TEK transaction'da uygular.
 *
 * **Yanıt gövdesizdir.** Diyagramdaki her satırın — pin ve kanal dahil — Guid'ini
 * istemci ürettiği için geri öğrenilecek bir şey yok; kaydetme atomik olduğundan
 * 200'ün kendisi "gönderdiğim her şey kalıcı" demektir.
 */
export async function saveDiagram(cabinetId: string, request: DiagramSaveRequest): Promise<void> {
  return http.post(`${DIAGRAM_ROUTE}/cabinet/${cabinetId}/save`, request);
}
