import { useEffect, useRef } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { getCabinetStatusBreakdown } from '@/api/cabinet';
import { cabinetKeys } from '@/api/query-keys';
import { subscribeToCabinet, useHubStatus } from '@/lib/signalr/diagram-hub';
import type { CabinetStatusBreakdownDto } from '@/models/cabinet';

/**
 * Kamera durumu değişimi kabin durumu DEĞİŞMEDİKÇE yayınlanmaz (sunucu yalnızca cihaz ve kabin değişimini yayınlar): kabin zaten
 * Uyarı'dayken ikinci kamera düşerse hiçbir olay gelmez. Panel açıkken bu yedek yoklama o boşluğu kapatır.
 */
const FALLBACK_POLL_MS = 30_000;

/** Yayın kümesi yalnızca sorguyu tazeler; değer taşıyan olaylar (kanal, komut) bu ekranı ilgilendirmez. */
const ignore = () => undefined;

/**
 * Kabin durumunun dökümü — harita panelindeki "Durum nedeni" bölümü. Panel açıkken kabinin canlı yayın grubuna abone olunur
 * (diyagramla aynı grup, ref sayımlı): cihaz ya da kabin durumu değişince döküm yeniden çekilir. Yeniden bağlanmada da
 * tazelenir — kopuklukta kaçan olaylar geri gelmez (`use-cabinet-overview-live.ts` ile aynı kural).
 */
export function useCabinetStatusBreakdown(cabinetId: string) {
  const queryClient = useQueryClient();
  const status = useHubStatus();

  useEffect(() => {
    const key = cabinetKeys.statusBreakdown(cabinetId);
    const refresh = () => void queryClient.invalidateQueries({ queryKey: key });

    return subscribeToCabinet(cabinetId, {
      // Cihaz durumu yalnızca gerçek geçişte yayınlanır (seyrek); hangi cihazın değiştiğine bakmadan tazelemek ucuz ve güvenli.
      onDeviceStatus: refresh,
      // Kabin olayı kabin grubunda HER ingest'te gelir ve dinleyiciler kabine göre ayrılmaz (haritanın özet grubu başka kabinleri de
      // getirir): yalnızca BU kabinin durumu döküm göründüğünden farklıysa tazelenir — yoksa her ingest bir istek olurdu.
      onCabinetStatus: change => {
        if (change.cabinetId !== cabinetId) return;
        const current = queryClient.getQueryData<CabinetStatusBreakdownDto>(key);
        if (current && current.statusId === change.statusId) return;
        refresh();
      },
      onChannelValues: ignore,
      onCommandCompleted: ignore
    });
  }, [cabinetId, queryClient]);

  const hasConnectedBefore = useRef(false);
  useEffect(() => {
    if (status !== 'connected') return;
    if (!hasConnectedBefore.current) {
      hasConnectedBefore.current = true;
      return;
    }
    void queryClient.invalidateQueries({ queryKey: cabinetKeys.statusBreakdown(cabinetId) });
  }, [status, cabinetId, queryClient]);

  return useQuery({
    queryKey: cabinetKeys.statusBreakdown(cabinetId),
    queryFn: () => getCabinetStatusBreakdown(cabinetId),
    refetchInterval: FALLBACK_POLL_MS
  });
}
