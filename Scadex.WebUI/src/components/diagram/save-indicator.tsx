import { AlertTriangleIcon, CheckIcon, LoaderCircleIcon } from 'lucide-react';
import type { DiagramActivity } from '@/hooks/use-diagram-editor';

/**
 * İstek durumu rozeti. Editör her eylemi anında gönderir (Kaydet düğmesi yok); rozetin işi kullanıcıya isteğin gidip
 * gitmediğini göstermek. Başarısız istek ayrıca toast olarak görünür ve tuval sunucu gerçeğine döner — burada yalnızca
 * son hata özetlenir, "tekrar dene" yoktur (yeniden denenecek bekleyen iş kalmaz).
 */
export function SaveIndicator({ activity }: { activity: DiagramActivity }) {
  if (activity.pending > 0) {
    return (
      <span className='text-muted-foreground flex items-center gap-1.5 text-xs'>
        <LoaderCircleIcon className='size-3.5 animate-spin' />
        Kaydediliyor…
      </span>
    );
  }

  if (activity.error) {
    return (
      <span className='text-destructive flex items-center gap-1.5 text-xs' title={activity.error}>
        <AlertTriangleIcon className='size-3.5' />
        {/* Mesaj kırpılır: sunucunun doğrulama metni uzun olabilir, tam hali title'da durur. */}
        <span className='max-w-40 truncate'>{activity.error}</span>
      </span>
    );
  }

  if (activity.lastSavedAt) {
    return (
      <span className='text-muted-foreground flex items-center gap-1.5 text-xs'>
        <CheckIcon className='size-3.5' />
        {new Date(activity.lastSavedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })} · kaydedildi
      </span>
    );
  }

  // Bu oturumda hiç değişiklik yapılmadı — söylenecek bir şey yok.
  return null;
}
