/** Ayna: Scadex.Model/Dtos/Realtime/Queries/RealtimeEvents.cs — sözleşme: docs/api-contract/09-realtime.md */
import type { CommandStatus, DeviceCommandType, DeviceStatus } from '@/models/enums';

/**
 * `/hubs/diagram` üzerinden sunucudan gelen olayların gövdeleri.
 *
 * Hub olayları REST yanıtlarıyla **aynı** serializer'dan geçiyor
 * (`AddJsonProtocol` + `ApiJsonOptions.Apply`), dolayısıyla casing ve sayısal enum
 * kodlaması buradaki diğer DTO aynalarıyla birebir aynı.
 */

export interface ChannelValueChange {
  ioChannelId: string;
  /** Kanalın bağlı olduğu cihaz — node bazında yeniden çizim için. */
  deviceId: string;
  channelNumber: number;
  /** Null = kanal var ama okunamadı. `"0"` ile AYNI ŞEY DEĞİL. */
  value: string | null;
  updatedAt: string;
}

export interface DeviceStatusChange {
  deviceId: string;
  /** Null = hiç telemetri alınmadı. `Offline` (0) ile AYNI ŞEY DEĞİL. */
  statusId: DeviceStatus | null;
  lastSeen: string | null;
}

export interface CabinetStatusChange {
  cabinetId: string;
  statusId: DeviceStatus | null;
  lastSeen: string | null;
  scadaLastIngestAt: string | null;
}

/**
 * Bir kumandanın sonuçlandığı bildirimi.
 *
 * Üstteki üç olaydan **farklı bir sebeple** var: onlar sahadan gelen değişimi
 * taşır, bu ise bir kullanıcının yaptığı işi. Komutu gönderen sonucu zaten HTTP
 * yanıtında alır; bu yayın aynı kabini izleyen **diğerleri** için.
 *
 * **Gönderen de bu olayı alır.** Sunucu grubun tamamına yayınlıyor ve gönderenin
 * bağlantısı da o grupta; ayıklama istemcide, `commandId` üzerinden yapılır.
 *
 * Gövde `DeviceCommandResultDto`'nun ALT KÜMESİDİR (`payloadJson`, `sentAt`,
 * `elapsedMs` yok) — bu yüzden doğrudan geçmiş listesine eklenmez.
 */
export interface CommandCompleted {
  commandId: string;
  deviceId: string;
  ioChannelId: string | null;
  channelNumber: number | null;
  commandType: DeviceCommandType;
  status: CommandStatus;
  resultMessage: string | null;
  respondedAt: string | null;
  requestedByName: string | null;
}

/**
 * Sunucunun çağırdığı metot ADLARI. Sözleşmenin en kırılgan parçası: yanlış
 * yazılan bir ad hiçbir hata üretmez, olay sessizce hiçbir yere gitmez ve sorun
 * ancak arayüzde "canlı veri gelmiyor" olarak fark edilir.
 *
 * Tek yerde sabitlenmeleri, en azından yazım hatasının tek bir yerde olmasını
 * sağlar ve dinleyici tarafında derleyici desteği verir.
 */
export const DiagramHubEvents = {
  channelValuesChanged: 'ChannelValuesChanged',
  deviceStatusChanged: 'DeviceStatusChanged',
  cabinetStatusChanged: 'CabinetStatusChanged',
  commandCompleted: 'CommandCompleted'
} as const;

/** İstemcinin çağırdığı hub metotları. */
export const DiagramHubMethods = {
  subscribe: 'Subscribe',
  unsubscribe: 'Unsubscribe'
} as const;
