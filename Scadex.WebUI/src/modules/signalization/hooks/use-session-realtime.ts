import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { signalizationKeys } from '../api/query-keys';
import { subscribeToSessions, useSignalizationHubStatus } from '../signalr/signalization-hub';

/**
 * Olayların toplanma penceresi. Motor tek bir saha olayında oturumu art arda birkaç kez kaydedebilir (kapı açıldı → kilit komutu →
 * siren); her olayda ayrı refetch yerine pencere sonunda TEK invalidation yapılır.
 */
const BATCH_MS = 250;

/**
 * Operatör işlemi canlı yayını (`/hubs/signalization`) → TanStack invalidation. Layout'a bir kez takılır (modülün `LayoutExtension`'ı).
 *
 * Yalnızca AKTİF sorgular yeniden çekilir: açık oturumlar (harita, canlı panel, uyarı izleyicisi), sayfalı geçmiş ve değişen
 * oturumun detayı. Özet rapor (`summary`) BİLEREK tazelenmez — 366 günlük aralığı her oturum değişiminde yeniden hesaplatmak
 * pahalı, rapor da canlı ekran değildir.
 *
 * Yoklama (`LIVE_POLL_MS`) kaldırılmadı: soket kurulamazsa ya da kopuksa geri dönüş odur.
 */
export function useSessionRealtime(): void {
  const queryClient = useQueryClient();
  const status = useSignalizationHubStatus();

  useEffect(() => {
    const pendingSessionIds = new Set<number>();
    let flushTimer: number | null = null;

    const flush = () => {
      flushTimer = null;
      const sessionIds = [...pendingSessionIds];
      pendingSessionIds.clear();

      void queryClient.invalidateQueries({ queryKey: signalizationKeys.openSessionsAll() });
      void queryClient.invalidateQueries({ queryKey: signalizationKeys.sessionLists() });
      for (const id of sessionIds) void queryClient.invalidateQueries({ queryKey: signalizationKeys.sessionDetail(id) });
    };

    const dispose = subscribeToSessions({
      onSessionChanged: message => {
        pendingSessionIds.add(message.sessionId);
        flushTimer ??= window.setTimeout(flush, BATCH_MS);
      }
    });

    return () => {
      dispose();
      if (flushTimer !== null) window.clearTimeout(flushTimer);
    };
  }, [queryClient]);

  // Kopukluk sırasında kaçan olaylar geri gelmez; bağlantı geri geldiğinde tek doğru telafi TAZELEMEDİR. Hangi oturumların
  // değiştiği bilinmez: bütün oturum sorguları tazelenir (özet hariç).
  //
  // İLK bağlantıda tazeleme YAPILMAZ: sorgular zaten yeni çekildi (`use-diagram-live.ts` ile aynı kural).
  const hasConnectedBefore = useRef(false);
  useEffect(() => {
    if (status !== 'connected') return;

    if (!hasConnectedBefore.current) {
      hasConnectedBefore.current = true;
      return;
    }

    void queryClient.invalidateQueries({
      queryKey: signalizationKeys.sessions(),
      predicate: query => query.queryKey[2] !== 'summary'
    });
  }, [status, queryClient]);
}
