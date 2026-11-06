import { useEffect, useRef } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getAutomationStatusBreakdown } from '@/api/automation';
import { automationKeys } from '@/api/query-keys';
import { subscribeToAutomation, useHubStatus } from '@/lib/signalr/diagram-hub';
import type { AutomationStatusBreakdownDto } from '@/models/automation';

/**
 * Kamera durumu değişimi otomasyon durumu DEĞİŞMEDİKÇE yayınlanmaz (sunucu yalnızca cihaz ve otomasyon değişimini yayınlar): otomasyon zaten
 * Uyarı'dayken ikinci kamera düşerse hiçbir olay gelmez. Panel açıkken bu yedek yoklama o boşluğu kapatır.
 */
const FALLBACK_POLL_MS = 30_000;

/** Yayın kümesi yalnızca sorguyu tazeler; değer taşıyan olaylar (kanal, komut) bu ekranı ilgilendirmez. */
const ignore = () => undefined;

/**
 * Otomasyon durumunun dökümü — harita panelindeki "Durum nedeni" bölümü. Panel açıkken otomasyonun canlı yayın grubuna abone olunur
 * (diyagramla aynı grup, ref sayımlı): cihaz ya da otomasyon durumu değişince döküm yeniden çekilir. Yeniden bağlanmada da
 * tazelenir — kopuklukta kaçan olaylar geri gelmez (`use-automation-overview-live.ts` ile aynı kural).
 */
export function useAutomationStatusBreakdown(automationId: string) {
  const queryClient = useQueryClient();
  const status = useHubStatus();

  useEffect(() => {
    const key = automationKeys.statusBreakdown(automationId);
    const refresh = () => void queryClient.invalidateQueries({ queryKey: key });

    return subscribeToAutomation(automationId, {
      // Cihaz durumu yalnızca gerçek geçişte yayınlanır (seyrek); hangi cihazın değiştiğine bakmadan tazelemek ucuz ve güvenli.
      onDeviceStatus: refresh,
      // Otomasyon olayı otomasyon grubunda HER ingest'te gelir ve dinleyiciler otomasyona göre ayrılmaz (haritanın özet grubu başka otomasyonları da
      // getirir): yalnızca BU otomasyonun durumu döküm göründüğünden farklıysa tazelenir — yoksa her ingest bir istek olurdu.
      onAutomationStatus: change => {
        if (change.automationId !== automationId) return;
        const current = queryClient.getQueryData<AutomationStatusBreakdownDto>(key);
        if (current && current.statusId === change.statusId) return;
        refresh();
      },
      onChannelValues: ignore,
      onCommandCompleted: ignore
    });
  }, [automationId, queryClient]);

  const hasConnectedBefore = useRef(false);
  useEffect(() => {
    if (status !== 'connected') return;
    if (!hasConnectedBefore.current) {
      hasConnectedBefore.current = true;
      return;
    }
    void queryClient.invalidateQueries({ queryKey: automationKeys.statusBreakdown(automationId) });
  }, [status, automationId, queryClient]);

  return useQuery({
    queryKey: automationKeys.statusBreakdown(automationId),
    queryFn: () => getAutomationStatusBreakdown(automationId),
    refetchInterval: FALLBACK_POLL_MS
  });
}
