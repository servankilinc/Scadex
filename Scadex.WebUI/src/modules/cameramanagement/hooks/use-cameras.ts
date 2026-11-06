import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { getCamera, getCamerasByAutomation, updateCamera } from '../api/camera';
import { diagramKeys } from '@/api/query-keys';
import { cameraKeys } from '../api/query-keys';
import type { CameraUpdateRequest } from '../models/camera';

/**
 * Bir otomasyondaki kameralar.
 *
 * `automationId` boşken sorgu HİÇ çalışmaz (`enabled`): otomasyon seçilmeden liste
 * anlamsız ve sunucuya `automation/undefined` gitmesi 404 üretirdi.
 */
export function useCameras(automationId: string | undefined, includePassive = false) {
  return useQuery({
    queryKey: cameraKeys.byAutomation(automationId ?? '', includePassive),
    queryFn: () => getCamerasByAutomation(automationId!, includePassive),
    enabled: Boolean(automationId)
  });
}

/**
 * Tek bir kamera — canlı izleme detay ekranı için.
 *
 * Listeden türetilmiyor: detay sayfası doğrudan URL ile açılabilir ve o anda
 * otomasyon listesi önbellekte olmayabilir. Ayrıca hangi otomasyona ait olduğunu da
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
