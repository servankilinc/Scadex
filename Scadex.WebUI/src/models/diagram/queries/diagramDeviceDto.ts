/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramDeviceDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { CameraBrand, DeviceStatus } from '@/models/enums';
import type { DiagramIoChannelDto } from './diagramIoChannelDto';
import type { DiagramPinDto } from './diagramPinDto';
import type { DiagramTemplateDto } from './diagramTemplateDto';

/**
 * Canvas'ta bir React Flow node'u olarak render edilen cihaz.
 * `position` = (coordinateX, coordinateY), `draggable` = !isLocked,
 * `hidden` = !isVisible, boyut `width ?? template.width` (bkz. `deviceSize`).
 */
export interface DiagramDeviceDto {
  id: string;
  name: string;
  coordinateX: number;
  coordinateY: number;
  /** Derece. React Flow'da rotation prop'u yok — node kökünde CSS transform. */
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
  isActive: boolean;
  /**
   * Cihaz bazlı boyut override'ı. `null` = şablonun ölçüsü geçerli — bir varsayılan
   * kopyası DEĞİL: şablon sonradan büyürse override'sız cihazlar onunla birlikte
   * büyür. Opsiyonel (`?`) değil, çünkü sunucu null alanları gövdeden düşürmüyor.
   */
  width: number | null;
  height: number | null;
  componentTemplateId: string;
  ipAddress: string | null;
  /** Null = bilinmiyor (canlılık kanıtı yok; arayüzde `deviceStatusLabel`). 0 DEĞİL — 0 `DeviceStatus.Offline`'dır. */
  deviceStatusId: DeviceStatus | null;
  deviceStatusName: string | null;
  lastSeen: string | null;
  /**
   * İzleme (sunucuda `IMonitoredAsset`). Açıksa cihaz `ipAddress:monitoringPort`'a TCP ile, port boşsa `ipAddress`'e ICMP
   * ping ile yoklanır ve sonucu kabin durumuna yansır. İlk üçü cihaz formuyla yazılır; `lastConnectionError` yalnızca yoklamayla.
   */
  monitoringPort: number | null;
  pingIntervalSec: number;
  isMonitoringEnabled: boolean;
  lastConnectionError: string | null;
  /**
   * 1-1 ek tablolar (2026-10-22): şablon tipine göre en fazla biri doludur — kamera `camera`, kontrol modülü
   * `controlCard`, PC `pc`. Kimlik bilgileri (kullanıcı adı, parola, sır) cihaz gövdesinde DEĞİL burada. Oluşturma ve güncelleme
   * isteğinde de tipiyle eşleşen nesne gönderilmek ZORUNDA (`lib/diagram/diagram-requests.ts`).
   */
  camera: DiagramCameraDto | null;
  controlCard: DiagramControlCardDto | null;
  pc: DiagramPcDeviceDto | null;
  template: DiagramTemplateDto;
  pins: DiagramPinDto[];
  ioChannels: DiagramIoChannelDto[];
}

/**
 * Sunucu parolayı DÖNDÜRMEZ, yalnızca `hasPassword`. `password` alanı telde YOKTUR: editörde girilen, henüz kaydedilmemiş
 * parolayı taşır (`undefined`/`null` = dokunma, `''` = temizle). Yalnızca taslağa yazılır.
 */
interface PendingPassword {
  hasPassword: boolean;
  password?: string | null;
}

export interface DiagramCameraDto extends PendingPassword {
  brand: CameraBrand;
  model: string | null;
  description: string | null;
  username: string | null;
}

/**
 * Kontrol kartı (2026-10-28): SCADA ingest'i ve kart okumasını `DeviceId` header'ında bu MAC adresiyle gönderir; kart ve kabin
 * MAC'ten çözülür. `null` = kart bağlanamaz. Tekillik bilerek denetlenmez.
 */
export interface DiagramControlCardDto {
  macAddress: string | null;
}

/**
 * PC: RemoteDesk istemcisi `PcHub`'a Basic Auth ile bağlanır — kullanıcı adı cihazın Id'si, parola sistemin ürettiği sır.
 * Sunucu sırrı DÖNDÜRMEZ (yalnızca hash saklar), yalnızca `hasCredential`. `secret` alanı telde YOKTUR: editörde üretilen,
 * henüz kaydedilmemiş sırrı taşır (`undefined`/`null` = dokunma, `''` = iptal). Yalnızca taslağa yazılır.
 */
export interface DiagramPcDeviceDto {
  hasCredential: boolean;
  secret?: string | null;
}
