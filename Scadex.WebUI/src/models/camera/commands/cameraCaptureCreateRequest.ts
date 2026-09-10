/**
 * Ayna: Scadex.Model/Dtos/Camera/Commands/CameraCaptureCreateDto.cs
 *       (+ CameraCaptureCreateDtoValidator)
 * Sözleşme: docs/api-contract/11-camera.md
 */
import type { CaptureType } from '@/models/enums/entityEnums';

export interface CameraCaptureCreateRequest {
  type: CaptureType;

  /**
   * Klip süresi (saniye).
   *
   * `Clip` için ZORUNLU, `Snapshot` için `null`/`undefined` OLMALI — sunucu
   * ikisini de doğruluyor ve tek karenin süresi olması anlamsız olurdu.
   */
  durationSec?: number | null;
}
