import http from '@/lib/axios-helper';
import type { CameraCaptureSettingDto, CameraCaptureSettingUpdateRequest } from '@/models/cameraCaptureSetting';

/**
 * Sistem ayarları.
 *
 * **Ayar nesnesi başına ayrı bir uç vardır ve öyle kalmalı** — sunucuda da tip başına
 * tek satırlık bir tablo ve ayrı bir servis var (`ICameraCaptureSettingService`).
 * Medya geçidi (MediaMTX) ayarları burada DEĞİL, sunucunun `appsettings.json > MediaGateway`
 * bölümündedir; `mediamtx.yml` ile elle senkron tutulduğu için ekrandan değiştirilmez.
 *
 * Kimlik yok: her uç tek satırlıdır, `SingleRowId = 1` sunucuda sabittir.
 */
const CAMERA_CAPTURE_ROUTE = '/api/CameraCaptureSetting';

export async function getCameraCaptureSetting(): Promise<CameraCaptureSettingDto> {
  return http.get<CameraCaptureSettingDto>(CAMERA_CAPTURE_ROUTE);
}

/** Başarıda gövdesiz 200. Sunucu kendi önbelleğini düşürür; etkisi anında görünür. */
export async function updateCameraCaptureSetting(request: CameraCaptureSettingUpdateRequest): Promise<void> {
  return http.put(CAMERA_CAPTURE_ROUTE, request);
}
