import type { DiagramPinDto } from '@/models/diagram';
import type { DiagramEdge } from './to-rf-edges';
import type { DiagramNode } from './to-rf-nodes';

/**
 * Kablo çizme kuralları.
 *
 * Bu kurallar sunucuda da uygulanır (`DiagramService.ValidateReferences`).
 * Buradaki kopya bir GÜVENLİK SINIRI DEĞİL, kullanıcı kolaylığıdır: geçersiz
 * bir kabloyu çizdirip 1,5 saniye sonra kaydetmede reddettirmek yerine, imleç
 * daha bırakılmadan engellenir.
 *
 * Sunucu tarafı kaldırılamaz — istemci doğrulaması atlanabilir.
 */

export type ConnectionRejection = 'unknown-pin' | 'self' | 'duplicate' | 'voltage';

export const ConnectionRejectionMessages: Record<ConnectionRejection, string> = {
  'unknown-pin': 'Pin bulunamadı',
  self: 'Bir pin kendisine bağlanamaz',
  duplicate: 'Bu iki pin arasında zaten bir kablo var',
  voltage: 'Farklı gerilim seviyesindeki pinler bağlanamaz'
};

export interface ConnectionContext {
  pinsById: Map<string, DiagramPinDto>;
  /** Yönsüz pin çifti anahtarları — mevcut kablolar. */
  existingPairs: Set<string>;
}

/**
 * Çift anahtarı YÖNSÜZ üretilir: `(a,b)` ile `(b,a)` aynı kablodur.
 *
 * `ConnectionMode.Loose` altında hangi ucun "kaynak" olduğu kullanıcının çizme
 * yönüne bağlıdır, yani anlamsızdır. Sıralı karşılaştırmak, aynı kabloyu ters
 * yönde ikinci kez çizmeye izin verirdi.
 */
export function pairKey(a: string, b: string): string {
  return a < b ? `${a}|${b}` : `${b}|${a}`;
}

export function buildConnectionContext(nodes: DiagramNode[], edges: DiagramEdge[]): ConnectionContext {
  const pinsById = new Map<string, DiagramPinDto>();
  for (const node of nodes) {
    if (node.type !== 'template') continue;
    for (const pin of node.data.device.pins) pinsById.set(pin.id, pin);
  }

  const existingPairs = new Set<string>();
  for (const edge of edges) {
    if (edge.sourceHandle && edge.targetHandle) existingPairs.add(pairKey(edge.sourceHandle, edge.targetHandle));
  }

  return { pinsById, existingPairs };
}

/** Geçerliyse `null`, değilse reddetme sebebi döner. */
export function validateConnection(sourcePinId: string | null | undefined, targetPinId: string | null | undefined, context: ConnectionContext): ConnectionRejection | null {
  if (!sourcePinId || !targetPinId) return 'unknown-pin';
  if (sourcePinId === targetPinId) return 'self';

  const source = context.pinsById.get(sourcePinId);
  const target = context.pinsById.get(targetPinId);
  if (!source || !target) return 'unknown-pin';

  if (context.existingPairs.has(pairKey(sourcePinId, targetPinId))) return 'duplicate';

  // Gerilim kuralı yalnızca İKİ TARAF da belirtilmişse işler. Biri null ise
  // ("belirtilmemiş") susulur: bilinmeyeni hata saymak, gerilimi henüz girilmemiş
  // şablonlarla çalışmayı imkânsız kılardı.
  if (source.voltageLevel != null && target.voltageLevel != null && source.voltageLevel !== target.voltageLevel) {
    return 'voltage';
  }

  return null;
}
