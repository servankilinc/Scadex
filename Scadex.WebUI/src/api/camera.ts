import http from '@/lib/axios-helper';
import type { CameraDto, CameraUpdateRequest } from '@/models/camera';

const CAMERA_ROUTE = '/api/Camera';

/**
 * Bir kabindeki kameralar.
 *
 * `includePassive` varsayılan olarak KAPALI. Pasif (diyagramdan kaldırılmış)
 * kameralar çekim geçmişi için görülebilir — `IsActive` üzerinde global query
 * filter bilerek yok — ama liste ekranının varsayılanı aktifler olmalı.
 */
export async function getCamerasByCabinet(cabinetId: string, includePassive = false): Promise<CameraDto[]> {
  const query = includePassive ? '?includePassive=true' : '';
  return http.get<CameraDto[]>(`${CAMERA_ROUTE}/cabinet/${cabinetId}${query}`);
}

export async function getCamera(id: string): Promise<CameraDto> {
  return http.get<CameraDto>(`${CAMERA_ROUTE}/${id}`);
}

/**
 * Başarıda gövdesiz 200 döner. Yalnızca AKTİF kamerayı günceller (pasifte 404).
 *
 * Oluşturma ve kaldırma (pasife alma) bu ekranda YOKTUR — kamera bir cihazdır ve
 * cihazın yaşam döngüsü yalnızca diyagram kaydetmeden geçer (2026-10-25).
 *
 * **Parola üç durumlu.** Kullanıcı parola alanına dokunmadıysa çağıran taraf
 * `password` alanını gövdeden ÇIKARMALIDIR (`undefined`); aksi hâlde boş string
 * gider ve parola silinir. Okuma DTO'su parolayı hiç döndürmediği için form her
 * açılışta boş gelir — bu ayrım olmasaydı her düzenleme parolayı uçururdu.
 */
export async function updateCamera(request: CameraUpdateRequest): Promise<void> {
  return http.put(CAMERA_ROUTE, request);
}
