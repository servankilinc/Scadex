/**
 * Uzaktan kontrol (Faz 8) sözleşmesinin ELLE yazılmış TS aynası. Olay modeli `Scadex.RemoteDesk.Contracts/Hub/RemoteInput.cs`,
 * hub yanıtları `Scadex.RemoteDesk/Model/Dtos/Control` ve `Enums/RemoteDeskEnums.cs`. Enum'lar sayı olarak taşınır.
 */

export const InputEventType = {
  Move: 1,
  Down: 2,
  Up: 3,
  Wheel: 4,
  /** Faz 9 — sunucu henüz iletmez. */
  KeyDown: 5,
  /** Faz 9 — sunucu henüz iletmez. */
  KeyUp: 6
} as const;
export type InputEventType = (typeof InputEventType)[keyof typeof InputEventType];

/** DOM `MouseEvent.button` ile aynı numaralar. */
export const MouseButton = {
  Left: 0,
  Middle: 1,
  Right: 2
} as const;
export type MouseButton = (typeof MouseButton)[keyof typeof MouseButton];

export interface InputEvent {
  seq: number;
  /** Unix ms. */
  t: number;
  type: InputEventType;
  /** [0, 1] — izlenen monitöre göre (letterbox payı çıkarılmış). */
  x?: number;
  y?: number;
  button?: MouseButton;
  /** Windows birimi (120 = bir çentik), DOM yönü (pozitif = sağa). */
  deltaX?: number;
  /** Windows birimi (120 = bir çentik), DOM yönü (pozitif = aşağı). */
  deltaY?: number;
}

export interface InputBatch {
  controlSessionId: string;
  monitorIndex: number;
  events: InputEvent[];
}

export const ControlRequestStatus = {
  Granted: 1,
  Busy: 2,
  NotViewing: 3,
  PcNotConnected: 4,
  NotFound: 5
} as const;
export type ControlRequestStatus = (typeof ControlRequestStatus)[keyof typeof ControlRequestStatus];

/** Viewer hub `RequestControl` yanıtı. Red de bir yanıttır; `message` gösterilir. */
export interface RequestControlResponse {
  status: ControlRequestStatus;
  controlSessionId: string | null;
  controllerName: string | null;
  message: string | null;
  idleTimeoutSec: number;
}

export const RemoteControlEndReason = {
  Released: 1,
  ViewerDisconnected: 2,
  PcDisconnected: 3,
  Idle: 4,
  ServerRestart: 5,
  ViewEnded: 6,
  Replaced: 7
} as const;
export type RemoteControlEndReason = (typeof RemoteControlEndReason)[keyof typeof RemoteControlEndReason];

export const RemoteControlEndReasonLabels: Record<RemoteControlEndReason, string> = {
  [RemoteControlEndReason.Released]: 'Kontrol bırakıldı.',
  [RemoteControlEndReason.ViewerDisconnected]: 'Bağlantı koptu, kontrol bitti.',
  [RemoteControlEndReason.PcDisconnected]: "PC'nin bağlantısı koptu, kontrol bitti.",
  [RemoteControlEndReason.Idle]: 'Uzun süre işlem yapılmadığı için kontrol bırakıldı.',
  [RemoteControlEndReason.ServerRestart]: 'Sunucu yeniden başladı, kontrol bitti.',
  [RemoteControlEndReason.ViewEnded]: 'İzleme bittiği için kontrol bırakıldı.',
  [RemoteControlEndReason.Replaced]: 'Kontrol başka bir sekmeden alındı.'
};

/** Sunucu → tarayıcı: kontrol sunucu tarafında bitti. */
export interface ControlEndedNotice {
  controlSessionId: string;
  reason: RemoteControlEndReason;
}

/** `PcDetailDto.control` — PC'yi şu an kontrol eden kullanıcı. */
export interface PcControlDto {
  userId: string;
  userName: string;
  /** UTC; `Z` soneki olmayabilir — `toUtcDate` / `formatUtcDateTime` ile okuyun. */
  startedUtc: string;
}
