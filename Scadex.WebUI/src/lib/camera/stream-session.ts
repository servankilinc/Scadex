/**
 * Tek bir kameranın canlı akım oturumu: bilet al → bağlan → koparsa yeniden
 * bağlan.
 *
 * `whep.ts` tek bir el sıkışmayı bilir; dayanıklılık burada. Ayrı tutulmalarının
 * sebebi, yeniden bağlanmanın **yeni bir bilet** gerektirmesi: bilet 60 saniye
 * yaşıyor, dolayısıyla eskisini saklayıp tekrar denemek zaten çalışmazdı ve
 * `whep.ts`'in bilet almayı bilmesi onu API katmanına bağlardı.
 */
import { createStreamTicket } from '@/api/camera-stream';
import { toApiError } from '@/lib/axios-helper';
import type { StreamProfile } from '@/models/enums/entityEnums';
import { acquireStreamSlot, type ReleaseSlot } from './stream-budget';
import { whepConnect, type WhepSession } from './whep';

export type StreamState =
  /** Bütçede yer bekliyor. */
  | 'queued'
  | 'connecting'
  | 'connected'
  /** Koptu, yeniden bağlanılacak. */
  | 'reconnecting'
  /** Tekrar denemekle düzelmeyecek bir hata (pasif kamera, kapalı akım…). */
  | 'failed';

export interface StreamSessionHandle {
  /** Oturumu kapatır ve bütçedeki yeri geri verir. */
  close(): void;
  /** `failed` durumundan elle yeniden dener. */
  retry(): void;
}

interface StartOptions {
  cameraId: string;
  profile: StreamProfile;
  videoEl: HTMLVideoElement;
  onState: (state: StreamState, error?: string) => void;
}

/** Geri çekilme basamakları. Son değer tavandır ve süresiz tekrarlanır. */
const BACKOFF_MS = [1000, 2000, 4000, 8000, 15000, 30000];

export function startStreamSession({ cameraId, profile, videoEl, onState }: StartOptions): StreamSessionHandle {
  const abort = new AbortController();

  let session: WhepSession | null = null;
  let release: ReleaseSlot | null = null;
  let attempt = 0;
  let retryTimer: ReturnType<typeof setTimeout> | null = null;
  let disposed = false;

  const clearRetry = () => {
    if (retryTimer === null) return;
    clearTimeout(retryTimer);
    retryTimer = null;
  };

  const teardownConnection = () => {
    session?.close();
    session = null;

    // Yeri HEMEN geri ver: yeniden bağlanmayı beklerken tutmak, sıradaki
    // kutucuğu boşuna bekletirdi.
    release?.();
    release = null;
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

    teardownConnection();

    // Sekme arka plandayken yeniden denemenin anlamı yok: kimse bakmıyorken
    // kameradan akış çekmek, tam da bütçenin engellemeye çalıştığı israf.
    // Görünür olunca `onVisibility` tekrar tetikler.
    if (document.visibilityState === 'hidden') {
      onState(attempt === 0 ? 'queued' : 'reconnecting');
      return;
    }

    try {
      onState(attempt === 0 ? 'queued' : 'reconnecting');
      release = await acquireStreamSlot(abort.signal);

      if (disposed) return;
      onState('connecting');

      const streamToken = await createStreamTicket(cameraId, profile);
      if (disposed) return;

      session = await whepConnect(streamToken.whepUrl, streamToken.token, videoEl, state => {
        if (disposed) return;

        if (state === 'connected') {
          // Sayaç SIFIRLANIR: saatler sonra kopan bir bağlantı, ilk kopuşmuş
          // gibi hızlı denenmeli. Sıfırlanmasaydı uzun süre açık kalan bir
          // kutucuk ilk kopuşta 30 saniye beklerdi.
          attempt = 0;
          onState('connected');
          return;
        }

        if (state === 'failed' || state === 'disconnected' || state === 'closed') {
          onState('reconnecting');
          scheduleReconnect();
        }
      });
    } catch (error) {
      if (disposed) return;
      teardownConnection();

      if (error instanceof DOMException && error.name === 'AbortError') return;

      const apiError = toApiError(error);

      // 400 = kameranın tanımından kaynaklanan, tekrar denemekle düzelmeyecek
      // bir durum (pasif kamera, kapalı akım). Yeniden denemek yalnızca aynı
      // hatayı sonsuza dek üretirdi; kullanıcı ayarı düzeltmeli.
      if (apiError.status >= 400 && apiError.status < 500) {
        onState('failed', apiError.message);
        return;
      }

      onState('reconnecting', apiError.message);
      scheduleReconnect();
    }
  };

  const onVisibility = () => {
    if (disposed || document.visibilityState !== 'visible') return;
    // Sekme geri geldi: gizliyken atlanan denemeyi hemen yap.
    if (!session && retryTimer === null) void connect();
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
      teardownConnection();
    },
    retry() {
      if (disposed) return;
      attempt = 0;
      clearRetry();
      void connect();
    }
  };
}
