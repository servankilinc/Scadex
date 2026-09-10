import { AlertTriangleIcon, CheckIcon, LoaderCircleIcon, SaveIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { SaveController } from '@/hooks/use-diagram-save';

interface SaveIndicatorProps {
  save: SaveController;
  isDirty: boolean;
}

/**
 * Kaydetme durumunun ve Kaydet düğmesinin TEK yeri.
 *
 * **Editör kendiliğinden kaydetmiyor.** Düğme bu yüzden bir kolaylık değil, tek
 * yol: basılmadıkça hiçbir değişiklik sunucuya gitmez. Göstergenin işi de bunun
 * sonucu olarak değişti — "kaydedildi mi?" belirsizliğini gidermek değil,
 * kaydedilmemiş iş olduğunu görünür tutmak.
 */
export function SaveIndicator({ save, isDirty }: SaveIndicatorProps) {
  if (save.status === 'saving') {
    return (
      <span className='text-muted-foreground flex items-center gap-1.5 text-xs'>
        <LoaderCircleIcon className='size-3.5 animate-spin' />
        Kaydediliyor…
      </span>
    );
  }

  if (save.status === 'error') {
    return (
      <div className='flex items-center gap-2'>
        <span className='text-destructive flex items-center gap-1.5 text-xs' title={save.errorMessage ?? undefined}>
          <AlertTriangleIcon className='size-3.5' />
          {/* Mesaj kırpılır: sunucunun doğrulama metni uzun olabilir, tam hali
              title'da durur. Toolbar'ı taşırmak diyagramın yerini yerdi. */}
          <span className='max-w-40 truncate'>{save.errorMessage ?? 'Kaydedilemedi'}</span>
        </span>
        <Button size='xs' variant='destructive' onClick={() => void save.saveNow()}>
          Tekrar dene
        </Button>
      </div>
    );
  }

  if (isDirty) {
    return (
      <div className='flex items-center gap-2'>
        <span className='text-muted-foreground text-xs'>Kaydedilmemiş değişiklik</span>
        {/* Birincil görünüm: bu düğme artık kaydetmenin TEK yolu, ikincil bir
            kısayol değil. */}
        <Button size='xs' onClick={() => void save.saveNow()}>
          <SaveIcon /> Kaydet
        </Button>
      </div>
    );
  }

  if (save.lastSavedAt) {
    return (
      <span className='text-muted-foreground flex items-center gap-1.5 text-xs'>
        <CheckIcon className='size-3.5' />
        {new Date(save.lastSavedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })} · kaydedildi
      </span>
    );
  }

  // Bu oturumda hiç değişiklik yapılmadı — söylenecek bir şey yok. Boş bir
  // "kaydedildi" göstermek, olmayan bir gönderiyi ima ederdi.
  return null;
}
