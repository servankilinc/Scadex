/**
 * Ayna: Scadex.Signalization/Hubs/ISignalizationHubClientContract.cs + Realtime/OperatorSessionChangedMessage.cs
 *
 * `/hubs/signalization` üzerinden sunucudan gelen olayların gövdeleri. Hub, REST ile AYNI JSON ayarlarını kullanır (camelCase,
 * sayısal enum) — diğer DTO aynalarıyla birebir aynı kodlama.
 */
import type { OperatorSessionStatus } from './enums';

/** Bildirimdir: veri HTTP'den yeniden okunur. Alanlar yalnızca "bu olay beni ilgilendiriyor mu" kararı içindir. */
export interface OperatorSessionChangedMessage {
  /** IDENTITY — `number`. */
  sessionId: number;
  cabinetId: string;
  status: OperatorSessionStatus;
  /** `endedAtUtc == null`. */
  isOpen: boolean;
}

/**
 * Sunucunun çağırdığı metot ADLARI. Yanlış yazılan bir ad hata üretmez, olay sessizce hiçbir yere gitmez — tek yerde sabitlenir.
 */
export const SignalizationHubEvents = {
  operatorSessionChanged: 'OperatorSessionChanged'
} as const;

/** İstemcinin çağırdığı hub metotları. */
export const SignalizationHubMethods = {
  subscribeSessions: 'SubscribeSessions',
  unsubscribeSessions: 'UnsubscribeSessions'
} as const;
