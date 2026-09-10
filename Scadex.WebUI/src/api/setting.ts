import http from '@/lib/axios-helper';
import type { CameraCaptureSettingDto, CameraCaptureSettingUpdateRequest } from '@/models/cameraCaptureSetting';
import type { MediaGatewaySettingDto, MediaGatewaySettingUpdateRequest } from '@/models/mediaGatewaySetting';

/**
 * Sistem ayarları.
 *
 * **Ayar nesnesi başına ayrı bir uç vardır ve öyle kalmalı** — sunucuda da tip başına
 * tek satırlık bir tablo ve ayrı bir servis var (`IMediaGatewaySettingService`,
 * `ICameraCaptureSettingService`). Hepsini tek bir `/api/Setting` altında toplamak,
 * ilgisiz iki ayar grubunu tek yazma yoluna bağlamak olurdu.
 *
 * Kimlik yok: her uç tek satırlıdır, `SingleRowId = 1` sunucuda sabittir.
 */
const MEDIA_GATEWAY_ROUTE = '/api/MediaGatewaySetting';
const CAMERA_CAPTURE_ROUTE = '/api/CameraCaptureSetting';

export async function getMediaGatewaySetting(): Promise<MediaGatewaySettingDto> {
  return http.get<MediaGatewaySettingDto>(MEDIA_GATEWAY_ROUTE);
}

/** Başarıda gövdesiz 200. Sunucu kendi önbelleğini düşürür; etkisi anında görünür. */
export async function updateMediaGatewaySetting(request: MediaGatewaySettingUpdateRequest): Promise<void> {
  return http.put(MEDIA_GATEWAY_ROUTE, request);
}

export async function getCameraCaptureSetting(): Promise<CameraCaptureSettingDto> {
  return http.get<CameraCaptureSettingDto>(CAMERA_CAPTURE_ROUTE);
}

export async function updateCameraCaptureSetting(request: CameraCaptureSettingUpdateRequest): Promise<void> {
  return http.put(CAMERA_CAPTURE_ROUTE, request);
}
