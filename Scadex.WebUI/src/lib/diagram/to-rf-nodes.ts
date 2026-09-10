import type { Node } from '@xyflow/react';
import type { DiagramAnnotationItemDto, DiagramDeviceDto, DiagramDto } from '@/models/diagram';

/**
 * Domain → React Flow node dönüşümü. Saf fonksiyon: aynı girdi her zaman aynı
 * çıktıyı verir, yan etkisi yoktur, test edilebilir.
 *
 * DeviceType başına ayrı node tipi YOK. `ComponentTemplate` zaten genişlik,
 * yükseklik, renk ve port şemasını taşıyor — görsel spec budur. 12 ayrı bileşen
 * aynı renderer'ın 12 kopyası olurdu ve `DeviceType 13` eklemek frontend deploy'u
 * gerektirirdi; oysa backend'de yeni tip eklemek migration bile gerektirmiyor.
 */

export type DeviceNodeData = { device: DiagramDeviceDto };
export type AnnotationNodeData = { annotation: DiagramAnnotationItemDto };

export type DeviceNode = Node<DeviceNodeData, 'template'>;
export type AnnotationNode = Node<AnnotationNodeData, 'annotation'>;
export type DiagramNode = DeviceNode | AnnotationNode;

export function toRfNodes(diagram: Pick<DiagramDto, 'devices' | 'annotations'>): DiagramNode[] {
  return [...diagram.annotations.map(toAnnotationNode), ...diagram.devices.map(toDeviceNode)];
}

/**
 * Cihazın EFEKTİF kutu ölçüsü: kendi override'ı varsa o, yoksa şablonunki.
 *
 * Boyut node'un içeriğinden türetilemez — pin konumları kutu ölçüsünün 0..1
 * kesri olarak saklandığı için ölçünün önceden bilinmesi zorunlu.
 *
 * **Tek fonksiyon olması bilinçli.** Aynı fallback üç yerde birden gerekiyor
 * (`toDeviceNode`, `updateDevice`, özellikler paneli); `??`'ü üç kez yazmak,
 * birinin unutulduğu anda "panelde 200 yazıyor ama canvas 120" durumunu üretirdi.
 */
export function deviceSize(device: DiagramDeviceDto): { width: number; height: number } {
  return {
    width: device.width ?? device.template.width,
    height: device.height ?? device.template.height
  };
}

export function toDeviceNode(device: DiagramDeviceDto): DeviceNode {
  return {
    id: device.id,
    type: 'template',
    position: { x: device.coordinateX, y: device.coordinateY },
    ...deviceSize(device),
    zIndex: device.zIndex,
    draggable: !device.isLocked,
    hidden: !device.isVisible,
    data: { device }
  };
}

export function toAnnotationNode(annotation: DiagramAnnotationItemDto): AnnotationNode {
  return {
    id: annotation.id,
    type: 'annotation',
    position: { x: annotation.coordinateX, y: annotation.coordinateY },
    width: annotation.width,
    height: annotation.height,
    zIndex: annotation.zIndex,
    draggable: !annotation.isLocked,
    hidden: !annotation.isVisible,
    // Notlar cihazların ALTINDA kalmalı: bir kutu notu cihazın üstüne binerse
    // cihaz seçilemez hale gelir. RF seçim isabetini render sırasına göre yapar,
    // bu yüzden sıralama toRfNodes'ta da notlar önce gelecek şekilde kurulur.
    selectable: !annotation.isLocked,
    data: { annotation }
  };
}
