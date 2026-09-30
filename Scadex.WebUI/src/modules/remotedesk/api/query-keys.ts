/**
 * Modülün TanStack Query anahtarları — hepsi `['remoteDesk', …]` altında: modül çekirdek anahtarlarını uçurmaz, çekirdek
 * invalidation'ı modül verisine dokunmaz.
 */
const root = ['remoteDesk'] as const;

export const remoteDeskKeys = {
  all: root,
  /** PC listesi; bağlantı durumu 10 sn'de bir yoklanır (MVP'de tarayıcı hub'ı yok). */
  pcs: () => [...root, 'pcs'] as const,
  pc: (deviceId: string) => [...root, 'pcs', deviceId] as const
};
