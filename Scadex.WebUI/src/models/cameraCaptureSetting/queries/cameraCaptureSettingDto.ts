/** Ayna: Scadex.Model/Dtos/CameraCaptureSetting/Queries/CameraCaptureSettingDto.cs */

/**
 * Kamera çekim (anlık görüntü + klip) ayarları.
 *
 * Medya geçidi ayarlarıyla aynı yerde durur ama AYRI bir tablo ve ayrı bir servistir
 * (`ICameraCaptureSettingService`): ayar nesnesi başına bir servis kuralı gereği.
 * Kaydedildiği an etkilidir.
 */
export interface CameraCaptureSettingDto {
  /** Kameradan ISAPI ile anlık görüntü çekerken beklenecek süre. */
  snapshotTimeoutMs: number;
  /** Aynı kameranın anlık görüntüsünün önbellekte tutulma süresi. `0` = önbellek yok. */
  snapshotCacheSeconds: number;
  /** `wwwroot` ALTINDA göreli bir yol — mutlak yol veya `..` kabul edilmez. */
  captureRoot: string;

  /** Çekimin saklanma süresi (gün). `0` = süresiz. */
  captureRetentionDays: number;

  /** Tek bir klibin isteyebileceği en uzun süre. */
  maxClipDurationSec: number;
  /** MediaMTX'in dosyayı kapatması için klip bittikten sonra beklenen pay. */
  clipFinalizeGraceMs: number;
}
