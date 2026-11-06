import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useSyncExternalStore } from 'react';
import { API_BASE_URL } from '@/lib/axios-helper';
import { getAccessToken } from '@/lib/auth-session';
import {
  DiagramHubEvents,
  DiagramHubMethods,
  type AutomationStatusChangeNotify,
  type CardReadNotify,
  type ChannelValueChangeNotify,
  type CommandCompletedNotify,
  type DeviceStatusChangeNotify
} from '@/models/realtime';

/**
 * `/hubs/diagram` bağlantısının YAŞAM DÖNGÜSÜ. Veriyle ilgilenmez — gelen olayı
 * kayıtlı dinleyicilere aktarır, telemetri `lib/diagram/live-store.ts`'te durur.
 *
 * **Tek bağlantı, sayaçlı abonelik.** Uygulama boyunca tek bir WebSocket açılır;
 * bileşenler otomasyon bazında abone olur ve son abone ayrılınca bağlantı kapanır.
 * Bileşen başına ayrı bağlantı açmak, aynı otomasyonu gösteren iki panel için iki
 * soket ve iki kat yayın demek olurdu.
 *
 * Sözleşme: `Backend/docs/api-contract/09-realtime.md`
 */

export type HubStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface DiagramHubHandlers {
  onChannelValues: (changes: ChannelValueChangeNotify[]) => void;
  onDeviceStatus: (changes: DeviceStatusChangeNotify[]) => void;
  onAutomationStatus: (change: AutomationStatusChangeNotify) => void;
  onCommandCompleted: (change: CommandCompletedNotify) => void;
  /** Opsiyonel: yalnızca Otomasyonlar → "Kart tanımlama" penceresi dinler. */
  onCardRead?: (change: CardReadNotify) => void;
}

/**
 * Son abone ayrıldıktan sonra bağlantının kapatılması için beklenen süre.
 *
 * Sıfır olsaydı iki durumda gereksiz yere kapanıp hemen yeniden açılırdı:
 * `<StrictMode>`'un effect'i iki kez çalıştırması ve kullanıcının otomasyonlar
 * arasında hızlı gezinmesi. WebSocket el sıkışması ucuz değil.
 */
const CLOSE_GRACE_MS = 2000;

let connection: HubConnection | null = null;
let closeTimer: number | null = null;

/** Otomasyon başına abone sayısı. Sıfıra düşen otomasyondan çıkılır. */
const automationRefCounts = new Map<string, number>();
const handlerSets = new Set<DiagramHubHandlers>();

/**
 * "Tüm otomasyonlar" özetinin aboneleri (`SubscribeAutomations` grubu). Abonelik başına AYRI nesne: aynı fonksiyonla iki
 * kez abone olan iki bileşen Set'te tek kayda düşüp sayacı bozmasın.
 */
interface OverviewSubscription {
  onAutomationStatus: (change: AutomationStatusChangeNotify) => void;
}
const overviewSubscriptions = new Set<OverviewSubscription>();

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

  built.on(DiagramHubEvents.channelValuesChanged, (_automationId: string, changes: ChannelValueChangeNotify[]) => {
    for (const handlers of handlerSets) handlers.onChannelValues(changes);
  });

  built.on(DiagramHubEvents.deviceStatusChanged, (_automationId: string, changes: DeviceStatusChangeNotify[]) => {
    for (const handlers of handlerSets) handlers.onDeviceStatus(changes);
  });

  // Aynı olay iki gruptan gelebilir (otomasyon grubu her ingest'te, özet grubu yalnızca durum değişince); iki dinleyici
  // türü de değeri yerine yazdığı için çift teslim zararsız.
  built.on(DiagramHubEvents.automationStatusChanged, (change: AutomationStatusChangeNotify) => {
    for (const handlers of handlerSets) handlers.onAutomationStatus(change);
    for (const subscription of overviewSubscriptions) subscription.onAutomationStatus(change);
  });

  built.on(DiagramHubEvents.commandCompleted, (_automationId: string, change: CommandCompletedNotify) => {
    for (const handlers of handlerSets) handlers.onCommandCompleted(change);
  });

  // Diğer olaylar gibi otomasyona göre dağıtılmaz: dinleyen `change.automationId`'ye bakar.
  built.on(DiagramHubEvents.cardRead, (_automationId: string, change: CardReadNotify) => {
    for (const handlers of handlerSets) handlers.onCardRead?.(change);
  });

  built.onreconnecting(() => setStatus('reconnecting'));

  built.onreconnected(() => {
    setStatus('connected');
    // Sunucu YENIDEN BAGLANAN istemciyi eski gruplarında HATIRLAMAZ: yeni bir
    // ConnectionId gelir. Abonelikler burada yenilenmezse bağlantı "Connected"
    // görünür ama hiçbir olay gelmez — sessiz ve teşhisi zor bir kopukluk.
    void invokeSubscribeAll();
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
    // Bağlantı kurulurken gelen aboneler `invoke`'u atlamış olabilir (durum henüz 'Connected' değildi); o an kayıtlı
    // olan her şey burada bir kez abone edilir. Aynı gruba iki kez katılmak sunucuda zararsız.
    void invokeSubscribeAll();
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

async function invokeSubscribe(automationId: string): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(DiagramHubMethods.subscribe, automationId);
  } catch {
    /* bağlantı bu arada düştü; onreconnected yeniden dener */
  }
}

async function invokeUnsubscribe(automationId: string): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(DiagramHubMethods.unsubscribe, automationId);
  } catch {
    /* bağlantı zaten kapanıyorsa sunucu grubu kendisi düşürür */
  }
}

/** Parametresiz hub metodu (özet grubu). Bağlantı yoksa sessizce atlanır — bağlanınca `invokeSubscribeAll` telafi eder. */
async function invokeMethod(method: string): Promise<void> {
  if (connection?.state !== HubConnectionState.Connected) return;
  try {
    await connection.invoke(method);
  } catch {
    /* bağlantı bu arada düştü; onreconnected yeniden dener */
  }
}

/**
 * Kayıtlı bütün abonelikleri sunucuya bildirir. Sunucu yeniden bağlanan istemciyi eski gruplarında HATIRLAMAZ (yeni
 * ConnectionId); ilk bağlantıda da kurulum sırasında gelen aboneler atlanmış olabilir.
 */
async function invokeSubscribeAll(): Promise<void> {
  for (const automationId of automationRefCounts.keys()) void invokeSubscribe(automationId);
  if (overviewSubscriptions.size > 0) await invokeMethod(DiagramHubMethods.subscribeAutomations);
}

function hasSubscribers(): boolean {
  return automationRefCounts.size > 0 || handlerSets.size > 0 || overviewSubscriptions.size > 0;
}

function scheduleCloseIfIdle(): void {
  if (hasSubscribers()) return;
  if (closeTimer !== null) window.clearTimeout(closeTimer);

  closeTimer = window.setTimeout(() => {
    closeTimer = null;
    if (hasSubscribers()) return;

    const closing = connection;
    connection = null;
    setStatus('disconnected');
    void closing?.stop();
  }, CLOSE_GRACE_MS);
}

/**
 * Bir otomasyonun canlı yayınına abone olur. Dönen fonksiyon aboneliği bırakır.
 *
 * Aynı otomasyona birden fazla abone olmak güvenlidir: sunucuya yalnızca ilk abone
 * için `Subscribe`, yalnızca son abone ayrılınca `Unsubscribe` gider.
 */
export function subscribeToAutomation(automationId: string, handlers: DiagramHubHandlers): () => void {
  handlerSets.add(handlers);

  const previous = automationRefCounts.get(automationId) ?? 0;
  automationRefCounts.set(automationId, previous + 1);

  void (async () => {
    const started = await ensureStarted();
    // Yalnızca İLK abone için: aynı gruba iki kez katılmak sunucuda zararsız ama
    // gereksiz bir tur.
    if (started && previous === 0) await invokeSubscribe(automationId);
    // Bağlantı bu abone gelmeden önce kurulmuşsa `previous > 0` olsa bile grup
    // zaten aktiftir; ek bir şey yapmaya gerek yok.
  })();

  return () => {
    handlerSets.delete(handlers);

    const current = automationRefCounts.get(automationId) ?? 0;
    if (current <= 1) {
      automationRefCounts.delete(automationId);
      void invokeUnsubscribe(automationId);
    } else {
      automationRefCounts.set(automationId, current - 1);
    }

    scheduleCloseIfIdle();
  };
}

/**
 * Tüm otomasyonların durum özetine abone olur (ana sayfa haritası, otomasyon listesi). Dönen fonksiyon aboneliği bırakır.
 *
 * Sunucu bu gruba yalnızca otomasyon durumu DEĞİŞİNCE yayın yapar — her ingest'te değil. Birden fazla abone güvenlidir:
 * sunucuya yalnızca ilk abone için `SubscribeAutomations`, son abone gidince `UnsubscribeAutomations` gider.
 */
export function subscribeToAutomationOverview(onAutomationStatus: (change: AutomationStatusChangeNotify) => void): () => void {
  const subscription: OverviewSubscription = { onAutomationStatus };
  const isFirst = overviewSubscriptions.size === 0;
  overviewSubscriptions.add(subscription);

  void (async () => {
    const started = await ensureStarted();
    // Bağlantı zaten kuruluysa grup burada açılır; kurulmakta ise `ensureStarted` bağlanınca hepsini açar.
    if (started && isFirst) await invokeMethod(DiagramHubMethods.subscribeAutomations);
  })();

  return () => {
    overviewSubscriptions.delete(subscription);
    if (overviewSubscriptions.size === 0) void invokeMethod(DiagramHubMethods.unsubscribeAutomations);
    scheduleCloseIfIdle();
  };
}
