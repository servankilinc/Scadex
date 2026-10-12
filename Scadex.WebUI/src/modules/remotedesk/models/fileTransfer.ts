/**
 * Dosya transferi (RemoteDesk.md § 13) sözleşmesinin ELLE yazılmış TS aynası. Viewer hub DTO'ları `Model/Dtos/FileTransfer`,
 * enum'lar `Enums/RemoteDeskEnums.cs`. Enum'lar sayı olarak taşınır. Dosya baytları hub'dan DEĞİL, biletli HTTP'den akar.
 */

export const FileTransferDirection = {
  /** Tarayıcıdan PC'ye (yükleme). */
  ToPc: 1,
  /** PC'den tarayıcıya (indirme). */
  FromPc: 2
} as const;
export type FileTransferDirection = (typeof FileTransferDirection)[keyof typeof FileTransferDirection];

export const FileTransferStatus = {
  CentralReceiving: 1,
  ReadyForPc: 2,
  PcSending: 3,
  ReadyForBrowser: 4,
  Completed: 5,
  Failed: 6
} as const;
export type FileTransferStatus = (typeof FileTransferStatus)[keyof typeof FileTransferStatus];

export interface DirEntryDto {
  name: string;
  isDirectory: boolean;
  size: number;
  /** UTC; `Z` soneki olmayabilir — `toUtcDate`/`formatUtcDateTime`. */
  modifiedUtc: string;
}

/** Viewer hub `ListPcDirectory` yanıtı. */
export interface ListDirectoryResponse {
  allowed: boolean;
  message: string | null;
  path: string;
  isRoot: boolean;
  /** `''` = sürücü köklerine dön; `null` = üst yok. */
  parentPath: string | null;
  entries: DirEntryDto[];
  error: string | null;
}

/** Viewer hub `RequestDownload`/`RequestUpload` yanıtı. */
export interface FileTransferResponse {
  accepted: boolean;
  transferId: string | null;
  message: string | null;
}

/** Sunucu → tarayıcı: transfer durumu değişti. */
export interface FileTransferStateNotice {
  transferId: string;
  status: FileTransferStatus;
  fileName: string;
  bytes: number;
  error: string | null;
}

/** `reports/transfers` satırı (`FileTransferLog`). */
export interface FileTransferReportItemDto {
  id: string;
  deviceId: string;
  deviceName: string | null;
  cabinetName: string | null;
  userId: string;
  userFullName: string | null;
  userName: string | null;
  direction: FileTransferDirection;
  fileName: string;
  path: string;
  sizeBytes: number;
  /** UTC; `Z` yok. */
  startedUtc: string;
  endedUtc: string | null;
  success: boolean | null;
  error: string | null;
}
