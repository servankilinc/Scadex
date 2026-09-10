/**
 * Ayna: Scadex.Model/Dtos/Camera/Queries/CameraDto.cs
 * Sözleşme: docs/api-contract/11-camera.md
 */
export interface CameraDto {
  id: string;
  cabinetId: string;
  cabinetName: string | null;
  name: string;
  description: string | null;
  manufacturer: string | null;
  model: string | null;

  ipAddress: string;
  rtspPort: number;
  httpPort: number;
  httpsPort: number | null;

  username: string | null;
  /**
   * Kamera parolası — DÜZ METİN olarak döner (kullanıcı kararı).
   *
   * Önceki sözleşmede yerine `hasPassword: boolean` gidiyordu; sistem kapalı
   * ağda çalıştığı ve bu aşamada kamera kimlik bilgilerinin gizlenmesi
   * istenmediği için doğrudan döndürülüyor.
   *
   * Yazma yolu YİNE ÜÇ DURUMLU: `undefined` = dokunma, `''` = sil, dolu = değiştir.
   */
  password: string | null;

  mainStreamChannel: number;
  subStreamChannel: number;
  mainStreamEnabled: boolean;
  subStreamEnabled: boolean;
  snapshotChannel: number;

  monitoringPort: number | null;
  /**
   * `DeviceStatus` lookup FK — Device/Cabinet ile AYNI sözlük.
   * `null` = **hiç yoklanmadı**; `Offline` (0) ile aynı şey DEĞİL.
   */
  deviceStatusId: number | null;
  deviceStatusName: string | null;
  lastSeen: string | null;
  pingIntervalSec: number;
  isMonitoringEnabled: boolean;
  lastConnectionError: string | null;

  isActive: boolean;
  createDateUtc: string | null;
  updateDateUtc: string | null;
}
