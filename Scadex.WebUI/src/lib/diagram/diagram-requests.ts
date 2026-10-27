import type {
  AnnotationCreateRequest,
  AnnotationLayoutItem,
  AnnotationUpdateRequest,
  ConnectionCreateRequest,
  ConnectionUpdateRequest,
  DeviceCreateRequest,
  DeviceLayoutItem,
  DeviceUpdateRequest,
  DiagramAnnotationItemDto,
  DiagramConnectionDto,
  DiagramDeviceDto
} from '@/models/diagram';

/**
 * Tuvaldeki DTO → uç gövdesi dönüşümleri. Saf fonksiyonlar; her uç ailesi için TEK üretici var ki oluşturma ile
 * güncelleme aynı alanları aynı kuralla göndersin.
 *
 * **Boyut override'ı DTO'dan okunur, `node.width`'ten DEĞİL.** `node.width` `deviceSize`'ın ürettiği EFEKTİF değerdir;
 * onu göndermek şablon ölçüsünü kalıcı bir override olarak yazar ve "şablona dön"ü etkisiz kılardı.
 */

/** Cihaz formunun yazdığı alanlar (`PUT …/devices/{id}`). Yerleşim alanları ayrı uçtadır. */
export type DeviceProperties = Pick<
  DiagramDeviceDto,
  'name' | 'ipAddress' | 'monitoringPort' | 'pingIntervalSec' | 'isMonitoringEnabled' | 'camera' | 'controlCard' | 'pc'
>;

export function pickDeviceProperties(device: DiagramDeviceDto): DeviceProperties {
  const { name, ipAddress, monitoringPort, pingIntervalSec, isMonitoringEnabled, camera, controlCard, pc } = device;
  return { name, ipAddress, monitoringPort, pingIntervalSec, isMonitoringEnabled, camera, controlCard, pc };
}

/**
 * Kaydedilen özelliklerin tuvaldeki hâli. Parola sunucuya gitti: bekleyen değer silinir, `hasPassword` ondan türetilir.
 * Üretilen sır ise BİLEREK tutulur — sunucu onu bir daha döndürmez, operatör onu şimdi cihaza girmeli; yeniden göndermek
 * aynı sırrı yazar, zararsızdır. İptal edilen sır (`''`) bekleyen değer olarak kalmaz.
 */
export function savedDeviceProperties(properties: DeviceProperties): DeviceProperties {
  const { camera, controlCard, pc } = properties;
  const credential = <T extends { hasCredential: boolean; secret?: string | null }>(value: T | null): T | null =>
    value && value.secret != null ? { ...value, hasCredential: value.secret !== '', secret: value.secret === '' ? null : value.secret } : value;

  return {
    ...properties,
    camera: camera && camera.password != null ? { ...camera, hasPassword: camera.password !== '', password: null } : camera,
    controlCard: credential(controlCard),
    pc: credential(pc)
  };
}

/**
 * Cihaz özellikleri. Ek nesneler (kamera / kontrol kartı / PC) tiplerine göre tam olarak biri dolu gelir; sunucu tipiyle
 * eşleşmeyeni 400'ler. `hasPassword`/`hasCredential` telde yok; bekleyen parola/sır yoksa `null` ("dokunma").
 */
export function toDeviceUpdateRequest(device: DiagramDeviceDto): DeviceUpdateRequest {
  const { camera, controlCard, pc } = device;
  return {
    name: device.name,
    ipAddress: device.ipAddress,
    monitoringPort: device.monitoringPort,
    pingIntervalSec: device.pingIntervalSec,
    isMonitoringEnabled: device.isMonitoringEnabled,
    camera: camera
      ? { brand: camera.brand, model: camera.model, description: camera.description, username: camera.username, password: camera.password ?? null }
      : null,
    controlCard: controlCard ? { secret: controlCard.secret ?? null } : null,
    pc: pc ? { secret: pc.secret ?? null } : null
  };
}

export function toDeviceCreateRequest(device: DiagramDeviceDto): DeviceCreateRequest {
  return {
    ...toDeviceUpdateRequest(device),
    id: device.id,
    componentTemplateId: device.componentTemplateId,
    coordinateX: device.coordinateX,
    coordinateY: device.coordinateY,
    width: device.width,
    height: device.height,
    rotation: device.rotation,
    zIndex: device.zIndex,
    isLocked: device.isLocked,
    isVisible: device.isVisible
  };
}

export function toDeviceLayoutItem(device: DiagramDeviceDto): DeviceLayoutItem {
  return {
    id: device.id,
    coordinateX: device.coordinateX,
    coordinateY: device.coordinateY,
    width: device.width,
    height: device.height,
    rotation: device.rotation,
    zIndex: device.zIndex,
    isLocked: device.isLocked,
    isVisible: device.isVisible
  };
}

export function toConnectionUpdateRequest(connection: DiagramConnectionDto): ConnectionUpdateRequest {
  return {
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

export function toConnectionCreateRequest(connection: DiagramConnectionDto): ConnectionCreateRequest {
  return {
    ...toConnectionUpdateRequest(connection),
    id: connection.id,
    sourcePinId: connection.sourcePinId,
    targetPinId: connection.targetPinId
  };
}

export function toAnnotationUpdateRequest(annotation: DiagramAnnotationItemDto): AnnotationUpdateRequest {
  return {
    name: annotation.name,
    text: annotation.text,
    shape: annotation.shape,
    backgroundColor: annotation.backgroundColor,
    fontColor: annotation.fontColor,
    fontSize: annotation.fontSize,
    isBold: annotation.isBold,
    borderColor: annotation.borderColor
  };
}

export function toAnnotationCreateRequest(annotation: DiagramAnnotationItemDto): AnnotationCreateRequest {
  return { ...toAnnotationUpdateRequest(annotation), ...toAnnotationLayoutItem(annotation) };
}

export function toAnnotationLayoutItem(annotation: DiagramAnnotationItemDto): AnnotationLayoutItem {
  return {
    id: annotation.id,
    coordinateX: annotation.coordinateX,
    coordinateY: annotation.coordinateY,
    width: annotation.width,
    height: annotation.height,
    rotation: annotation.rotation,
    zIndex: annotation.zIndex,
    isLocked: annotation.isLocked,
    isVisible: annotation.isVisible
  };
}

/** Yerleşim ucuna giden alanlar; geri kalanı içerik ucunundur (`PUT …/annotations/{id}`). */
export const LAYOUT_KEYS = ['coordinateX', 'coordinateY', 'width', 'height', 'rotation', 'zIndex', 'isLocked', 'isVisible'] as const;

export type LayoutKey = (typeof LAYOUT_KEYS)[number];

export function touchesLayout(patch: object): boolean {
  return Object.keys(patch).some(key => (LAYOUT_KEYS as readonly string[]).includes(key));
}

export function touchesContent(patch: object): boolean {
  return Object.keys(patch).some(key => !(LAYOUT_KEYS as readonly string[]).includes(key));
}
