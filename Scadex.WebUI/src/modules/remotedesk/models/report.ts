/**
 * Denetim raporu DTO'larının ELLE yazılmış TS aynası (backend: `Scadex.RemoteDesk/Model/Dtos/Report/Queries`). C# tarafı
 * değişince burası da değişmeli — codegen yok. `...Utc` damgalarında `Z` YOK: gösterirken `toUtcDate`/`formatUtcDateTime`.
 */

/** Sunucudaki `RemoteDeskEnums.RemoteControlEndReason` — sayılar birebir aynı. */
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
  [RemoteControlEndReason.Released]: 'Bırakıldı',
  [RemoteControlEndReason.ViewerDisconnected]: 'Tarayıcı bağlantısı koptu',
  [RemoteControlEndReason.PcDisconnected]: 'PC bağlantısı koptu',
  [RemoteControlEndReason.Idle]: 'Uzun süre işlem yok',
  [RemoteControlEndReason.ServerRestart]: 'Sunucu yeniden başladı',
  [RemoteControlEndReason.ViewEnded]: 'İzleme bitti',
  [RemoteControlEndReason.Replaced]: 'Başka sekmeye geçti'
};

/** Ortak filtre — `POST .../reports/{views|controls|summary}` gövdesi. Tarih aralığı kaydın başlangıcına uygulanır. */
export interface RemoteAccessReportQuery {
  deviceId: string | null;
  /** PC'nin BUGÜN bağlı olduğu kabin — sunucu kabindeki cihazları çekirdekten çözer. */
  cabinetId: string | null;
  userId: string | null;
  fromUtc: string | null;
  toUtc: string | null;
  page: number;
  pageSize: number;
}

/** İzleme kaydı. Ad alanları `null` = cihaz/kullanıcı çekirdekte artık yok. */
export interface ScreenViewReportItemDto {
  id: number;
  deviceId: string;
  deviceName: string | null;
  cabinetName: string | null;
  monitorIndex: number;
  userId: string;
  userFullName: string | null;
  userName: string | null;
  startedUtc: string;
  /** `null` = hâlâ izliyor. */
  endedUtc: string | null;
}

/** Uzaktan kontrol kaydı — girdi sayısı bilerek yok. */
export interface RemoteControlReportItemDto {
  id: string;
  deviceId: string;
  deviceName: string | null;
  cabinetName: string | null;
  userId: string;
  userFullName: string | null;
  userName: string | null;
  startedUtc: string;
  /** `null` = hâlâ kontrol ediyor. */
  endedUtc: string | null;
  endReason: RemoteControlEndReason | null;
}

export interface ReportPcTotalDto {
  deviceId: string;
  deviceName: string | null;
  cabinetName: string | null;
  seconds: number;
}

export interface ReportUserTotalDto {
  userId: string;
  fullName: string | null;
  userName: string | null;
  seconds: number;
}

/** Özet kartları — süreler saniye, açık kayıt "şu ana kadar". */
export interface RemoteAccessSummaryDto {
  viewCount: number;
  viewSeconds: number;
  controlCount: number;
  controlSeconds: number;
  topPc: ReportPcTotalDto | null;
  topUser: ReportUserTotalDto | null;
}

export interface ReportPcOptionDto {
  deviceId: string;
  deviceName: string | null;
  /** Kabin filtresinin seçenekleri buradan türetilir. `null` = cihaz çekirdekte artık yok. */
  cabinetId: string | null;
  cabinetName: string | null;
}

export interface ReportUserOptionDto {
  userId: string;
  fullName: string | null;
  userName: string | null;
}

/** `GET .../reports/filters` — kayıtlarda geçen PC'ler ve kullanıcılar (silinmişler dahil). */
export interface RemoteAccessFilterOptionsDto {
  pcs: ReportPcOptionDto[];
  users: ReportUserOptionDto[];
}
