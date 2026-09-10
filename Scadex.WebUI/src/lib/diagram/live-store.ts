import { useCallback, useSyncExternalStore } from 'react';
import type { DeviceStatus } from '@/models/enums';
import type { CabinetStatusChange, ChannelValueChange, DeviceStatusChange } from '@/models/realtime';

/**
 * Canlı telemetrinin TEK durduğu yer.
 *
 * **TanStack Query cache'ine YAZILMAZ ve editör günlüğüne ASLA girmez.** Bu, kod
 * tabanının en kolay yapılan hatası olurdu: telemetri graf snapshot'ına yazılırsa
 * `useDiagramEditor` onu kullanıcı düzenlemesi sanar — `isDirty` true olur,
 * otomatik kaydetme tetiklenir ve editör SCADA değerlerini sunucuya geri yazmaya
 * çalışır.
 *
 * **Anahtar bazlı abonelik.** Dinleyiciler kanal/cihaz kimliği başına tutulur;
 * bir kanal değiştiğinde yalnızca o kanalı okuyan bileşen yeniden render olur.
 * Tek bir "store değişti" sinyali, 500 kanallı bir kabinde her tick'te tüm grafı
 * yeniden çizdirirdi.
 *
 * Desen `lib/auth-session.ts` ile aynı: `useSyncExternalStore`.
 *
 * Sözleşme: `Backend/docs/api-contract/09-realtime.md`
 */

export interface LiveChannel {
  value: string | null;
  updatedAt: string;
}

export interface LiveDevice {
  statusId: DeviceStatus | null;
  lastSeen: string | null;
}

export interface LiveCabinet {
  statusId: DeviceStatus | null;
  lastSeen: string | null;
  scadaLastIngestAt: string | null;
}

/**
 * Anahtar başına değer + anahtar başına dinleyici tutan küçük store.
 *
 * `useSyncExternalStore`'un `getSnapshot`'ı REFERANS OLARAK KARARLI bir değer
 * döndürmek zorunda; her okumada yeni nesne üretmek sonsuz render döngüsüne yol
 * açar. Bu yüzden değerler burada saklanır ve yalnızca gerçekten değiştiklerinde
 * yeni nesneyle DEĞİŞTİRİLİR.
 */
function createKeyedStore<T>() {
  const values = new Map<string, T>();
  const listeners = new Map<string, Set<() => void>>();

  function notify(key: string): void {
    const set = listeners.get(key);
    if (!set) return;
    for (const listener of set) listener();
  }

  return {
    get(key: string): T | undefined {
      return values.get(key);
    },

    set(key: string, next: T): void {
      values.set(key, next);
      notify(key);
    },

    subscribe(key: string, listener: () => void): () => void {
      let set = listeners.get(key);
      if (!set) listeners.set(key, (set = new Set()));
      set.add(listener);

      return () => {
        const current = listeners.get(key);
        if (!current) return;
        current.delete(listener);
        if (current.size === 0) listeners.delete(key);
      };
    },

    clear(): void {
      const keys = [...values.keys()];
      values.clear();
      // Temizlik de bir DEĞİŞİKLİKTİR: bildirilmezse bileşenler artık geçersiz
      // olan son değeri göstermeye devam eder.
      for (const key of keys) notify(key);
    }
  };
}

const channels = createKeyedStore<LiveChannel>();
const devices = createKeyedStore<LiveDevice>();
const cabinets = createKeyedStore<LiveCabinet>();

// ------------------------------------------------------------------- yazma

export function applyChannelValues(changes: ChannelValueChange[]): void {
  for (const change of changes) {
    channels.set(change.ioChannelId, { value: change.value, updatedAt: change.updatedAt });
  }
}

export function applyDeviceStatuses(changes: DeviceStatusChange[]): void {
  for (const change of changes) {
    devices.set(change.deviceId, { statusId: change.statusId, lastSeen: change.lastSeen });
  }
}

export function applyCabinetStatus(change: CabinetStatusChange): void {
  cabinets.set(change.cabinetId, {
    statusId: change.statusId,
    lastSeen: change.lastSeen,
    scadaLastIngestAt: change.scadaLastIngestAt
  });
}

/**
 * Tüm canlı değerleri atar.
 *
 * Kabin değiştirildiğinde ve bağlantı koptuğunda çağrılır. İkincisi kritik:
 * kopuk bir bağlantı sırasında saha değişmeye devam eder, ekrandaki değerler ise
 * donar. Donmuş bir sayıyı canlıymış gibi göstermek, hiç göstermemekten kötüdür —
 * operatör ona bakarak karar verir.
 */
export function resetLiveStore(): void {
  channels.clear();
  devices.clear();
  cabinets.clear();
}

// -------------------------------------------------- okuma (React dışı, imperatif)

export function getLiveChannel(ioChannelId: string): LiveChannel | undefined {
  return channels.get(ioChannelId);
}

export function getLiveDevice(deviceId: string): LiveDevice | undefined {
  return devices.get(deviceId);
}

export function getLiveCabinet(cabinetId: string): LiveCabinet | undefined {
  return cabinets.get(cabinetId);
}

// -------------------------------------------------------------- okuma (React)

export function useLiveChannel(ioChannelId: string | null | undefined): LiveChannel | undefined {
  const subscribe = useCallback(
    (listener: () => void) => (ioChannelId ? channels.subscribe(ioChannelId, listener) : NOOP),
    [ioChannelId]
  );
  const getSnapshot = useCallback(() => (ioChannelId ? channels.get(ioChannelId) : undefined), [ioChannelId]);
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

export function useLiveDevice(deviceId: string | null | undefined): LiveDevice | undefined {
  const subscribe = useCallback((listener: () => void) => (deviceId ? devices.subscribe(deviceId, listener) : NOOP), [deviceId]);
  const getSnapshot = useCallback(() => (deviceId ? devices.get(deviceId) : undefined), [deviceId]);
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

export function useLiveCabinet(cabinetId: string | null | undefined): LiveCabinet | undefined {
  const subscribe = useCallback((listener: () => void) => (cabinetId ? cabinets.subscribe(cabinetId, listener) : NOOP), [cabinetId]);
  const getSnapshot = useCallback(() => (cabinetId ? cabinets.get(cabinetId) : undefined), [cabinetId]);
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

/** Aboneliği olmayan durum için sabit temizleyici — her çağrıda yenisi üretilmesin. */
const NOOP = () => {};
