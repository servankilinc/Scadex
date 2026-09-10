import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createCabinet, getCabinetList, getCabinetUpdateModel, updateCabinet } from '@/api/cabinet';
import { cabinetKeys } from '@/api/query-keys';
import type { CabinetCreateRequest, CabinetUpdateRequest } from '@/models/cabinet';

/** Kabin kartları listesi — diyagram editörünün giriş noktası. */
export function useCabinets() {
  return useQuery({
    queryKey: cabinetKeys.list(),
    queryFn: getCabinetList
  });
}

/**
 * Düzenleme formunun kaynağı.
 *
 * Listeden doldurulamaz: `CabinetDetailDto` SCADA alanlarını taşımıyor.
 * `id` null iken (dialog kapalı) istek atılmaz.
 */
export function useCabinetUpdateModel(id: string | null) {
  return useQuery({
    queryKey: cabinetKeys.updateModel(id ?? ''),
    queryFn: () => getCabinetUpdateModel(id!),
    enabled: id != null
  });
}

/**
 * Yeni kabin açar.
 *
 * Başarıda liste **invalidate** edilir — `setQueryData` DEĞİL. Sunucu yalnızca
 * `{ id }` dönüyor; kartın ihtiyaç duyduğu `companyName` ve `deviceStatusName`
 * gelmediği için cache'i yerinde güncellemek uydurma veri yazmak olurdu.
 *
 * Diyagram kaydındaki "invalidate etme" kuralı buraya işlemez: orada yasak
 * olmasının sebebi uçuştaki düzenlemenin ezilmesiydi, kabin listesi ise
 * salt-okunur.
 *
 * `onError` BİLEREK yok — `handleFormApiError` çağıran formda kurulur.
 */
export function useCreateCabinet() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CabinetCreateRequest) => createCabinet(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: cabinetKeys.all });
      toast.success('Kabin oluşturuldu.');
    }
  });
}

export function useUpdateCabinet() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CabinetUpdateRequest) => updateCabinet(request),
    onSuccess: () => {
      // `cabinetKeys.all` hem listeyi hem düzenleme modelini kapsar; ikincisi
      // olmazsa dialog ikinci açılışta eski değerlerle dolar.
      void queryClient.invalidateQueries({ queryKey: cabinetKeys.all });
      toast.success('Kabin güncellendi.');
    }
  });
}
