/**
 * Ayna: Scadex.Model/Dtos/Camera/Queries/CameraCaptureDto.cs
 * Sözleşme: docs/api-contract/11-camera.md
 */
import type { CaptureStatus, CaptureType } from '@/models/enums/entityEnums';

export interface CameraCaptureDto {
  /** IDENTITY PK — `number`, `string` değil. Guid olmadığına dikkat. */
  id: number;
  cameraId: string;

  type: CaptureType;
  status: CaptureStatus;

  /**
   * Görüntünün ANI (ISO-8601 UTC) — satırın oluşturulma zamanı değil.
   * Klipte kaydın fiilen başladığı andır.
   */
  capturedAtUtc: string;

  /** Klip süresi (saniye); anlık görüntüde `null`. */
  durationSec: number | null;

  /**
   * Dosyanın sunucu köküne göre GÖRELİ yolu (örn.
   * `uploads/captures/2026/08/31/{guid}.jpg`).
   *
   * Tam URL DEĞİLDİR — `captureFileUrl()` ile `API_BASE_URL`'e eklenir.
   * `Pending` ve `Failed` iken `null`.
   */
  relativePath: string | null;

  sizeBytes: number | null;

  /** `Failed` ise sebep. Başarısız çekim de bir satır bırakır. */
  failureReason: string | null;

  /** Saklama süresinin sonu; `null` ise süresiz. */
  expiresAt: string | null;

  requestedByUserId: string | null;
}
