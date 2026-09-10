import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createCapture, getCaptures } from '@/api/camera-stream';
import { cameraKeys } from '@/api/query-keys';
import type { CameraCaptureCreateRequest } from '@/models/camera/commands/cameraCaptureCreateRequest';
import type { CameraCaptureDto } from '@/models/camera/queries/cameraCaptureDto';
import { CaptureStatus, CaptureType } from '@/models/enums/entityEnums';

/** `Pending` bir çekim varken listenin yoklanma sıklığı. */
const PENDING_POLL_MS = 2000;

/**
 * Bir kameranın çekim geçmişi.
 *
 * **Klip tamamlanması SignalR ile DEĞİL, yoklamayla öğreniliyor.** Hub
 * (`IDiagramNotifier` / `DiagramHub`) diyagram telemetrisi için var; ona bir
 * kamera olayı eklemek iyi adlandırılmış bir sınırı bulandırırdı. Klip
 * saniyeler süren, tek kullanıcıyı ilgilendiren, nadir bir olay — kalıcı bir
 * hub sözleşmesi genişletmesini hak etmiyor.
 *
 * Yoklama YALNIZCA `Pending` bir satır varken açık: iş bittiğinde `refetchInterval`
 * `false` döner ve istekler tamamen durur.
 */
export function useCaptures(cameraId: string | undefined, take = 20) {
  return useQuery({
    queryKey: cameraKeys.captures(cameraId ?? ''),
    queryFn: () => getCaptures(cameraId!, take),
    enabled: Boolean(cameraId),
    refetchInterval: query => {
      const captures = query.state.data as CameraCaptureDto[] | undefined;
      const hasPending = captures?.some(c => c.status === CaptureStatus.Pending) ?? false;
      return hasPending ? PENDING_POLL_MS : false;
    }
  });
}

/**
 * Çekim başlatır.
 *
 * `onError` BİLEREK YOK — kod tabanının kuralı: hata politikası çağıran
 * taraftadır (form ise `handleFormApiError`, değilse `toast`).
 */
export function useCreateCapture(cameraId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CameraCaptureCreateRequest) => createCapture(cameraId, request),
    onSuccess: capture => {
      void queryClient.invalidateQueries({ queryKey: cameraKeys.captures(cameraId) });

      // Başarısız çekim de 200 döner ve bir satır bırakır; kullanıcıya "kayıt
      // oluştu" demek yanıltıcı olurdu.
      if (capture.status === CaptureStatus.Failed) {
        toast.error(capture.failureReason ?? 'Çekim başarısız oldu.');
        return;
      }

      toast.success(
        capture.type === CaptureType.Clip
          ? 'Klip çekimi başlatıldı; hazır olunca listede görünecek.'
          : 'Görüntü kaydedildi.'
      );
    }
  });
}
