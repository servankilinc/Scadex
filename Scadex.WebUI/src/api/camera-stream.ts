import http, { API_BASE_URL } from '@/lib/axios-helper';
import type { CameraCaptureCreateRequest } from '@/models/camera/commands/cameraCaptureCreateRequest';
import type { CameraCaptureDto } from '@/models/camera/queries/cameraCaptureDto';
import type { StreamTokenDto } from '@/models/camera/queries/streamTokenDto';
import type { StreamProfile } from '@/models/enums/entityEnums';

const CAMERA_ROUTE = '/api/Camera';

/**
 * Canlı izleme bileti alır.
 *
 * **GET değil POST**: bilet almak bir yan etkidir — sunucu medya geçidinde
 * yolu kurar ve önbelleğe kayıt yazar.
 *
 * Dönen gövdede RTSP adresi ya da kamera parolası YOKTUR ve olmayacaktır.
 */
export async function createStreamTicket(cameraId: string, profile: StreamProfile): Promise<StreamTokenDto> {
  return http.post<StreamTokenDto>(`${CAMERA_ROUTE}/${cameraId}/stream-ticket?profile=${profile}`, undefined);
}

/**
 * Anlık görüntüyü ikili olarak çeker. **Satır yazmaz.**
 *
 * `responseType: 'blob'` şart: varsayılan ayrıştırıcı JPEG baytlarını metin
 * sanıp bozardı.
 *
 * @param fresh Sunucudaki kısa ömürlü önbelleği atlar.
 */
export async function fetchSnapshotBlob(cameraId: string, fresh = false): Promise<Blob> {
  return http.get<Blob>(`${CAMERA_ROUTE}/${cameraId}/snapshot${fresh ? '?fresh=true' : ''}`, {
    responseType: 'blob'
  });
}

/**
 * Delil çekimi başlatır.
 *
 * Anlık görüntü `Available`/`Failed` olarak döner; klip `Pending` döner ve
 * arka planda sürer — çağıran tarafın listeyi yoklaması gerekir
 * (bkz. `useCaptures`).
 *
 * **Başarısız çekim de 200 döner**: istek geçerliydi ve bir satır oluştu;
 * başarısızlık `status` alanındadır.
 */
export async function createCapture(cameraId: string, request: CameraCaptureCreateRequest): Promise<CameraCaptureDto> {
  return http.post<CameraCaptureDto>(`${CAMERA_ROUTE}/${cameraId}/capture`, request);
}

export async function getCaptures(cameraId: string, take = 20): Promise<CameraCaptureDto[]> {
  return http.get<CameraCaptureDto[]>(`${CAMERA_ROUTE}/${cameraId}/captures?take=${take}`);
}

/**
 * `relativePath`'i tarayıcının açabileceği tam adrese çevirir.
 *
 * Sunucu göreli yol döner (depo taşındığında binlerce satır güncellenmesin
 * diye); kökü birleştirmek istemcinin işi.
 */
export function captureFileUrl(relativePath: string): string {
  return `${API_BASE_URL.replace(/\/$/, '')}/${relativePath.replace(/^\//, '')}`;
}
