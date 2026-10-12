import { useSearchParams } from 'react-router';

/** Filtre seçimlerinde "hepsi" — Base UI Select boş string'i "seçim yok" sayar. URL'ye hiç yazılmaz. */
export const REPORT_ALL = 'all';

/**
 * Rapor ekranlarının filtre/sekme/sayfa durumu URL'dedir: yenileme ve paylaşılan bağlantı aynı görünümü açar (PC detayındaki
 * "Kayıtlar" bağlantısı `?deviceId=` ile süzülü açar). İki rapor ekranı da bu kancayı kullanır — davranışları ayrışmasın.
 */
export function useReportParams() {
  const [searchParams, setSearchParams] = useSearchParams();

  /** Parametre yoksa `fallback` (varsayılan: "hepsi"). */
  const get = (key: string, fallback: string = REPORT_ALL) => searchParams.get(key) ?? fallback;

  /**
   * Boş / "hepsi" / `null` değer parametreyi siler. Filtre değişince sayfa başa döner (sayfa açıkça verilmedikçe) — eski sayfa
   * numarası yeni sonuçta olmayabilir. `replace`: filtre oynamak tarayıcı geçmişini doldurmasın.
   */
  const update = (patch: Record<string, string | null>) =>
    setSearchParams(
      current => {
        const next = new URLSearchParams(current);
        if (!('page' in patch)) next.delete('page');
        for (const [key, value] of Object.entries(patch)) {
          if (value == null || value === '' || value === REPORT_ALL) next.delete(key);
          else next.set(key, value);
        }
        return next;
      },
      { replace: true }
    );

  return { get, update, has: (key: string) => searchParams.has(key) };
}

/** `datetime-local` girdisi (yerel saat) → sunucunun beklediği UTC ISO. Boş ya da geçersizse `null`. */
export function localToUtcIso(local: string): string | null {
  if (!local) return null;
  const parsed = new Date(local);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

/** `Date` → `datetime-local` girdisinin beklediği yerel `YYYY-MM-DDTHH:mm`. */
export function toDateTimeLocal(date: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
