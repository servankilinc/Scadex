import { HttpError, HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useSyncExternalStore } from 'react';
import { API_BASE_URL } from '@/lib/axios-helper';
import { getAccessToken } from '@/lib/auth-session';
import { SignalizationHubEvents, SignalizationHubMethods, type OperatorSessionChangedMessage } from '../models/realtime';

/**
 * `/hubs/signalization` bağlantısının YAŞAM DÖNGÜSÜ — çekirdeğin `lib/signalr/diagram-hub.ts`'i ile aynı kalıp. Veriyle ilgilenmez,
 * gelen olayı kayıtlı dinleyicilere aktarır; ne yapılacağı `hooks/use-session-realtime.ts`'tedir.
 *
 * **Tek bağlantı, sayaçlı abonelik.** İlk abonede `SubscribeSessions`, son abone gidince `UnsubscribeSessions`; bağlantı kısa bir
 * gecikmeyle kapanır.
 *
 * Diyagram istemcisinden iki farkı var:
 * - **İlk bağlantı kurulamazsa artan aralıkla yeniden denenir.** Abonelik layout'a bir kez takılır; diyagramdaki gibi "bir sonraki
 *   abonelikte dene" hiç gelmeyebilir.
 * - **Negotiate 404 dönerse denemez.** Backend'de modül kapalı (`Modules:Signalization:Enabled`) ama `VITE_MODULES` açık demektir —
 *   `use-operator-sessions.ts > isModuleOff` ile aynı karar.
 */

export type SignalizationHubStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface SessionHubHandlers {
  onSessionChanged: (message: OperatorSessionChangedMessage) => void;
}

/** Son abone ayrıldıktan sonra kapanış gecikmesi — `<StrictMode>` çift effect'inde kapanıp hemen yeniden açılmasın. */
const CLOSE_GRACE_MS = 2000;
const RETRY_DELAYS_MS = [2_000, 5_000, 10_000, 30_000];

let connection: HubConnection | null = null;
let closeTimer: number | null = null;
let retryTimer: number | null = null;
let retryAttempt = 0;
/** Backend'de modül kapalı: bu sayfa ömründe bir daha denenmez. */
let moduleOff = false;

const handlerSets = new Set<SessionHubHandlers>();

// ---------------------------------------------------------------- durum store

let status: SignalizationHubStatus = 'disconnected';
const statusListeners = new Set<() => void>();

function setStatus(next: SignalizationHubStatus): void {
  if (status === next) return;
  status = next;
  for (const listener of statusListeners) listener();
}

function subscribeStatus(listener: () => void): () => void {
  statusListeners.add(listener);
  return () => {
    statusListeners.delete(listener);
  };
}

/** Bağlantı durumu — yeniden bağlanınca veriyi tazelemek için. */
export function useSignalizationHubStatus(): SignalizationHubStatus {
  return useSyncExternalStore(
    subscribeStatus,
    () => status,
    () => 'disconnected' as SignalizationHubStatus
  );
}

// ------------------------------------------------------------------ bağlantı

function build(): HubConnection {
  const built = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/signalization`, {
      // WebSocket el sıkışması Authorization header'ı taşıyamaz; token query string'e konur (sunucu yalnızca `/hubs` için okur).
      // Fabrika: yeniden bağlanmada o anki token okunsun.
      accessTokenFactory: () => getAccessToken() ?? ''
    })
    .withAutomaticReconnect()
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build();

  built.on(SignalizationHubEvents.operatorSessionChanged, (message: OperatorSessionChangedMessage) => {
    for (const handlers of handlerSets) handlers.onSessionChanged(message);
  });

  built.onreconnecting(() => setStatus('reconnecting'));

  built.onreconnected(() => {
    setStatus('connected');
    // Yeni ConnectionId: sunucu eski grupları hatırlamaz, abonelik yenilenmezse bağlantı "açık" görünür ama olay gelmez.
    if (handlerSets.size > 0) void invokeSubscribe();
  });

  // `withAutomaticReconnect` pes ederse ya da sunucu bağlantıyı kapatırsa: abone varsa baştan kurulur.
  built.onclose(() => {
    if (connection === built) connection = null;
    setStatus('disconnected');
    if (handlerSets.size > 0) scheduleRetry();
  });

  return built;
}

async function ensureStarted(): Promise<void> {
  if (closeTimer !== null) {
    window.clearTimeout(closeTimer);
    closeTimer = null;
  }

  // Hub [Authorize]: token yoksa denemek yalnızca bir 401 turu üretir. Giriş sonrası layout yeniden kurulur, abonelik tekrar gelir.
  if (moduleOff || !getAccessToken()) return;

  connection ??= build();
  if (connection.state !== HubConnectionState.Disconnected) return;

  const starting = connection;
  try {
    setStatus('connecting');
    await starting.start();
  } catch (error) {
    setStatus('disconnected');
    if (error instanceof HttpError && error.statusCode === 404) {
      moduleOff = true;
      return;
    }
    // Sessiz: 10 sn yoklama geri dönüş olarak çalışıyor.
    if (handlerSets.size > 0) scheduleRetry();
    return;
  }

  retryAttempt = 0;
  setStatus('connected');
  if (handlerSets.size > 0) await invokeSubscribe();
}

function scheduleRetry(): void {
  if (retryTimer !== null || moduleOff) return;
  const delay = RETRY_DELAYS_MS[Math.min(retryAttempt, RETRY_DELAYS_MS.length - 1)];
  retryAttempt++;
  retryTimer = window.setTimeout(() => {
    retryTimer = null;
    if (handlerSets.size > 0) void ensureStarted();
  }, delay);
}

async function invokeSubscribe(): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(SignalizationHubMethods.subscribeSessions);
  } catch {
    /* bağlantı bu arada düştü; onreconnected / yeniden deneme aboneliği yeniler */
  }
}

async function invokeUnsubscribe(): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(SignalizationHubMethods.unsubscribeSessions);
  } catch {
    /* bağlantı zaten kapanıyorsa sunucu grubu kendisi düşürür */
  }
}

function scheduleCloseIfIdle(): void {
  if (handlerSets.size > 0) return;
  if (closeTimer !== null) window.clearTimeout(closeTimer);

  closeTimer = window.setTimeout(() => {
    closeTimer = null;
    if (handlerSets.size > 0) return;

    if (retryTimer !== null) {
      window.clearTimeout(retryTimer);
      retryTimer = null;
    }
    retryAttempt = 0;

    const closing = connection;
    connection = null;
    setStatus('disconnected');
    void closing?.stop();
  }, CLOSE_GRACE_MS);
}

/**
 * Operatör işlemi değişikliklerine abone olur. Dönen fonksiyon aboneliği bırakır.
 *
 * Birden fazla abone güvenlidir: sunucuya yalnızca ilk abone için `SubscribeSessions`, son abone gidince `UnsubscribeSessions` gider.
 */
export function subscribeToSessions(handlers: SessionHubHandlers): () => void {
  const isFirst = handlerSets.size === 0;
  handlerSets.add(handlers);

  void (async () => {
    const wasConnected = connection?.state === HubConnectionState.Connected;
    await ensureStarted();
    // Bağlantı zaten açıktıysa `ensureStarted` abonelik göndermez; ilk abone için burada gönderilir.
    if (wasConnected && isFirst) await invokeSubscribe();
  })();

  return () => {
    handlerSets.delete(handlers);
    if (handlerSets.size === 0) void invokeUnsubscribe();
    scheduleCloseIfIdle();
  };
}
