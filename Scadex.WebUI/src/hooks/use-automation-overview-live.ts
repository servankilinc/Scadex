import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { automationKeys } from '@/api/query-keys';
import { subscribeToAutomationOverview, useHubStatus } from '@/lib/signalr/diagram-hub';

/**
 * Otomasyon listesini (`automationKeys.list()`) otomasyon DURUMU değişince tazeler — ana sayfa haritası, rozet sayaçları ve otomasyon
 * kartları sayfa yenilenmeden renk değiştirir.
 *
 * Önbellek `setQueryData` ile yerinde DEĞİL, `invalidateQueries` ile tazelenir: kartlar ve harita popup'ı
 * `deviceStatusName`'i de okuyor ve olay yalnızca `statusId` taşıyor — adı istemcide uydurmak sunucudaki lookup'tan
 * ayrışırdı. Maliyet düşük: sunucu bu gruba yalnızca durum DEĞİŞİNCE yayın yapar (her ingest'te değil), art arda gelen
 * olaylarda TanStack süren isteği iptal edip tek bir istekle bitirir.
 */
export function useAutomationOverviewLive(): void {
  const queryClient = useQueryClient();
  const status = useHubStatus();

  useEffect(
    () =>
      subscribeToAutomationOverview(() => {
        void queryClient.invalidateQueries({ queryKey: automationKeys.list() });
      }),
    [queryClient]
  );

  // Kopukluk sırasında kaçan olaylar geri gelmez (hub bir kuyruk değil, anlık yayındır); bağlantı geri gelince tek
  // doğru telafi tazelemedir. İLK bağlantıda yapılmaz: liste zaten az önce çekildi.
  const hasConnectedBefore = useRef(false);
  useEffect(() => {
    if (status !== 'connected') return;
    if (!hasConnectedBefore.current) {
      hasConnectedBefore.current = true;
      return;
    }
    void queryClient.invalidateQueries({ queryKey: automationKeys.list() });
  }, [status, queryClient]);
}
