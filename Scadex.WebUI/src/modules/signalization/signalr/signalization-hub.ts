import { HttpError, HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useSyncExternalStore } from 'react';
import { API_BASE_URL } from '@/lib/axios-helper';
import { getAccessToken } from '@/lib/auth-session';
import {
  SignalizationHubEvents,
  SignalizationHubMethods,
  type OperatorSessionChangedMessage,
  type SignalCabinetStateChangedMessage,
  type SignalDoorSwitchChangedMessage
} from '../models/realtime';

/**
 * `/hubs/signalization` bağlantısının YAŞAM DÖNGÜSÜ — çekirdeğin `lib/signalr/diagram-hub.ts`'i ile aynı kalıp. Veriyle ilgilenmez,
 * gelen olayı kayıtlı dinleyicilere aktarır; ne yapılacağı `hooks/use-session-realtime.ts`'tedir.
 *
 * **Tek bağlantı, sayaçlı abonelik.** İki abonelik türü vardır ve bağlantı ikisinden biri yaşadığı sürece açık kalır:
 * - **Oturumlar** (`sessions` grubu, tüm kabinler): ilk abonede `SubscribeSessions`, son abone gidince `UnsubscribeSessions`.
 * - **Kabin durumu** (`cabinet:{id}` grubu, tek kabin — çıkışlar VE kapı anahtarları): kabinin ilk abonesinde `SubscribeCabinet`, sonuncusu gidince
 *   `UnsubscribeCabinet` (çekirdeğin `diagram-hub.ts`'indeki kabin sayacıyla aynı kalıp).
 *
 * Son abone de gidince bağlantı kısa bir gecikmeyle kapanır.
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

export interface CabinetStateHubHandlers {
  onCabinetStateChanged: (message: SignalCabinetStateChangedMessage) => void;
  onDoorSwitchChanged: (message: SignalDoorSwitchChangedMessage) => void;
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
/** Kabin kimliği → o kabinin çıkış durumu aboneleri. Boş küme tutulmaz: kabin haritadan düşer. */
const cabinetHandlers = new Map<string, Set<CabinetStateHubHandlers>>();

/** Bağlantının yaşamaya devam etmesi gereken tek koşul: herhangi bir türde abone var. */
function hasSubscribers(): boolean {
  return handlerSets.size > 0 || cabinetHandlers.size > 0;
}

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

  built.on(SignalizationHubEvents.signalCabinetStateChanged, (message: SignalCabinetStateChangedMessage) => {
    // Olay yalnızca o kabinin grubuna gelir; yine de kimliğe göre dağıtılır (aynı bağlantı birden çok kabine abone olabilir).
    const handlers = cabinetHandlers.get(message.cabinetId);
    if (handlers) for (const handler of handlers) handler.onCabinetStateChanged(message);
  });

  built.on(SignalizationHubEvents.signalDoorSwitchChanged, (message: SignalDoorSwitchChangedMessage) => {
    const handlers = cabinetHandlers.get(message.cabinetId);
    if (handlers) for (const handler of handlers) handler.onDoorSwitchChanged(message);
  });

  built.onreconnecting(() => setStatus('reconnecting'));

  built.onreconnected(() => {
    setStatus('connected');
    // Yeni ConnectionId: sunucu eski grupları hatırlamaz, abonelik yenilenmezse bağlantı "açık" görünür ama olay gelmez.
    void invokeSubscribeAll();
  });

  // `withAutomaticReconnect` pes ederse ya da sunucu bağlantıyı kapatırsa: abone varsa baştan kurulur.
  built.onclose(() => {
    if (connection === built) connection = null;
    setStatus('disconnected');
    if (hasSubscribers()) scheduleRetry();
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
    if (hasSubscribers()) scheduleRetry();
    return;
  }

  retryAttempt = 0;
  setStatus('connected');
  await invokeSubscribeAll();
}

function scheduleRetry(): void {
  if (retryTimer !== null || moduleOff) return;
  const delay = RETRY_DELAYS_MS[Math.min(retryAttempt, RETRY_DELAYS_MS.length - 1)];
  retryAttempt++;
  retryTimer = window.setTimeout(() => {
    retryTimer = null;
    if (hasSubscribers()) void ensureStarted();
  }, delay);
}

/** Tek bir hub çağrısı; bağlantı bu arada düşmüşse sessiz geçer (yeniden bağlanma aboneliği zaten yeniler). */
async function invoke(method: string, ...args: unknown[]): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(method, ...args);
  } catch {
    /* bağlantı düştü ya da kapanıyor; sunucu grubu kendisi düşürür */
  }
}

/** Kayıtlı bütün abonelikleri sunucuya bildirir — ilk bağlantıda ve her YENİDEN bağlanmada (ConnectionId değişir). */
async function invokeSubscribeAll(): Promise<void> {
  if (handlerSets.size > 0) await invoke(SignalizationHubMethods.subscribeSessions);
  for (const cabinetId of cabinetHandlers.keys()) await invoke(SignalizationHubMethods.subscribeCabinet, cabinetId);
}

function scheduleCloseIfIdle(): void {
  if (hasSubscribers()) return;
  if (closeTimer !== null) window.clearTimeout(closeTimer);

  closeTimer = window.setTimeout(() => {
    closeTimer = null;
    if (hasSubscribers()) return;

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
    if (wasConnected && isFirst) await invoke(SignalizationHubMethods.subscribeSessions);
  })();

  return () => {
    handlerSets.delete(handlers);
    if (handlerSets.size === 0) void invoke(SignalizationHubMethods.unsubscribeSessions);
    scheduleCloseIfIdle();
  };
}

/**
 * TEK kabinin durum değişikliklerine (siren / aydınlatma / kilit / kapı anahtarları) abone olur. Dönen fonksiyon aboneliği bırakır.
 *
 * Aynı kabine birden fazla abone güvenlidir: sunucuya yalnızca ilk abone için `SubscribeCabinet`, sonuncusu gidince
 * `UnsubscribeCabinet` gider.
 */
export function subscribeToCabinetState(cabinetId: string, handlers: CabinetStateHubHandlers): () => void {
  const existing = cabinetHandlers.get(cabinetId);
  const isFirst = existing === undefined;

  if (existing) existing.add(handlers);
  else cabinetHandlers.set(cabinetId, new Set([handlers]));

  void (async () => {
    const wasConnected = connection?.state === HubConnectionState.Connected;
    await ensureStarted();
    if (wasConnected && isFirst) await invoke(SignalizationHubMethods.subscribeCabinet, cabinetId);
  })();

  return () => {
    const current = cabinetHandlers.get(cabinetId);
    if (!current) return;

    current.delete(handlers);
    if (current.size === 0) {
      // Boş küme bırakılmaz: yeniden bağlanmada olmayan bir aboneliği tazelemeye kalkardı.
      cabinetHandlers.delete(cabinetId);
      void invoke(SignalizationHubMethods.unsubscribeCabinet, cabinetId);
    }

    scheduleCloseIfIdle();
  };
}
