import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { getDeviceCommands, sendDeviceCommand } from '@/api/device-command';
import { diagramKeys } from '@/api/query-keys';
import { toApiError } from '@/lib/axios-helper';
import { useIsUnsaved } from '@/lib/diagram/unsaved-store';
import { CommandStatus, CommandStatusLabels, DeviceCommandTypeLabels } from '@/models/enums';
import type { DeviceCommandResultDto, DeviceCommandSendRequest } from '@/models/deviceCommand';
import type { CommandCompleted } from '@/models/realtime';

/**
 * Kumanda gönderme ve geçmiş.
 *
 * Sözleşme: `Backend/docs/api-contract/08-scada-command.md`
 */

/** Geçmişte tutulan en fazla satır — sunucunun `take` üst sınırıyla aynı. */
const HISTORY_LIMIT = 20;

/**
 * Cihazın kumanda geçmişi.
 *
 * Henüz kaydedilmemiş bir cihaz için SORGU AÇILMAZ: o Id'nin sunucuda karşılığı
 * yok ve istek 404'e giderdi.
 *
 * Guid'i istemci ürettiği için bunu Id'ye bakarak anlamak mümkün değil; cevap
 * `unsaved-store`'da. Kayıt yazıldığı anda sorgu kendiliğinden açılır.
 */
export function useDeviceCommands(deviceId: string | null | undefined) {
  const isUnsaved = useIsUnsaved(deviceId);
  const enabled = !!deviceId && !isUnsaved;

  return useQuery({
    queryKey: diagramKeys.deviceCommands(deviceId ?? ''),
    queryFn: () => getDeviceCommands(deviceId!, HISTORY_LIMIT),
    enabled,
    // Geçmiş insan hızında değişir; canlı tazeliği `CommandCompleted` yayını
    // sağlıyor, periyodik yoklamaya gerek yok.
    staleTime: 60_000
  });
}

/**
 * Kumanda gönderir.
 *
 * **`onSuccess` "komut başarılı oldu" DEMEK DEĞİLDİR.** HTTP 200 "işlem
 * yürütüldü" demektir; SCADA komutu reddetmiş (`Failed`) ya da hiç cevap
 * vermemiş (`NoResponse`) olabilir ve ikisi de 200 döner. İşin sonucu
 * `result.status`'tadır ve bildirim ona göre seçilir — aksi halde zaman aşımına
 * uğramış bir röle komutu kullanıcıya "başarılı" diye gösterilirdi.
 */
export function useSendCommand(deviceId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: DeviceCommandSendRequest) => sendDeviceCommand(deviceId, request),

    onSuccess: result => {
      // setQueryData — invalidate DEĞİL: sunucu zaten tam satırı döndürdü,
      // yeniden sormak aynı veriyi bir kez daha indirmek olurdu.
      prependCommand(queryClient, deviceId, result);

      const label = DeviceCommandTypeLabels[result.commandType];

      if (result.status === CommandStatus.Succeeded) {
        toast.success(`${label} gönderildi`, {
          description: result.elapsedMs == null ? undefined : `SCADA ${result.elapsedMs} ms'de kabul etti.`
        });
        return;
      }

      // Failed ile NoResponse ayrı gösterilir: birincisinde komutun sahaya
      // gitmediği kesindir, ikincisinde gidip gitmediği BİLİNMEZ. Operatörün
      // yeniden denemeden önce bilmesi gereken şey tam olarak budur.
      toast.error(`${label} başarısız — ${CommandStatusLabels[result.status]}`, {
        description:
          result.resultMessage ??
          (result.status === CommandStatus.NoResponse ? 'SCADA yanıt vermedi; komutun sahaya gidip gitmediği bilinmiyor.' : undefined),
        duration: 8000
      });
    },

    // Buraya yalnızca ön kontrollerin reddi (400/404) ve ağ hatası düşer.
    // Bu durumlarda sunucuda SATIR OLUŞMAMIŞTIR.
    onError: error => toast.error(toApiError(error).message)
  });
}

/**
 * Hub'dan gelen `CommandCompleted`'ı geçmişe yansıtır.
 *
 * **Gönderen kendi olayını da alır** (sunucu grubun tamamına yayınlıyor), bu
 * yüzden önce `commandId` ile ayıklanır — aksi halde her komut listede iki kez
 * görünürdü.
 *
 * Olay gövdesi `DeviceCommandResultDto`'nun alt kümesi olduğu için listeye
 * DOĞRUDAN EKLENMEZ; eksik alanları uydurmak yerine o cihazın geçmişi
 * geçersizleştirilir. Yalnızca cache'te zaten bir liste varsa: yoksa kimse o
 * geçmişe bakmıyordur ve boşuna istek atılmaz.
 */
export function applyCommandCompleted(queryClient: QueryClient, change: CommandCompleted): void {
  const key = diagramKeys.deviceCommands(change.deviceId);
  const current = queryClient.getQueryData<DeviceCommandResultDto[]>(key);

  if (!current) return;
  if (current.some(row => row.id === change.commandId)) return;

  void queryClient.invalidateQueries({ queryKey: key });
}

function prependCommand(queryClient: QueryClient, deviceId: string, result: DeviceCommandResultDto): void {
  queryClient.setQueryData<DeviceCommandResultDto[]>(diagramKeys.deviceCommands(deviceId), previous =>
    previous ? [result, ...previous].slice(0, HISTORY_LIMIT) : previous
  );
}
