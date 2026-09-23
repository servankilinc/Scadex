/**
 * Resimli kartların (Kabinler, Canlı İzleme kabin seçimi) üzerindeki rozet ve düğmeler: yarı saydam, arkası
 * bulanık "cam" yüzey — resmin üstündeki koyu katmanda okunur kalır, resmi tamamen örtmez.
 *
 * Düğmede `outline` varyantının koyu tema renkleri (`dark:bg-input/30`, `dark:border-input` …) de ezilir;
 * yoksa koyu temada opak kalırdı.
 */
export const GLASS_BADGE = 'border-white/30 bg-white/10 text-white backdrop-blur-sm';

export const GLASS_BUTTON =
  'border-white/30 bg-white/10 text-white backdrop-blur-sm hover:bg-white/20 hover:text-white dark:border-white/30 dark:bg-white/10 dark:hover:bg-white/20';
