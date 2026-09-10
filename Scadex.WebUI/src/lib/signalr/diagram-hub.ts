import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useSyncExternalStore } from 'react';
import { API_BASE_URL } from '@/lib/axios-helper';
import { getAccessToken } from '@/lib/auth-session';
import {
  DiagramHubEvents,
  DiagramHubMethods,
  type CabinetStatusChange,
  type ChannelValueChange,
  type CommandCompleted,
  type DeviceStatusChange
} from '@/models/realtime';

/**
 * `/hubs/diagram` bağlantısının YAŞAM DÖNGÜSÜ. Veriyle ilgilenmez — gelen olayı
 * kayıtlı dinleyicilere aktarır, telemetri `lib/diagram/live-store.ts`'te durur.
 *
 * **Tek bağlantı, sayaçlı abonelik.** Uygulama boyunca tek bir WebSocket açılır;
 * bileşenler kabin bazında abone olur ve son abone ayrılınca bağlantı kapanır.
 * Bileşen başına ayrı bağlantı açmak, aynı kabini gösteren iki panel için iki
 * soket ve iki kat yayın demek olurdu.
 *
 * Sözleşme: `Backend/docs/api-contract/09-realtime.md`
 */

export type HubStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface DiagramHubHandlers {
  onChannelValues: (changes: ChannelValueChange[]) => void;
  onDeviceStatus: (changes: DeviceStatusChange[]) => void;
  onCabinetStatus: (change: CabinetStatusChange) => void;
  onCommandCompleted: (change: CommandCompleted) => void;
}

/**
 * Son abone ayrıldıktan sonra bağlantının kapatılması için beklenen süre.
 *
 * Sıfır olsaydı iki durumda gereksiz yere kapanıp hemen yeniden açılırdı:
 * `<StrictMode>`'un effect'i iki kez çalıştırması ve kullanıcının kabinler
 * arasında hızlı gezinmesi. WebSocket el sıkışması ucuz değil.
 */
const CLOSE_GRACE_MS = 2000;

let connection: HubConnection | null = null;
let closeTimer: number | null = null;

/** Kabin başına abone sayısı. Sıfıra düşen kabinden çıkılır. */
const cabinetRefCounts = new Map<string, number>();
const handlerSets = new Set<DiagramHubHandlers>();

// ---------------------------------------------------------------- durum store

let status: HubStatus = 'disconnected';
const statusListeners = new Set<() => void>();

function setStatus(next: HubStatus): void {
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

/** Bağlantı durumu — kopukluk banner'ı bunu okur. */
export function useHubStatus(): HubStatus {
  return useSyncExternalStore(
    subscribeStatus,
    () => status,
    // Sunucuda render yok ama getServerSnapshot vermemek uyarı üretir.
    () => 'disconnected' as HubStatus
  );
}

// ------------------------------------------------------------------ bağlantı

function build(): HubConnection {
  const built = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/diagram`, {
      // WebSocket el sıkışması Authorization HEADER'I TAŞIYAMAZ; SignalR token'ı
      // query string'e koyar ve sunucu onu `OnMessageReceived` ile oradan okur.
      // Fabrika olarak veriliyor, sabit değer olarak değil: yeniden bağlanmada
      // o anki token okunsun.
      accessTokenFactory: () => getAccessToken() ?? ''
    })
    .withAutomaticReconnect()
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build();

  built.on(DiagramHubEvents.channelValuesChanged, (_cabinetId: string, changes: ChannelValueChange[]) => {
    for (const handlers of handlerSets) handlers.onChannelValues(changes);
  });

  built.on(DiagramHubEvents.deviceStatusChanged, (_cabinetId: string, changes: DeviceStatusChange[]) => {
    for (const handlers of handlerSets) handlers.onDeviceStatus(changes);
  });

  built.on(DiagramHubEvents.cabinetStatusChanged, (change: CabinetStatusChange) => {
    for (const handlers of handlerSets) handlers.onCabinetStatus(change);
  });

  built.on(DiagramHubEvents.commandCompleted, (_cabinetId: string, change: CommandCompleted) => {
    for (const handlers of handlerSets) handlers.onCommandCompleted(change);
  });

  built.onreconnecting(() => setStatus('reconnecting'));

  built.onreconnected(() => {
    setStatus('connected');
    // Sunucu YENIDEN BAGLANAN istemciyi eski gruplarında HATIRLAMAZ: yeni bir
    // ConnectionId gelir. Abonelikler burada yenilenmezse bağlantı "Connected"
    // görünür ama hiçbir olay gelmez — sessiz ve teşhisi zor bir kopukluk.
    for (const cabinetId of cabinetRefCounts.keys()) void invokeSubscribe(cabinetId);
  });

  built.onclose(() => setStatus('disconnected'));

  return built;
}

async function ensureStarted(): Promise<HubConnection | null> {
  if (closeTimer !== null) {
    window.clearTimeout(closeTimer);
    closeTimer = null;
  }

  // Token yoksa bağlanmanın anlamı yok: hub [Authorize]'dur, denemek yalnızca
  // bir 401 turu üretir.
  if (!getAccessToken()) return null;

  connection ??= build();

  if (connection.state === HubConnectionState.Connected) return connection;
  if (connection.state === HubConnectionState.Connecting || connection.state === HubConnectionState.Reconnecting) return connection;

  try {
    setStatus('connecting');
    await connection.start();
    setStatus('connected');
  } catch {
    // Sessizce yutulur ve durum 'disconnected' kalır: banner kullanıcıya zaten
    // görünür bir geri bildirim veriyor, toast fırlatmak kopuk bir ağda saniyede
    // bir bildirim üretirdi. `withAutomaticReconnect` yalnızca KURULMUŞ bir
    // bağlantı düşerse devreye girer, ilk start başarısızlığında değil —
    // yeniden deneme bir sonraki abonelikte olur.
    setStatus('disconnected');
    return null;
  }

  return connection;
}

async function invokeSubscribe(cabinetId: string): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(DiagramHubMethods.subscribe, cabinetId);
  } catch {
    /* bağlantı bu arada düştü; onreconnected yeniden dener */
  }
}

async function invokeUnsubscribe(cabinetId: string): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(DiagramHubMethods.unsubscribe, cabinetId);
  } catch {
    /* bağlantı zaten kapanıyorsa sunucu grubu kendisi düşürür */
  }
}

function scheduleCloseIfIdle(): void {
  if (cabinetRefCounts.size > 0 || handlerSets.size > 0) return;
  if (closeTimer !== null) window.clearTimeout(closeTimer);

  closeTimer = window.setTimeout(() => {
    closeTimer = null;
    if (cabinetRefCounts.size > 0 || handlerSets.size > 0) return;

    const closing = connection;
    connection = null;
    setStatus('disconnected');
    void closing?.stop();
  }, CLOSE_GRACE_MS);
}

/**
 * Bir kabinin canlı yayınına abone olur. Dönen fonksiyon aboneliği bırakır.
 *
 * Aynı kabine birden fazla abone olmak güvenlidir: sunucuya yalnızca ilk abone
 * için `Subscribe`, yalnızca son abone ayrılınca `Unsubscribe` gider.
 */
export function subscribeToCabinet(cabinetId: string, handlers: DiagramHubHandlers): () => void {
  handlerSets.add(handlers);

  const previous = cabinetRefCounts.get(cabinetId) ?? 0;
  cabinetRefCounts.set(cabinetId, previous + 1);

  void (async () => {
    const started = await ensureStarted();
    // Yalnızca İLK abone için: aynı gruba iki kez katılmak sunucuda zararsız ama
    // gereksiz bir tur.
    if (started && previous === 0) await invokeSubscribe(cabinetId);
    // Bağlantı bu abone gelmeden önce kurulmuşsa `previous > 0` olsa bile grup
    // zaten aktiftir; ek bir şey yapmaya gerek yok.
  })();

  return () => {
    handlerSets.delete(handlers);

    const current = cabinetRefCounts.get(cabinetId) ?? 0;
    if (current <= 1) {
      cabinetRefCounts.delete(cabinetId);
      void invokeUnsubscribe(cabinetId);
    } else {
      cabinetRefCounts.set(cabinetId, current - 1);
    }

    scheduleCloseIfIdle();
  };
}
