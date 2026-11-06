/** Ayna: Scadex.Model/Realtime/Notifications/*.cs — sözleşme: docs/api-contract/09-realtime.md */
import type { CommandStatus, DeviceStatus } from '@/models/enums';

/**
 * `/hubs/diagram` üzerinden sunucudan gelen olayların gövdeleri.
 *
 * Hub olayları REST yanıtlarıyla **aynı** serializer'dan geçiyor
 * (`AddJsonProtocol` + `ApiJsonOptions.Apply`), dolayısıyla casing ve sayısal enum
 * kodlaması buradaki diğer DTO aynalarıyla birebir aynı.
 */

export interface ChannelValueChangeNotify {
  ioChannelId: string;
  /** Kanalın bağlı olduğu cihaz — node bazında yeniden çizim için. */
  deviceId: string;
  channelNumber: number;
  /** Null = kanal var ama okunamadı. `"0"` ile AYNI ŞEY DEĞİL. */
  value: string | null;
  updatedAt: string;
}

export interface DeviceStatusChangeNotify {
  deviceId: string;
  /** Null = bilinmiyor (canlılık kanıtı yok; arayüzde `deviceStatusLabel`). `Offline` (0) ile AYNI ŞEY DEĞİL. */
  statusId: DeviceStatus | null;
  lastSeen: string | null;
}

export interface AutomationStatusChangeNotify {
  automationId: string;
  statusId: DeviceStatus | null;
  lastSeen: string | null;
  scadaLastIngestAt: string | null;
}

/**
 * Bir kumandanın sonuçlandığı bildirimi.
 *
 * Üstteki üç olaydan **farklı bir sebeple** var: onlar sahadan gelen değişimi
 * taşır, bu ise bir kullanıcının yaptığı işi. Komutu gönderen sonucu zaten HTTP
 * yanıtında alır; bu yayın aynı otomasyonu izleyen **diğerleri** için.
 *
 * **Gönderen de bu olayı alır.** Sunucu grubun tamamına yayınlıyor ve gönderenin
 * bağlantısı da o grupta; ayıklama istemcide, `commandId` üzerinden yapılır.
 *
 * Gövde `DeviceCommandResultDto`'nun ALT KÜMESİDİR (`payloadJson`, `sentAt`,
 * `elapsedMs` yok) — bu yüzden doğrudan geçmiş listesine eklenmez.
 */
export interface CommandCompletedNotify {
  commandId: string;
  deviceId: string;
  ioChannelId: string | null;
  channelNumber: number | null;
  status: CommandStatus;
  resultMessage: string | null;
  respondedAt: string | null;
  requestedByName: string | null;
}

/**
 * Otomasyonun kart okuyucusunda kart okutuldu (`POST /api/Scada/card`). Otomasyonlar ekranındaki "Kart tanımlama" penceresi bunu
 * dinler. Kart tanımsız da olsa gelir; modüllerin okumayı nasıl işlediğinden bağımsızdır.
 */
export interface CardReadNotify {
  automationId: string;
  /** SCADA'nın gönderdiği ham kart numarası. */
  cardId: string;
  /** Kart aktif bir kullanıcıya tanımlıysa sahibi; değilse `null`. */
  userId: string | null;
  userFullName: string | null;
  /** Z'siz olabilir — `formatUtcDateTime` ile gösterilir. */
  occurredAtUtc: string;
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
  automationStatusChanged: 'AutomationStatusChanged',
  commandCompleted: 'CommandCompleted',
  cardRead: 'CardRead'
} as const;

/** İstemcinin çağırdığı hub metotları. */
export const DiagramHubMethods = {
  subscribe: 'Subscribe',
  unsubscribe: 'Unsubscribe',
  /** Tüm otomasyonların durum özeti (harita, otomasyon listesi). Bu gruba yalnızca `AutomationStatusChanged` gider, o da durum DEĞİŞİNCE. */
  subscribeAutomations: 'SubscribeAutomations',
  unsubscribeAutomations: 'UnsubscribeAutomations'
} as const;
