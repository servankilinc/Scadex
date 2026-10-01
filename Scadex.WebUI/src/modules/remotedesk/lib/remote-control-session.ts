import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { API_BASE_URL } from '@/lib/axios-helper';
import { getAccessToken } from '@/lib/auth-session';
import {
  ControlRequestStatus,
  InputEventType,
  MouseButton,
  RemoteControlEndReasonLabels,
  type ControlEndedNotice,
  type InputEvent,
  type RequestControlResponse
} from '../models/control';

/**
 * Bir PC'nin uzaktan kontrolü (RemoteDesk.md § 12: fare Faz 8, klavye Faz 9). React'in dışında; `use-remote-control.ts` yaşam döngüsünü bağlar.
 *
 * - Bağlantı `/hubs/remote-desk/viewer`'a, oturum başına ayrı açılır ve bitince kapanır: kontrol nadir ve kısa bir iştir, sürekli açık
 *   bir soket gerekmez. Otomatik yeniden bağlanma YOK: bağlantı koparsa sunucu kontrolü zaten bitirir, kullanıcı yeniden ister.
 * - Olaylar ~60 Hz (16 ms) paketlerle `send` ile (yanıt beklemeden) gider. Paket içinde ardışık hareketlerden yalnızca sonuncusu kalır;
 *   düğme ve tekerlek olayları asla düşmez, sıra korunur.
 * - Koordinatlar video kutusuna değil `object-fit: contain` ile ÇİZİLEN görüntüye göre [0,1]'e çevrilir (letterbox payı çıkar).
 * - Klavye: katman odaktayken `keydown/keyup` `KeyboardEvent.code` ile gider (karakter değil — PC kendi düzeniyle yorumlar). Tarayıcının
 *   varsayılanı engellenir; yine de Win, Alt+Tab, Ctrl+W/T/N gibi kısayolları tarayıcı/işletim sistemi yakalar — onlar için `sendCombo` ya da
 *   tam ekranda Keyboard Lock (Chromium). Ctrl+Alt+Del hiçbir yoldan gönderilemez.
 * - Katman odağı, pencere odağı ya da sekme görünürlüğü kaybolunca basılı düğme ve tuşlar bırakılır (§ 12.5).
 */

export type RemoteControlState = 'requesting' | 'active' | 'ended';

export interface RemoteControlOptions {
  deviceId: string;
  monitorIndex: number;
  /** Fare olaylarını yakalayan katman (videonun üstünde, aynı kutu). */
  surface: HTMLElement;
  video: HTMLVideoElement;
  /** `ended`'de `message` nedeni taşır (red, sunucunun bitirmesi, kopma). */
  onState: (state: RemoteControlState, message?: string) => void;
}

export interface RemoteControlHandle {
  /** Kontrolü bırakır ve bağlantıyı kapatır. Birden çok kez çağrılabilir. */
  stop: () => void;
  /** Tarayıcının yakalayamadığı kombinasyon (ör. `['MetaLeft']`, `['AltLeft', 'Tab']`): sırayla basılır, ters sırayla bırakılır. */
  sendCombo: (codes: string[]) => void;
}

const FLUSH_MS = 16;

export function startRemoteControl(options: RemoteControlOptions): RemoteControlHandle {
  const { deviceId, monitorIndex, surface, video, onState } = options;

  let connection: HubConnection | null = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/remote-desk/viewer`, { accessTokenFactory: () => getAccessToken() ?? '' })
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build();

  let controlSessionId: string | null = null;
  let finished = false;
  let seq = 0;
  let queue: InputEvent[] = [];
  let flushTimer: number | null = null;
  const pressed = new Set<MouseButton>();
  const pressedKeys = new Set<string>();

  // ------------------------------------------------------------ bitiş

  function finish(message?: string, release = true): void {
    if (finished) return;
    finished = true;
    detach();

    const conn = connection;
    connection = null;
    const id = controlSessionId;
    if (conn) {
      void (async () => {
        try {
          if (release && id && conn.state === HubConnectionState.Connected) {
            releasePressed();
            flush(conn);
            await conn.invoke('ReleaseControl', id);
          }
        } catch {
          // Bağlantı kopmuşsa sunucu kontrolü zaten bitirir.
        } finally {
          await conn.stop().catch(() => undefined);
        }
      })();
    }
    onState('ended', message);
  }

  // ------------------------------------------------------------ gönderme

  function push(event: Omit<InputEvent, 'seq' | 't'>): void {
    const next: InputEvent = { ...event, seq: ++seq, t: Date.now() };
    const last = queue[queue.length - 1];
    // Son-durum: bekleyen son olay da hareketse yerine yaz.
    if (next.type === InputEventType.Move && last?.type === InputEventType.Move) queue[queue.length - 1] = next;
    else queue.push(next);
  }

  function flush(conn: HubConnection | null = connection): void {
    if (!conn || !controlSessionId || queue.length === 0 || conn.state !== HubConnectionState.Connected) return;
    const events = queue;
    queue = [];
    void conn.send('SendInput', { controlSessionId, monitorIndex, events }).catch(() => undefined);
  }

  function releasePressed(): void {
    for (const button of pressed) push({ type: InputEventType.Up, button });
    pressed.clear();
    for (const code of pressedKeys) push({ type: InputEventType.KeyUp, code });
    pressedKeys.clear();
  }

  // ------------------------------------------------------------ koordinat

  /** Ekran noktası → çizilen görüntüye göre [0,1]. Görüntü yoksa `null`. */
  function normalize(clientX: number, clientY: number): { x: number; y: number } | null {
    const vw = video.videoWidth;
    const vh = video.videoHeight;
    if (!vw || !vh) return null;
    const rect = video.getBoundingClientRect();
    const scale = Math.min(rect.width / vw, rect.height / vh);
    const width = vw * scale;
    const height = vh * scale;
    const left = rect.left + (rect.width - width) / 2;
    const top = rect.top + (rect.height - height) / 2;
    return { x: (clientX - left) / width, y: (clientY - top) / height };
  }

  const inside = (p: { x: number; y: number }) => p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
  const clamp = (v: number) => Math.min(1, Math.max(0, v));
  const toButton = (b: number): MouseButton | null => (b === 0 || b === 1 || b === 2 ? b : null);

  // ------------------------------------------------------------ olaylar

  function onPointerMove(e: PointerEvent): void {
    const p = normalize(e.clientX, e.clientY);
    if (p && inside(p)) push({ type: InputEventType.Move, x: p.x, y: p.y });
  }

  function onPointerDown(e: PointerEvent): void {
    const button = toButton(e.button);
    const p = normalize(e.clientX, e.clientY);
    if (button === null || !p || !inside(p)) return;
    e.preventDefault();
    // Yakalama: düğme görüntünün dışında bırakılsa da `pointerup` bize gelsin (takılı düğme olmasın).
    surface.setPointerCapture(e.pointerId);
    // Klavye olayları katmana gelsin.
    surface.focus({ preventScroll: true });
    pressed.add(button);
    push({ type: InputEventType.Down, button, x: p.x, y: p.y });
    flush();
  }

  function onPointerUp(e: PointerEvent): void {
    const button = toButton(e.button);
    if (button === null || !pressed.has(button)) return;
    e.preventDefault();
    pressed.delete(button);
    const p = normalize(e.clientX, e.clientY);
    push(p ? { type: InputEventType.Up, button, x: clamp(p.x), y: clamp(p.y) } : { type: InputEventType.Up, button });
    flush();
  }

  function onWheel(e: WheelEvent): void {
    e.preventDefault();
    // Windows birimine (120 = bir çentik): Chromium'da bir çentik ≈ 100 px; satır modunda 3 satır = bir çentik.
    const factor = e.deltaMode === WheelEvent.DOM_DELTA_PIXEL ? 1.2 : e.deltaMode === WheelEvent.DOM_DELTA_LINE ? 40 : 120;
    const deltaX = Math.round(e.deltaX * factor);
    const deltaY = Math.round(e.deltaY * factor);
    if (deltaX === 0 && deltaY === 0) return;
    push({ type: InputEventType.Wheel, deltaX: deltaX || undefined, deltaY: deltaY || undefined });
  }

  const onContextMenu = (e: Event) => e.preventDefault();

  function onKeyDown(e: KeyboardEvent): void {
    if (!e.code || e.isComposing) return;
    e.preventDefault();
    e.stopPropagation();
    // Basılı tutulunca tarayıcı tekrar gönderir; o da iletilir (Windows enjekte edilen tuşu kendisi tekrarlamaz).
    pressedKeys.add(e.code);
    push({ type: InputEventType.KeyDown, code: e.code });
    flush();
  }

  function onKeyUp(e: KeyboardEvent): void {
    if (!e.code || !pressedKeys.has(e.code)) return;
    e.preventDefault();
    e.stopPropagation();
    pressedKeys.delete(e.code);
    push({ type: InputEventType.KeyUp, code: e.code });
    flush();
  }

  function onSurfaceBlur(): void {
    releasePressed();
    flush();
  }

  function sendCombo(codes: string[]): void {
    if (finished || !controlSessionId) return;
    for (const code of codes) push({ type: InputEventType.KeyDown, code });
    for (const code of [...codes].reverse()) push({ type: InputEventType.KeyUp, code });
    flush();
    surface.focus({ preventScroll: true });
  }

  function onFocusLost(): void {
    if (document.visibilityState === 'hidden' || !document.hasFocus()) {
      releasePressed();
      flush();
    }
  }

  function attach(): void {
    surface.addEventListener('pointermove', onPointerMove);
    surface.addEventListener('pointerdown', onPointerDown);
    surface.addEventListener('pointerup', onPointerUp);
    surface.addEventListener('wheel', onWheel, { passive: false });
    surface.addEventListener('contextmenu', onContextMenu);
    surface.addEventListener('keydown', onKeyDown);
    surface.addEventListener('keyup', onKeyUp);
    surface.addEventListener('blur', onSurfaceBlur);
    window.addEventListener('blur', onFocusLost);
    document.addEventListener('visibilitychange', onFocusLost);
    flushTimer = window.setInterval(() => flush(), FLUSH_MS);
  }

  function detach(): void {
    surface.removeEventListener('pointermove', onPointerMove);
    surface.removeEventListener('pointerdown', onPointerDown);
    surface.removeEventListener('pointerup', onPointerUp);
    surface.removeEventListener('wheel', onWheel);
    surface.removeEventListener('contextmenu', onContextMenu);
    surface.removeEventListener('keydown', onKeyDown);
    surface.removeEventListener('keyup', onKeyUp);
    surface.removeEventListener('blur', onSurfaceBlur);
    window.removeEventListener('blur', onFocusLost);
    document.removeEventListener('visibilitychange', onFocusLost);
    if (flushTimer !== null) window.clearInterval(flushTimer);
    flushTimer = null;
  }

  // ------------------------------------------------------------ başlat

  connection.on('ControlEnded', (notice: ControlEndedNotice) => {
    if (notice.controlSessionId === controlSessionId) finish(RemoteControlEndReasonLabels[notice.reason], false);
  });
  connection.onclose(() => finish('Kontrol bağlantısı koptu.', false));

  onState('requesting');
  void (async () => {
    try {
      const conn = connection;
      if (!conn) return;
      await conn.start();
      const response = await conn.invoke<RequestControlResponse>('RequestControl', deviceId);
      if (finished) return;
      if (response.status !== ControlRequestStatus.Granted || !response.controlSessionId) {
        finish(response.message ?? 'Kontrol verilmedi.', false);
        return;
      }
      controlSessionId = response.controlSessionId;
      attach();
      surface.focus({ preventScroll: true });
      onState('active');
    } catch (error) {
      // 401/403: izin yok ya da oturum düşmüş.
      const text = error instanceof Error ? error.message : String(error);
      finish(/\b40[13]\b/.test(text) ? 'Uzaktan kontrol izniniz yok.' : 'Kontrol alınamadı.', false);
    }
  })();

  return { stop: () => finish(undefined, true), sendCombo };
}
