import type { CameraDraft, ControlCardDraft, DiagramDeviceDto, PcDeviceDraft } from '@/models/diagram';
import { CameraBrand, DeviceType } from '@/models/enums';

/**
 * Cihazın 1-1 ek nesneleri (2026-10-22): kamera → `camera`, kontrol modülü → `controlCard`, PC → `pc`; diğer tiplerde
 * üçü de `null`. Sunucu (`ValidateDeviceExtensions`) taslakta TİPİYLE EŞLEŞEN nesneyi zorunlu tutar — bu dosya o kuralın
 * istemci tarafındaki tek kaynağıdır: yeni cihaz, kopya ve taslak hep buradan geçer.
 */
type DeviceExtensions = Pick<DiagramDeviceDto, 'camera' | 'controlCard' | 'pc'>;

/** Şablondan yeni bırakılan cihazın boş ek nesnesi. */
export function emptyDeviceExtensions(deviceTypeId: DeviceType): DeviceExtensions {
  return {
    camera:
      deviceTypeId === DeviceType.Camera
        ? { brand: CameraBrand.Hikvision, model: null, description: null, username: null, hasPassword: false, password: null }
        : null,
    controlCard: deviceTypeId === DeviceType.ControlModule ? { macAddress: null, username: null, hasPassword: false, password: null } : null,
    pc: deviceTypeId === DeviceType.Pc ? { macAddress: null } : null
  };
}

/**
 * Kopyanın ek nesnesi. MAC KOPYALANMAZ (tek bir fiziksel karta aittir, tekillik index'i zaten reddederdi); parola da
 * kopyalanamaz — sunucu onu hiç göndermez, kopya parolasız doğar. Marka, model, kullanıcı adı taşınır.
 */
export function copyDeviceExtensions(source: DiagramDeviceDto): DeviceExtensions {
  return {
    camera: source.camera ? { ...source.camera, hasPassword: false, password: null } : null,
    controlCard: source.controlCard ? { ...source.controlCard, macAddress: null, hasPassword: false, password: null } : null,
    pc: source.pc ? { macAddress: null } : null
  };
}

/** Taslağa giden hâl: `hasPassword` telde yok; bekleyen parola yoksa `null` ("dokunma"). */
export function toExtensionDrafts(device: DiagramDeviceDto): { camera: CameraDraft | null; controlCard: ControlCardDraft | null; pc: PcDeviceDraft | null } {
  const { camera, controlCard, pc } = device;
  return {
    camera: camera
      ? { brand: camera.brand, model: camera.model, description: camera.description, username: camera.username, password: camera.password ?? null }
      : null,
    controlCard: controlCard ? { macAddress: controlCard.macAddress, username: controlCard.username, password: controlCard.password ?? null } : null,
    pc: pc ? { macAddress: pc.macAddress } : null
  };
}
