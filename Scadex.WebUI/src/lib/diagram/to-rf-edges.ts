import type { Edge } from '@xyflow/react';
import { EdgeRouting, LineStyle } from '@/models/enums';
import type { DiagramConnectionDto, DiagramDto } from '@/models/diagram';

/**
 * Domain → React Flow edge dönüşümü. Saf fonksiyon.
 *
 * Kritik eşleme: RF edge'in `source`/`target` alanlarında NODE id'si ister, pin
 * id'sini değil. Sunucu bu yüzden `sourceDeviceId`/`targetDeviceId`'yi
 * denormalize gönderiyor; burada indeks kurmaya gerek kalmıyor.
 */

export type ConnectionEdgeData = { connection: DiagramConnectionDto };
export type DiagramEdge = Edge<ConnectionEdgeData>;

/**
 * `EdgeRouting` → RF edge tipi.
 * `Orthogonal` kendi bileşenimizdir (kayıtlı waypoint'leri çizer); diğer ikisi
 * RF'in yerleşik tipleridir.
 */
const ROUTING_TO_EDGE_TYPE: Record<EdgeRouting, string> = {
  [EdgeRouting.Orthogonal]: 'orthogonal',
  [EdgeRouting.Straight]: 'straight',
  [EdgeRouting.Curved]: 'default'
};

/** `LineStyle` → SVG `stroke-dasharray`. Solid'de öznitelik hiç yazılmaz. */
const LINE_STYLE_TO_DASH: Record<LineStyle, string | undefined> = {
  [LineStyle.Solid]: undefined,
  [LineStyle.Dashed]: '6 4',
  [LineStyle.Dotted]: '1 4'
};

export function toRfEdges(diagram: Pick<DiagramDto, 'connections'>): DiagramEdge[] {
  return diagram.connections.map(toRfEdge);
}

export function toRfEdge(connection: DiagramConnectionDto): DiagramEdge {
  return {
    id: connection.id,
    source: connection.sourceDeviceId,
    target: connection.targetDeviceId,
    sourceHandle: connection.sourcePinId,
    targetHandle: connection.targetPinId,
    type: ROUTING_TO_EDGE_TYPE[connection.routing] ?? 'orthogonal',
    // null etiket RF'te boş bir etiket kutusu çizdirir; undefined hiç çizdirmez.
    label: connection.label ?? undefined,
    zIndex: connection.zIndex,
    style: {
      stroke: connection.color,
      strokeWidth: connection.strokeWidth,
      strokeDasharray: LINE_STYLE_TO_DASH[connection.lineStyle]
    },
    data: { connection }
  };
}
