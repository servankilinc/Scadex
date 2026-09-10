import {
  emptyDelta,
  type AnnotationDraft,
  type ConnectionDraft,
  type DeviceDraft,
  type DiagramSaveRequest
} from '@/models/diagram';
import type { DiagramJournal } from './journal';
import { isUnsaved } from './unsaved-store';
import type { AnnotationNode, DeviceNode, DiagramNode } from './to-rf-nodes';
import type { DiagramEdge } from './to-rf-edges';

/**
 * Koordinat React Flow'dan, geri kalan DTO'dan okunur. Sürükleme sırasında konumu
 * RF kendi tutar (`node.position`).
 *
 * Aile başına TEK bir taslak üreticisi var: oluşturma ile güncelleme aynı gövdeyi
 * gönderir, farkı sunucu Id'ye bakarak anlar.
 */
export function buildSaveRequest(nodes: DiagramNode[], edges: DiagramEdge[], journal: DiagramJournal): DiagramSaveRequest {
  const nodeById = new Map(nodes.map(n => [n.id, n]));
  const edgeById = new Map(edges.map(e => [e.id, e]));

  const devices = emptyDelta<DeviceDraft>();
  const connections = emptyDelta<ConnectionDraft>();
  const annotations = emptyDelta<AnnotationDraft>();

  for (const id of journal.devices.touched) {
    const node = deviceNode(nodeById.get(id));
    if (node) devices.upserted.push(toDeviceDraft(node));
  }
  devices.deleted = [...journal.devices.deleted];

  for (const id of journal.connections.touched) {
    const edge = edgeById.get(id);
    if (edge?.data) connections.upserted.push(toConnectionDraft(edge));
  }
  connections.deleted = [...journal.connections.deleted];

  for (const id of journal.annotations.touched) {
    const node = annotationNode(nodeById.get(id));
    if (node) annotations.upserted.push(toAnnotationDraft(node));
  }
  annotations.deleted = [...journal.annotations.deleted];

  return { devices, connections, annotations };
}

function deviceNode(node: DiagramNode | undefined): DeviceNode | null {
  return node?.type === 'template' ? node : null;
}

function annotationNode(node: DiagramNode | undefined): AnnotationNode | null {
  return node?.type === 'annotation' ? node : null;
}

function toDeviceDraft(node: DeviceNode): DeviceDraft {
  const { device } = node.data;

  // Pin ve kanal kimlikleri SALT-OLUŞTURMA: mevcut bir cihaza gönderilirse sunucu
  // 400 döner (pinleri zaten var). Id'nin kendisi "bu kayıt sunucuya gitti mi"
  // sorusunu cevaplayamadığı için defter sorulur.
  //
  // Başarısız bir gönderiden sonra kayıt "kaydedilmemiş" kalır (`handleFailed`
  // `markSaved` çağırmaz), yani bir sonraki deneme pinleri yeniden taşır.
  const isNew = isUnsaved(node.id);

  return {
    id: node.id,
    componentTemplateId: device.componentTemplateId,
    name: device.name,
    coordinateX: node.position.x,
    coordinateY: node.position.y,
    rotation: device.rotation,
    zIndex: device.zIndex,
    isLocked: device.isLocked,
    isVisible: device.isVisible,
    externalCode: device.externalCode,
    // `componentTemplatePinId` tipte null olabilir ama BURADA olamaz: gönderilen
    // pinler `instantiate-template-pins.ts`'in şablondan ürettikleridir ve o alanı
    // daima doldurur. Null gelseydi sunucu şema karşılaştırmasında zaten 400'e
    // düşerdi — sessizce atlamak hatayı gizlemek olurdu.
    pins: isNew ? device.pins.map(pin => ({ id: pin.id, componentTemplatePinId: pin.componentTemplatePinId! })) : [],
    // Kanal adresi (yön + numara) birlikte gider: kartta IN1 ile OUT1 ayrı
    // noktalar ve sunucu kimliği bu çiftle eşliyor.
    ioChannels: isNew
      ? device.ioChannels.map(channel => ({
          id: channel.id,
          direction: channel.direction,
          channelNumber: channel.channelNumber
        }))
      : []
  };
}

function toConnectionDraft(edge: DiagramEdge): ConnectionDraft {
  const { connection } = edge.data!;
  return {
    id: edge.id,
    sourcePinId: connection.sourcePinId,
    targetPinId: connection.targetPinId,
    label: connection.label,
    wireType: connection.wireType,
    color: connection.color,
    lineStyle: connection.lineStyle,
    strokeWidth: connection.strokeWidth,
    routing: connection.routing,
    waypoints: connection.waypoints,
    zIndex: connection.zIndex
  };
}

function toAnnotationDraft(node: AnnotationNode): AnnotationDraft {
  const { annotation } = node.data;
  return {
    id: node.id,
    name: annotation.name,
    coordinateX: node.position.x,
    coordinateY: node.position.y,
    width: annotation.width,
    height: annotation.height,
    rotation: annotation.rotation,
    zIndex: annotation.zIndex,
    isLocked: annotation.isLocked,
    isVisible: annotation.isVisible,
    text: annotation.text,
    shape: annotation.shape,
    backgroundColor: annotation.backgroundColor,
    fontColor: annotation.fontColor,
    fontSize: annotation.fontSize,
    isBold: annotation.isBold,
    borderColor: annotation.borderColor
  };
}
