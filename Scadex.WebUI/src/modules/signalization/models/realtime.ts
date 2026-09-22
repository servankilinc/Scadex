/**
 * Ayna: Scadex.Signalization/Hubs/ISignalizationHubClientContract.cs + Realtime/{OperatorSessionChangedMessage.cs,
 * SignalCabinetStateChangedMessage.cs, SignalDoorSwitchChangedMessage.cs} + Enums/SignalEnums.cs > SignalDoorKind
 *
 * `/hubs/signalization` üzerinden sunucudan gelen olayların gövdeleri. Hub, REST ile AYNI JSON ayarlarını kullanır (camelCase,
 * sayısal enum) — diğer DTO aynalarıyla birebir aynı kodlama.
 */
import type { OperatorSessionStatus } from './enums';
import type { SignalCabinetOutput } from './virtual-cabinet';

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
  operatorSessionChanged: 'OperatorSessionChanged',
  signalCabinetStateChanged: 'SignalCabinetStateChanged',
  signalDoorSwitchChanged: 'SignalDoorSwitchChanged'
} as const;

/** İstemcinin çağırdığı hub metotları. */
export const SignalizationHubMethods = {
  subscribeSessions: 'SubscribeSessions',
  unsubscribeSessions: 'UnsubscribeSessions',
  subscribeCabinet: 'SubscribeCabinet',
  unsubscribeCabinet: 'UnsubscribeCabinet'
} as const;

/**
 * Kabinin bir ÇIKIŞI değişti (siren / dış kapı aydınlatması / iç kapı kilidi). Yalnızca o kabinin grubuna gelir.
 *
 * Kaynak çekirdektir: başarılı komut çıkış kanalının değerini değiştirince (komutu kim gönderdiyse) yayınlanır. Olay VERİYİ
 * taşır — istemci `live` sorgusunu yeniden okumaz, önbellekteki alanı yamalar.
 */
export interface SignalCabinetStateChangedMessage {
  cabinetId: string;
  target: SignalCabinetOutput;
  /** Siren için `null`; diğerlerinde kapının kimliği. */
  targetId: string | null;
  /** Siren çalıyor / LED yanıyor / kilit AÇIK. */
  isOn: boolean;
  changedAtUtc: string;
}

/** Anahtarı değişen kapının türü. */
export const SignalDoorKind = {
  Outer: 1,
  Inner: 2
} as const;
export type SignalDoorKind = (typeof SignalDoorKind)[keyof typeof SignalDoorKind];

/**
 * Bir kapının anahtarı (input) değişti; "açık" yorumu SUNUCUDA kapının `switchOpenValue`'suyla yapılmıştır. Yalnızca o kabinin
 * grubuna gelir ve veriyi taşır.
 */
export interface SignalDoorSwitchChangedMessage {
  cabinetId: string;
  doorKind: SignalDoorKind;
  doorId: string;
  /** `null` = bilinmiyor (kanal değeri okunamadı). */
  isOpen: boolean | null;
  changedAtUtc: string;
}
