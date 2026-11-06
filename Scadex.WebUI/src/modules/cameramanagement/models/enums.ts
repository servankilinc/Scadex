/**
 * CameraManagement modülünün enum'ları — sunucudaki `Scadex.CameraManagement.Model.Enums.CameraManagementEnums` aynası.
 * Kameranın cihaz enum'ları (`CameraBrand`, `StreamProfile`, `DeviceType.Camera`) çekirdekte kalır (`@/models/enums/entityEnums`).
 */

/**
 * Merkeze alınan görüntünün cinsi. 0 değeri YOKTUR.
 *
 * İkisi de sunucuda FFmpeg ile MediaMTX'in ana akım yolundan alınır:
 * `Snapshot` → JPEG, `Clip` → sessiz MP4 (yeniden kodlama yok).
 */
export const CaptureType = {
  Snapshot: 1,
  Clip: 2
} as const;
export type CaptureType = (typeof CaptureType)[keyof typeof CaptureType];

export const CaptureTypeLabels: Record<CaptureType, string> = {
  [CaptureType.Snapshot]: 'Anlık Görüntü',
  [CaptureType.Clip]: 'Klip'
};

/**
 * Çekimin akıbeti. 0 değeri YOKTUR.
 *
 * `Failed` de bir SATIR bırakır: "o anda görüntü YOK" bilgisinin kendisi delildir.
 */
export const CaptureStatus = {
  Pending: 1,
  Available: 2,
  Failed: 3
} as const;
export type CaptureStatus = (typeof CaptureStatus)[keyof typeof CaptureStatus];

export const CaptureStatusLabels: Record<CaptureStatus, string> = {
  [CaptureStatus.Pending]: 'Hazırlanıyor',
  [CaptureStatus.Available]: 'Hazır',
  [CaptureStatus.Failed]: 'Başarısız'
};
