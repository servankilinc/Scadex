import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  getCameraCaptureSetting,
  getMediaGatewaySetting,
  updateCameraCaptureSetting,
  updateMediaGatewaySetting
} from '@/api/setting';
import { settingKeys } from '@/api/query-keys';
import type { CameraCaptureSettingUpdateRequest } from '@/models/cameraCaptureSetting';
import type { MediaGatewaySettingUpdateRequest } from '@/models/mediaGatewaySetting';

/**
 * Ayarlar nadiren değişir ama değiştiğinde ANINDA etkilidir: sunucu her yazmada kendi
 * `ICacheService` anahtarını düşürüyor ve `MediaMtxGateway` adresi/zaman aşımını her
 * çağrıda yeniden okuyor. Bu yüzden burada uzun bir `staleTime` YOK — bayat bir ayarı
 * ekranda göstermek, kullanıcının başka bir sekmede yaptığı değişikliği ezmesine yol
 * açardı.
 *
 * `onError` BİLEREK yok: hata politikası çağıran tarafta (`handleFormApiError`).
 */
export function useMediaGatewaySetting() {
  return useQuery({
    queryKey: settingKeys.mediaGateway(),
    queryFn: getMediaGatewaySetting
  });
}

export function useUpdateMediaGatewaySetting() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: MediaGatewaySettingUpdateRequest) => updateMediaGatewaySetting(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: settingKeys.mediaGateway() });
      toast.success('Medya geçidi ayarları kaydedildi.');
    }
  });
}

export function useCameraCaptureSetting() {
  return useQuery({
    queryKey: settingKeys.cameraCapture(),
    queryFn: getCameraCaptureSetting
  });
}

export function useUpdateCameraCaptureSetting() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CameraCaptureSettingUpdateRequest) => updateCameraCaptureSetting(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: settingKeys.cameraCapture() });
      toast.success('Kamera çekim ayarları kaydedildi.');
    }
  });
}
