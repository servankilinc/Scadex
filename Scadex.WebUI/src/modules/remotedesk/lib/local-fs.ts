/**
 * Dosya transferi penceresinin yerel paneli (RemoteDesk.md § 12.8) — İKİ MOD (2026-10-26, kullanıcı kararı: kapalı ağda HTTPS zorunlu değil):
 * - **Gezgin (`fsa`)**: File System Access API — yerel klasör ağacında gezilir, indirme seçili klasöre yazılır. Yalnızca GÜVENLİ BAĞLAMDA
 *   (HTTPS/localhost ya da tarayıcı ayarıyla güvenli sayılan adres) ve Chromium/Edge'de vardır.
 * - **Klasik (`classic`)**: güvenli bağlam ya da API yoksa (HTTP, Firefox) — dosya `<input type="file">`/sürükle-bırak ile seçilir, indirme
 *   tarayıcının indirme mekanizmasıyla kaydedilir. Klasör gezgini yoktur.
 * Tipler lib.dom'da bulunmayabildiği için kullandığımız yüzey burada tanımlı (keyboardLock() kalıbı); `any` yok.
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

export type LocalMode = 'fsa' | 'classic';

/** Yükleme kaynağı: gezginde dosya tutamacından, klasik modda seçilen `File`'dan okunur. */
export interface LocalSelection {
  name: string;
  getFile: () => Promise<File>;
}

/**
 * Dialog'un düğmeler için ihtiyaç duyduğu yerel durum: mod, aktif klasör (yalnızca gezginde — indirme hedefi) ve seçili dosya (yükleme
 * kaynağı). Klasik modda `dir` hep `null`'dır; indirme tarayıcının indirme klasörüne gider.
 */
export interface LocalState {
  mode: LocalMode;
  dir: FsDirHandle | null;
  selected: LocalSelection | null;
}

/** Yerel klasör gezgini kullanılabilir mi (güvenli bağlam + File System Access API). */
export const fsaAvailable = (): boolean => window.isSecureContext && 'showDirectoryPicker' in window;

export const pickDirectory = (): Promise<FsDirHandle> =>
  (window as unknown as { showDirectoryPicker: (o?: { mode?: string }) => Promise<FsDirHandle> }).showDirectoryPicker({ mode: 'readwrite' });

/**
 * Klasik mod indirmesi: tarayıcının indirme mekanizması (İndirilenler klasörü ya da tarayıcı "nereye kaydedileceğini sor" ayarındaysa
 * kaydetme penceresi). Güvenli bağlam istemez.
 */
export function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Tıklama indirmeyi başlattıktan sonra bırakılır; hemen bırakmak bazı tarayıcılarda indirmeyi iptal eder.
  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
