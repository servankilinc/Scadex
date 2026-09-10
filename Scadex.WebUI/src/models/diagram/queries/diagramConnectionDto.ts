/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramConnectionDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { EdgeRouting, LineStyle, WireType } from '@/models/enums';
import type { PointDto } from './pointDto';

/**
 * Canvas'ta bir React Flow edge'i olarak render edilen kablo.
 *
 * `sourceDeviceId` / `targetDeviceId` DENORMALİZE'dir: React Flow edge'in
 * `source`/`target` alanlarında NODE id'si ister, pin id'si değil. Bunlar
 * olmadan istemci tek bir kablo çizmeden önce tüm pin → cihaz indeksini kurardı.
 *
 *   edge.source       = sourceDeviceId
 *   edge.target       = targetDeviceId
 *   edge.sourceHandle = sourcePinId
 *   edge.targetHandle = targetPinId
 */
export interface DiagramConnectionDto {
  id: string;
  cabinetId: string;
  sourcePinId: string;
  targetPinId: string;
  sourceDeviceId: string;
  targetDeviceId: string;
  /** Null: draw-first UX'te yeni çizilen kablonun henüz etiketi yoktur. */
  label: string | null;
  wireType: WireType;
  /** CSS renk dizesi (şablon renginden farklı olarak burada string). */
  color: string;
  lineStyle: LineStyle;
  strokeWidth: number;
  routing: EdgeRouting;
  /**
   * Ara kırılma noktaları: kaynak → hedef sıralı, İKİ UÇ NOKTA HARİÇ.
   * Kırılma noktası yoksa boş dizi gelir — `null` değil.
   */
  waypoints: PointDto[];
  zIndex: number;
}
