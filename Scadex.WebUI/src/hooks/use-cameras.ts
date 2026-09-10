import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createCamera, getCamera, getCamerasByCabinet, updateCamera } from '@/api/camera';
import { cameraKeys } from '@/api/query-keys';
import type { CameraCreateRequest, CameraUpdateRequest } from '@/models/camera';

/**
 * Bir kabindeki kameralar.
 *
 * `cabinetId` boşken sorgu HİÇ çalışmaz (`enabled`): kabin seçilmeden liste
 * anlamsız ve sunucuya `cabinet/undefined` gitmesi 404 üretirdi.
 */
export function useCameras(cabinetId: string | undefined, includePassive = false) {
  return useQuery({
    queryKey: cameraKeys.byCabinet(cabinetId ?? '', includePassive),
    queryFn: () => getCamerasByCabinet(cabinetId!, includePassive),
    enabled: Boolean(cabinetId)
  });
}

/**
 * Tek bir kamera — canlı izleme detay ekranı için.
 *
 * Listeden türetilmiyor: detay sayfası doğrudan URL ile açılabilir ve o anda
 * kabin listesi önbellekte olmayabilir. Ayrıca hangi kabine ait olduğunu da
 * bilmediğimiz için hangi liste anahtarına bakacağımız belirsiz olurdu.
 */
export function useCamera(id: string | undefined) {
  return useQuery({
    queryKey: cameraKeys.detail(id ?? ''),
    queryFn: () => getCamera(id!),
    enabled: Boolean(id)
  });
}

/**
 * `invalidateQueries` burada güvenli — kamera ekranı form tabanlı bir listedir,
 * diyagram editörünün aksine üzerine yazılabilecek yerel bir düzenleme yok.
 * (Editörde `setQueryData` şart, çünkü refetch uçuştaki düzenlemeleri ezerdi.)
 *
 * `onError` BİLEREK yok: hata politikası `handleFormApiError` ile forma ait ve
 * `form.setError`'a ihtiyaç duyar, o da yalnızca çağıran tarafta vardır.
 */
export function useCreateCamera() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CameraCreateRequest) => createCamera(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: cameraKeys.all });
      toast.success('Kamera eklendi.');
    }
  });
}

export function useUpdateCamera() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CameraUpdateRequest) => updateCamera(request),
    onSuccess: (_data, request) => {
      void queryClient.invalidateQueries({ queryKey: cameraKeys.all });
      toast.success(request.isActive ? 'Kamera güncellendi.' : 'Kamera pasife alındı.');
    }
  });
}
