/** Dosya gezginleri (uzak + yerel) için ortak yol/boyut yardımcıları. */

/** Windows yol birleştirme (ters eğik çizgi). Uzak PC yolları Windows biçimindedir. */
export function joinPath(base: string | null, name: string): string {
  if (!base) return name;
  return base.endsWith('\\') || base.endsWith('/') ? base + name : `${base}\\${name}`;
}

/** İnsan okunur bayt (B/KB/MB/GB/TB). */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB', 'TB'];
  let value = bytes / 1024;
  let i = 0;
  while (value >= 1024 && i < units.length - 1) {
    value /= 1024;
    i++;
  }
  return `${value.toFixed(value >= 10 || i === 0 ? 0 : 1)} ${units[i]}`;
}
