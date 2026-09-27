/** Ayna: Scadex.Model/Dtos/CameraCaptureSetting/Queries/CameraCaptureSettingDto.cs */

/**
 * Kamera çekim (anlık görüntü + klip) ayarları. İkisi de sunucuda FFmpeg ile MediaMTX'in
 * ana akım yolundan alınır; marka API'si kullanılmaz.
 *
 * Medya geçidi ayarlarıyla aynı yerde durur ama AYRI bir tablo ve ayrı bir servistir
 * (`ICameraCaptureSettingService`): ayar nesnesi başına bir servis kuralı gereği.
 * Kaydedildiği an etkilidir.
 */
export interface CameraCaptureSettingDto {
  /**
   * FFmpeg'in MediaMTX'ten ilk kareyi alması için azami süre (kimse izlemiyorsa MediaMTX'in
   * kameraya bağlanması + ilk anahtar kare). Klipte kayıt süresinin üzerine pay olarak da eklenir.
   */
  snapshotTimeoutMs: number;
  /** Aynı kameranın anlık görüntüsünün önbellekte tutulma süresi. `0` = önbellek yok. */
  snapshotCacheSeconds: number;
  /** `wwwroot` ALTINDA göreli bir yol — mutlak yol veya `..` kabul edilmez. */
  captureRoot: string;

  /** Çekimin saklanma süresi (gün). `0` = süresiz. */
  captureRetentionDays: number;

  /** Tek bir klibin isteyebileceği en uzun süre. */
  maxClipDurationSec: number;
  /** Kayıt süresi dolduktan sonra FFmpeg'in MP4'ü kapatması için tanınan ek pay. */
  clipFinalizeGraceMs: number;
}
