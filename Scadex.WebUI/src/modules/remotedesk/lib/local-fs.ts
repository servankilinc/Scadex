/**
 * File System Access API dar tip yüzeyi (dosya transferi penceresinin yerel paneli, RemoteDesk.md § 12.8). Gezilebilir/yazılabilir yerel
 * dosya yalnızca GÜVENLİ BAĞLAMDA (HTTPS/localhost, Chromium/Edge) çalışır — bu artık dağıtım gereğidir, kodda yedek/dallanma yoktur
 * (kullanıcı kararı 2026-10-06). Tipler lib.dom'da bulunmayabildiği için kullandığımız yüzey burada tanımlı (keyboardLock() kalıbı); `any` yok.
 */

export interface FsWritable {
  write(data: Blob): Promise<void>;
  close(): Promise<void>;
}

export interface FsFileHandle {
  kind: 'file';
  name: string;
  getFile(): Promise<File>;
  createWritable(): Promise<FsWritable>;
}

export interface FsDirHandle {
  kind: 'directory';
  name: string;
  entries(): AsyncIterableIterator<[string, FsFileHandle | FsDirHandle]>;
  getDirectoryHandle(name: string): Promise<FsDirHandle>;
  getFileHandle(name: string, options?: { create?: boolean }): Promise<FsFileHandle>;
}

/** Dialog'un düğmeler için ihtiyaç duyduğu yerel durum: aktif klasör (indirme hedefi) ve seçili dosya (yükleme kaynağı). */
export interface LocalState {
  dir: FsDirHandle | null;
  selected: { handle: FsFileHandle; name: string } | null;
}

export const pickDirectory = (): Promise<FsDirHandle> =>
  (window as unknown as { showDirectoryPicker: (o?: { mode?: string }) => Promise<FsDirHandle> }).showDirectoryPicker({ mode: 'readwrite' });
