/**
 * Ayna: Scadex.Signalization/Enums/SignalEnums.cs
 *
 * Sayı olarak serileşir (JsonStringEnumConverter bilerek yok) ve numaralar BOŞLUKLUDUR:
 * `SessionEventType` içinde 16 yoktur (eski "uyarı onayı" — kaldırıldı, numara yeniden verilmez).
 */

// ─────────────────────────────────────────────────────────── oturum durumu

export const OperatorSessionStatus = {
  /** Dış kapı açık, işlem sürüyor. */
  Open: 1,
  /** Dış kapı kapandı, hiçbir uyarı bayrağı yok. */
  Completed: 2,
  /** Dış kapı kapandı ama en az bir bayrak var. */
  CompletedWithWarning: 3,
  /** Dış kapı kapanmadan azami süre doldu; oturumu zamanlayıcı kapattı. */
  TimedOut: 4
} as const;
export type OperatorSessionStatus = (typeof OperatorSessionStatus)[keyof typeof OperatorSessionStatus];

export const OperatorSessionStatusLabels: Record<OperatorSessionStatus, string> = {
  [OperatorSessionStatus.Open]: 'Sürüyor',
  [OperatorSessionStatus.Completed]: 'Tamamlandı',
  [OperatorSessionStatus.CompletedWithWarning]: 'Uyarıyla tamamlandı',
  [OperatorSessionStatus.TimedOut]: 'Zaman aşımı'
};

// ─────────────────────────────────────────────────────────── bayraklar

/** `[Flags]` — bir oturumda birden fazlası aynı anda olabilir; tip bu yüzden düz `number` (bit maskesi). */
export const SessionFlags = {
  None: 0,
  NoCardPresented: 1,
  UnauthorizedEntry: 2,
  InnerDoorLeftOpen: 4,
  ForcedOpen: 8,
  CommandFailed: 16,
  OuterOpenMissing: 32,
  TimedOut: 64
} as const;

/**
 * Güvenlik uyarısı sayılan bayraklar (sunucudaki `AlertFlags`): canlı panelde vurgulanır, raporda
 * filtrelenir. Onay akışı YOKTUR — bayrak oturum kaydında kalıcıdır.
 */
export const ALERT_FLAGS = SessionFlags.UnauthorizedEntry | SessionFlags.ForcedOpen;

export interface SessionFlagInfo {
  flag: number;
  label: string;
  description: string;
}

/** Görüntüleme sırası: önce güvenlik uyarıları. */
export const SESSION_FLAG_LIST: SessionFlagInfo[] = [
  { flag: SessionFlags.UnauthorizedEntry, label: 'Kartsız giriş', description: 'Dış kapı açıldıktan sonra süresinde yetkili kart okutulmadı.' },
  { flag: SessionFlags.ForcedOpen, label: 'Zorla açma', description: 'Kilitli bir iç kapının anahtarı "açık" gösterdi.' },
  { flag: SessionFlags.InnerDoorLeftOpen, label: 'İç kapı açık kaldı', description: 'Dış kapı kapandığında kilitsiz bir iç kapı açıktı; kilitlenemedi.' },
  { flag: SessionFlags.CommandFailed, label: 'Komut başarısız', description: 'Bir kilit ya da siren komutu başarısız oldu.' },
  { flag: SessionFlags.NoCardPresented, label: 'Kart okutulmadı', description: 'Oturum boyunca hiç yetkili kart okutulmadı.' },
  { flag: SessionFlags.OuterOpenMissing, label: 'Dış kapı açılışı yok', description: 'Oturum dış kapı açılışı gelmeden, kart ya da iç kapı olayıyla açıldı.' },
  { flag: SessionFlags.TimedOut, label: 'Zaman aşımı', description: 'Oturum dış kapı kapanmadan azami süre dolunca kapatıldı.' }
];

/** Maskedeki bayraklar, görüntüleme sırasıyla. */
export function flagsOf(mask: number): SessionFlagInfo[] {
  return SESSION_FLAG_LIST.filter(info => (mask & info.flag) !== 0);
}

export function isAlertFlag(flag: number): boolean {
  return (ALERT_FLAGS & flag) !== 0;
}

// ─────────────────────────────────────────────────────────── aşama

/** Açık oturumun aşaması. Sunucuda SAKLANMAZ, kapı ve operatör satırlarından türetilir. */
export const SessionPhase = {
  AwaitingCard: 1,
  Inside: 2,
  Exiting: 3
} as const;
export type SessionPhase = (typeof SessionPhase)[keyof typeof SessionPhase];

export const SessionPhaseLabels: Record<SessionPhase, string> = {
  [SessionPhase.AwaitingCard]: 'Kart bekleniyor',
  [SessionPhase.Inside]: 'İçeride',
  [SessionPhase.Exiting]: 'Çıkış'
};

// ─────────────────────────────────────────────────────────── olay türü

export const SessionEventType = {
  OuterOpened: 1,
  OuterClosed: 2,
  CardPresented: 3,
  AccessDenied: 4,
  Unlocked: 5,
  Locked: 6,
  AutoLocked: 7,
  LockSkippedDoorOpen: 8,
  InnerOpened: 9,
  InnerClosed: 10,
  ForcedOpen: 11,
  CommandFailed: 12,
  SnapshotTaken: 13,
  SnapshotFailed: 14,
  AwaitingCardTimedOut: 15,
  // 16 YOK (bkz. dosya başı)
  SirenRequested: 17,
  SirenReleased: 18
} as const;
export type SessionEventType = (typeof SessionEventType)[keyof typeof SessionEventType];

/**
 * `Record<number, …>`: geçmiş satırlarda artık adı olmayan bir numara (16) dönebilir; bilinmeyen tür
 * ekranı düşürmemeli, `#16` olarak görünmeli.
 */
export const SessionEventTypeLabels: Record<number, string> = {
  [SessionEventType.OuterOpened]: 'Dış kapı açıldı',
  [SessionEventType.OuterClosed]: 'Dış kapı kapandı',
  [SessionEventType.CardPresented]: 'Kart okutuldu',
  [SessionEventType.AccessDenied]: 'Erişim reddedildi',
  [SessionEventType.Unlocked]: 'Kilit açıldı',
  [SessionEventType.Locked]: 'Kilitlendi',
  [SessionEventType.AutoLocked]: 'Otomatik kilitlendi',
  [SessionEventType.LockSkippedDoorOpen]: 'Kilitlenemedi (kapı açık)',
  [SessionEventType.InnerOpened]: 'İç kapı açıldı',
  [SessionEventType.InnerClosed]: 'İç kapı kapandı',
  [SessionEventType.ForcedOpen]: 'Zorla açma',
  [SessionEventType.CommandFailed]: 'Komut başarısız',
  [SessionEventType.SnapshotTaken]: 'Kare çekildi',
  [SessionEventType.SnapshotFailed]: 'Kare çekilemedi',
  [SessionEventType.AwaitingCardTimedOut]: 'Kart süresi doldu',
  [SessionEventType.SirenRequested]: 'Siren talep edildi',
  [SessionEventType.SirenReleased]: 'Siren bırakıldı'
};

export type EventTone = 'default' | 'success' | 'warning' | 'danger' | 'muted';

/** Zaman çizelgesindeki nokta rengi. */
export function eventTone(type: number): EventTone {
  switch (type) {
    case SessionEventType.AccessDenied:
    case SessionEventType.ForcedOpen:
    case SessionEventType.CommandFailed:
    case SessionEventType.AwaitingCardTimedOut:
      return 'danger';
    case SessionEventType.LockSkippedDoorOpen:
    case SessionEventType.SnapshotFailed:
    case SessionEventType.SirenRequested:
      return 'warning';
    case SessionEventType.Unlocked:
    case SessionEventType.Locked:
    case SessionEventType.AutoLocked:
      return 'success';
    case SessionEventType.SnapshotTaken:
      return 'muted';
    default:
      return 'default';
  }
}

// ─────────────────────────────────────────────────────────── olay ayrıntısı

/** Ayna: `SessionEventDetail` sabitleri — sunucu anahtar yazar, ekran çevirir. */
const SessionEventDetailLabels: Record<string, string> = {
  // AccessDenied gerekçeleri
  UnknownCard: 'Tanımsız kart',
  NoAuthority: 'Kullanıcının kurumu yok',
  MultipleAuthorities: 'Kullanıcının birden fazla kurumu var',
  NoDoorForAuthority: 'Kurumun bu dış kapının ardında iç kapısı yok',
  // SirenReleased gerekçeleri
  UnlockedAgain: 'Bir iç kapı yeniden açıldı',
  OuterClosed: 'Dış kapı kapandı',
  Timeout: 'Siren süresi doldu',
  SessionTimedOut: 'Oturum zaman aşımı',
  // LockSkippedDoorOpen gerekçeleri
  SwitchUnknown: 'Anahtar durumu bilinmiyor',
  SessionEnd: 'Oturum sonu',
  // CommandFailed hedefleri
  Lock: 'Kilit',
  Siren: 'Siren'
};

/**
 * Olay ayrıntısını okunur metne çevirir. Sunucu üç biçim yazar: düz anahtar (`UnknownCard`),
 * `Anahtar: mesaj` (`Lock: Timeout: …`) ve kare olaylarında `sıra` / `sıra: sebep`.
 */
export function formatEventDetail(type: number, detail: string | null): string | null {
  if (!detail) return null;

  if (type === SessionEventType.SnapshotTaken || type === SessionEventType.SnapshotFailed) return `Kare ${detail}`;

  const direct = SessionEventDetailLabels[detail];
  if (direct) return direct;

  const separator = detail.indexOf(': ');
  if (separator > 0) {
    const head = SessionEventDetailLabels[detail.slice(0, separator)];
    if (head) return `${head}: ${detail.slice(separator + 2)}`;
  }

  return detail;
}
