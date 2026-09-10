import http from '@/lib/axios-helper';
import type { CameraCreateRequest, CameraDto, CameraProbeResultRequest, CameraUpdateRequest } from '@/models/camera';
import type { CreatedDto } from '@/models/common/createdDto';

const CAMERA_ROUTE = '/api/Camera';

/**
 * Bir kabindeki kameralar.
 *
 * `includePassive` varsayılan olarak KAPALI. Pasifleri görebilmek şart —
 * `IsActive` üzerinde global query filter bilerek yok ve pasife alınan bir
 * kaydı geri getirmenin başka yolu bulunmuyor — ama liste ekranının varsayılanı
 * aktifler olmalı.
 */
export async function getCamerasByCabinet(cabinetId: string, includePassive = false): Promise<CameraDto[]> {
  const query = includePassive ? '?includePassive=true' : '';
  return http.get<CameraDto[]>(`${CAMERA_ROUTE}/cabinet/${cabinetId}${query}`);
}

export async function getCamera(id: string): Promise<CameraDto> {
  return http.get<CameraDto>(`${CAMERA_ROUTE}/${id}`);
}

/** Başarıda `{ id }` döner — sözleşmenin "her Create id döndürür" kuralı. */
export async function createCamera(request: CameraCreateRequest): Promise<CreatedDto> {
  return http.post<CreatedDto>(CAMERA_ROUTE, request);
}

/**
 * Başarıda gövdesiz 200 döner.
 *
 * PASİFE ALMA DA BURADAN: `isActive: false` gönderilir. Ayrı bir DELETE ucu
 * yok — `Camera` `IActivatableEntity` ve fiziksel silme sunucuda exception atar.
 *
 * **Parola üç durumlu.** Kullanıcı parola alanına dokunmadıysa çağıran taraf
 * `password` alanını gövdeden ÇIKARMALIDIR (`undefined`); aksi hâlde boş string
 * gider ve parola silinir. Okuma DTO'su parolayı hiç döndürmediği için form her
 * açılışta boş gelir — bu ayrım olmasaydı her düzenleme parolayı uçururdu.
 */
export async function updateCamera(request: CameraUpdateRequest): Promise<void> {
  return http.put(CAMERA_ROUTE, request);
}

/**
 * Bir yoklama denemesinin sonucunu yazar.
 *
 * Bunu çağıran arka plan servisi HENÜZ YAZILMADI — kullanıcı kendisi yazacak.
 * Fonksiyon, arayüzden elle "şimdi dene" gibi bir akış istendiğinde ya da o
 * servis tarayıcı tarafından tetiklendiğinde kullanılmak üzere burada.
 */
export async function recordCameraProbeResult(id: string, request: CameraProbeResultRequest): Promise<void> {
  return http.post(`${CAMERA_ROUTE}/${id}/probe-result`, request);
}
