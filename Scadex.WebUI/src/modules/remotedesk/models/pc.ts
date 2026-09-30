/**
 * RemoteDesk DTO'larının ELLE yazılmış TS aynası (backend: `Scadex.RemoteDesk/Model/Dtos`). C# tarafı değişince burası da
 * değişmeli — codegen yok. Enum'lar sayı olarak gelir.
 */

/** Sunucudaki `Contracts.Hub.ScreenStreamState` — istemcinin son bildirdiği yayın durumu. */
export const ScreenStreamState = {
  Starting: 1,
  Streaming: 2,
  /** FFmpeg düştü, PC yeniden deniyor. */
  Retrying: 3,
  /** PC'nin ekranı kilitli / güvenli masaüstünde (UAC); kilit açılınca kendiliğinden sürer. */
  ScreenLocked: 4,
  Failed: 5,
  Stopped: 6
} as const;
export type ScreenStreamState = (typeof ScreenStreamState)[keyof typeof ScreenStreamState];

export const ScreenStreamStateLabels: Record<ScreenStreamState, string> = {
  [ScreenStreamState.Starting]: 'Başlıyor',
  [ScreenStreamState.Streaming]: 'Yayında',
  [ScreenStreamState.Retrying]: 'Yeniden deneniyor',
  [ScreenStreamState.ScreenLocked]: 'Ekran kilitli',
  [ScreenStreamState.Failed]: 'Başarısız',
  [ScreenStreamState.Stopped]: 'Durdu'
};

/** `GET /api/RemoteDesk/pcs` satırı. Ad/kabin/MAC çekirdekteki cihazdan; bağlantı alanları sunucunun belleğinden. */
export interface PcListItemDto {
  deviceId: string;
  deviceName: string;
  cabinetId: string;
  cabinetName: string;
  /** `null` = MAC tanımlı değil: bu PC hiçbir istemciyle eşleşemez. */
  macAddress: string | null;
  isConnected: boolean;
  clientVersion: string | null;
  monitorCount: number;
  viewerCount: number;
}

export interface PcMonitorDto {
  /** İzleme ucu bu numarayı alır. */
  index: number;
  deviceName: string;
  width: number;
  height: number;
  isPrimary: boolean;
  gpuName: string;
  /** Yayın yoksa `null`. */
  streamState: ScreenStreamState | null;
  encoder: string | null;
  viewerCount: number;
}

/** `GET /api/RemoteDesk/pcs/{deviceId}`. */
export interface PcDetailDto extends PcListItemDto {
  machineName: string | null;
  userName: string | null;
  osVersion: string | null;
  /** UTC; `Z` soneki olmayabilir — `toUtcDate` / `formatUtcDateTime` ile okuyun. */
  connectedUtc: string | null;
  /** Bağlı değilken boş. */
  monitors: PcMonitorDto[];
}

/**
 * `POST …/monitors/{index}/view`. `whepUrl` / `token` / `expirationUtc` kameranın `StreamTokenDto`'suyla aynı adlarda: aynı
 * WHEP oynatıcısı kullanılır. Kiralama `leaseRenewSec`'te bir yenilenmezse sunucu düşürür.
 */
export interface ScreenViewDto {
  viewId: string;
  whepUrl: string;
  token: string;
  expirationUtc: string;
  leaseRenewSec: number;
}
