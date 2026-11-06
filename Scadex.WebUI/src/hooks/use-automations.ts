import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createAutomation, getAutomationList, getAutomationUpdateModel, syncAutomationFromScada, updateAutomation } from '@/api/automation';
import { toApiError } from '@/lib/axios-helper';
import { automationKeys } from '@/api/query-keys';
import type { AutomationCreateRequest, AutomationUpdateRequest } from '@/models/automation';
import { CommandStatus, CommandStatusLabels } from '@/models/enums';

/** Otomasyon kartları listesi — diyagram editörünün giriş noktası. */
export function useAutomations() {
  return useQuery({
    queryKey: automationKeys.list(),
    queryFn: getAutomationList
  });
}

/**
 * Düzenleme formunun kaynağı.
 *
 * Listeden doldurulamaz: `AutomationDetailDto` SCADA alanlarını taşımıyor.
 * `id` null iken (dialog kapalı) istek atılmaz.
 */
export function useAutomationUpdateModel(id: string | null) {
  return useQuery({
    queryKey: automationKeys.updateModel(id ?? ''),
    queryFn: () => getAutomationUpdateModel(id!),
    enabled: id != null
  });
}

/**
 * Yeni otomasyon açar.
 *
 * Başarıda liste **invalidate** edilir — `setQueryData` DEĞİL. Sunucu yalnızca
 * `{ id }` dönüyor; kartın ihtiyaç duyduğu `companyName` ve `deviceStatusName`
 * gelmediği için cache'i yerinde güncellemek uydurma veri yazmak olurdu.
 *
 * Diyagram kaydındaki "invalidate etme" kuralı buraya işlemez: orada yasak
 * olmasının sebebi uçuştaki düzenlemenin ezilmesiydi, otomasyon listesi ise
 * salt-okunur.
 *
 * `onError` BİLEREK yok — `handleFormApiError` çağıran formda kurulur.
 */
export function useCreateAutomation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: AutomationCreateRequest) => createAutomation(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: automationKeys.all });
      toast.success('Otomasyon oluşturuldu.');
    }
  });
}

export function useUpdateAutomation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: AutomationUpdateRequest) => updateAutomation(request),
    onSuccess: () => {
      // `automationKeys.all` hem listeyi hem düzenleme modelini kapsar; ikincisi
      // olmazsa dialog ikinci açılışta eski değerlerle dolar.
      void queryClient.invalidateQueries({ queryKey: automationKeys.all });
      toast.success('Otomasyon güncellendi.');
    }
  });
}

/**
 * Diyagramdaki "Kartı oku" (`POST /api/Automation/{id}/scada-sync`). Önbellek GÜNCELLENMEZ: değişen kanal değerleri sunucudan
 * `DiagramHub` `ChannelValuesChanged` ile tuvale gelir, kontrol modülünün durumu da aynı yoldan. Burada yalnızca özet bildirilir.
 *
 * **`onSuccess` "kart okundu" DEMEK DEĞİLDİR** (`useSendCommand` ile aynı): kartın hatası / yanıtsızlığı 200 + `status` ile gelir.
 * Yanıt yoksa sunucu kontrol modülünü Offline'a çeker.
 */
export function useScadaSync(automationId: string) {
  return useMutation({
    mutationFn: () => syncAutomationFromScada(automationId),
    onSuccess: result => {
      if (result.status !== CommandStatus.Succeeded) {
        toast.error(`Kart okunamadı — ${CommandStatusLabels[result.status]}`, {
          description: result.message ?? (result.status === CommandStatus.NoResponse ? 'SCADA yanıt vermedi.' : undefined),
          duration: 8000
        });
        return;
      }

      const parts = [`${result.readCount} kanal okundu`, result.changedCount === 0 ? 'değişen yok' : `${result.changedCount} değişti`];
      if (result.unmatchedCount > 0) parts.push(`${result.unmatchedCount} kanalın diyagramda karşılığı yok`);
      toast.success('Kart okundu', { description: parts.join(', ') + '.' });
    },
    // Yalnızca yapılandırma (SCADA kapalı / adres yok / otomasyon yok) ve ağ hatası.
    onError: error => toast.error('Kart okunamadı', { description: toApiError(error).message, duration: 8000 })
  });
}
