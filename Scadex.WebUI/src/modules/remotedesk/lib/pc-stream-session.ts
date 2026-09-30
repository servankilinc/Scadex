/**
 * Tek bir PC monitörünün izleme oturumu: izleme başlat (kiralama + bilet) → WHEP → kiralamayı yenile → koparsa yeniden başla
 * → çıkışta kiralamayı bırak (RemoteDesk.md § 8).
 *
 * Kameradaki `lib/camera/stream-session.ts`'in aynası ama bilet kaynağı farklı ve bir KİRALAMA taşır: sunucu yayını yalnızca
 * kiralaması yenilenen izleyici olduğu sürece açık tutar. Sekme çökerse `DELETE` gitmez; kiralama 45 sn sonra kendiliğinden düşer.
 * Kamera yayın bütçesini (`stream-budget`) KULLANMAZ: PC ekranı tek oynatıcıyla açılır.
 */
import { toApiError } from '@/lib/axios-helper';
import { whepConnect, type WhepSession } from '@/lib/camera/whep';
import { releaseScreenView, renewScreenView, startScreenView } from '../api/remote-desk';

export type PcStreamState =
  /** PC'ye yayın komutu gitti, yol hazır olana kadar bekleniyor (ilk yayında 10–20 sn sürebilir). */
  | 'starting'
  | 'connected'
  /** Koptu ya da izleme sona erdi; yeni kiralamayla yeniden başlanacak. */
  | 'reconnecting'
  /** Tekrar denemekle düzelmeyecek durum (PC bağlı değil, monitör yok, PC yayını başlatamadı). */
  | 'failed';

export interface PcStreamSessionHandle {
  close(): void;
  retry(): void;
}

interface StartOptions {
  deviceId: string;
  monitorIndex: number;
  videoEl: HTMLVideoElement;
  onState: (state: PcStreamState, error?: string) => void;
}

/** Geri çekilme basamakları. Son değer tavandır ve süresiz tekrarlanır. */
const BACKOFF_MS = [1000, 2000, 5000, 10000, 30000];

export function startPcStreamSession({ deviceId, monitorIndex, videoEl, onState }: StartOptions): PcStreamSessionHandle {
  const abort = new AbortController();

  let session: WhepSession | null = null;
  let viewId: string | null = null;
  let renewTimer: ReturnType<typeof setInterval> | null = null;
  let retryTimer: ReturnType<typeof setTimeout> | null = null;
  let attempt = 0;
  let disposed = false;

  const clearRetry = () => {
    if (retryTimer === null) return;
    clearTimeout(retryTimer);
    retryTimer = null;
  };

  /** WHEP'i kapatır ve kiralamayı BIRAKIR: son izleyici gidince sunucu yayını ~10 sn sonra durdurur. */
  const teardown = () => {
    session?.close();
    session = null;

    if (renewTimer !== null) {
      clearInterval(renewTimer);
      renewTimer = null;
    }
    if (viewId) {
      // En iyi çaba: gitmezse kiralama zaman aşımıyla düşer.
      void releaseScreenView(viewId).catch(() => undefined);
      viewId = null;
    }
  };

  const scheduleReconnect = () => {
    if (disposed) return;
    const delay = BACKOFF_MS[Math.min(attempt, BACKOFF_MS.length - 1)];
    attempt += 1;

    clearRetry();
    retryTimer = setTimeout(() => {
      retryTimer = null;
      void connect();
    }, delay);
  };

  const connect = async (): Promise<void> => {
    if (disposed) return;
    teardown();

    // Kimse bakmıyorken PC'ye yayın yaptırmayalım; görünür olunca `onVisibility` tekrar tetikler.
    if (document.visibilityState === 'hidden') {
      onState(attempt === 0 ? 'starting' : 'reconnecting');
      return;
    }

    try {
      onState(attempt === 0 ? 'starting' : 'reconnecting');

      const view = await startScreenView(deviceId, monitorIndex, abort.signal);
      if (disposed) {
        void releaseScreenView(view.viewId).catch(() => undefined);
        return;
      }
      viewId = view.viewId;

      // Kiralama: 404 = sunucu yayını bitirdi (PC koptu, PC'de durduruldu, okuyucusuz kaldı) → yeni kiralamayla yeniden başla.
      renewTimer = setInterval(() => {
        const current = viewId;
        if (!current) return;
        renewScreenView(current).catch(error => {
          if (disposed || viewId !== current) return;
          if (toApiError(error).status === 404) {
            viewId = null;   // düşmüş kiralamayı bırakmaya çalışma
            onState('reconnecting', 'İzleme sona erdi, yeniden bağlanılıyor…');
            teardown();
            scheduleReconnect();
          }
        });
      }, view.leaseRenewSec * 1000);

      session = await whepConnect(
        view.whepUrl,
        view.token,
        videoEl,
        state => {
          if (disposed) return;
          if (state === 'connected') {
            attempt = 0;
            onState('connected');
            return;
          }
          if (state === 'failed' || state === 'disconnected' || state === 'closed') {
            onState('reconnecting');
            teardown();
            scheduleReconnect();
          }
        },
        { lowLatency: true }
      );
    } catch (error) {
      if (disposed) return;
      teardown();
      if (error instanceof DOMException && error.name === 'AbortError') return;

      const apiError = toApiError(error);
      // Sunucu yanıtı olan her hata (PC bağlı değil, monitör yok, PC yayını başlatamadı) sabit bir nedendir: kendiliğinden
      // tekrar denemek PC'ye boşuna yeniden yayın komutu gönderirdi. Kullanıcı "Tekrar dene" ile ister.
      if (apiError.status >= 400) {
        onState('failed', apiError.message);
        return;
      }

      onState('reconnecting', apiError.message);
      scheduleReconnect();
    }
  };

  const onVisibility = () => {
    if (disposed || document.visibilityState !== 'visible') return;
    if (!session && !viewId && retryTimer === null) void connect();
  };
  document.addEventListener('visibilitychange', onVisibility);

  void connect();

  return {
    close() {
      if (disposed) return;
      disposed = true;
      document.removeEventListener('visibilitychange', onVisibility);
      abort.abort();
      clearRetry();
      teardown();
    },
    retry() {
      if (disposed) return;
      attempt = 0;
      clearRetry();
      void connect();
    }
  };
}
