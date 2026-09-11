import { toast } from 'sonner';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { toApiError } from '@/lib/axios-helper';

/** Süre metni: `dk:sn`, bir saati aşınca `sa:dk:sn`. Değer yoksa `—`. */
export function formatDuration(totalSec: number | null | undefined): string {
  if (totalSec == null || Number.isNaN(totalSec)) return '—';

  const sec = Math.max(0, Math.round(totalSec));
  const hours = Math.floor(sec / 3600);
  const minutes = Math.floor((sec % 3600) / 60);
  const seconds = sec % 60;
  const pad = (n: number) => n.toString().padStart(2, '0');

  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${pad(minutes)}:${pad(seconds)}`;
}

/** `datetime-local` / `date` girdisi (yerel saat) → sunucunun beklediği UTC ISO. Boşsa `null`. */
export function localToUtcIso(local: string): string | null {
  if (!local) return null;

  const parsed = new Date(local);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}

/**
 * Sunucu hata anahtarı → react-hook-form yolu:
 * `OuterDoors[0].InnerDoors[1].LockIoChannelId` → `outerDoors.0.innerDoors.1.lockIoChannelId`.
 *
 * Çekirdekteki `handleFormApiError` yalnızca İLK harfi küçültür; iç içe dizi yollarını çözmez (çekirdek
 * formları düzdür). Ağaç formu bu yüzden kendi çeviricisini kullanır.
 */
export function toFormPath(serverKey: string): string {
  return serverKey
    .replace(/\[(\d+)\]/g, '.$1')
    .split('.')
    .map(segment => (segment ? segment.charAt(0).toLowerCase() + segment.slice(1) : segment))
    .join('.');
}

/**
 * Ağaç formlarının hata politikası: alan hataları ilgili girdinin altına yazılır VE ilk mesaj bir toast olarak
 * da gösterilir — hata kapanmış/kaydırılmış bir kapı kartında kalabilir, kullanıcı kaydın neden gitmediğini
 * görmeli.
 */
export function handleTreeFormApiError<TFieldValues extends FieldValues>(error: unknown, setError: UseFormSetError<TFieldValues>): void {
  const apiError = toApiError(error);
  toast.error(apiError.message);

  // `fieldErrors` anahtarlarının ilk harfi transport katmanında zaten küçültülmüş; kalan segmentler burada.
  for (const [field, messages] of Object.entries(apiError.fieldErrors)) {
    setError(toFormPath(field) as Path<TFieldValues>, { message: messages[0] });
  }
}
