import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { getCamera, getCamerasByCabinet, updateCamera } from '@/api/camera';
import { cameraKeys, diagramKeys } from '@/api/query-keys';
import type { CameraUpdateRequest } from '@/models/camera';

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
export function useUpdateCamera() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CameraUpdateRequest) => updateCamera(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: cameraKeys.all });
      // Kamera bir cihazdır: diyagram önbelleği de bayatlar. Yalnızca İŞARETLENİR (refetchType none) — açık editördeki düzenleme ezilmesin.
      void queryClient.invalidateQueries({ queryKey: diagramKeys.all, refetchType: 'none' });
      toast.success('Kamera güncellendi.');
    }
  });
}
