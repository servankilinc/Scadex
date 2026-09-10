import { useEffect, useState } from 'react';
import { useBlocker } from 'react-router';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';

interface UnsavedChangesBlockerProps {
  isDirty: boolean;
  saveNow: () => Promise<void>;
}

/**
 * Kaydedilmemiş değişikliklerle sayfadan çıkılmasını engeller.
 *
 * **Otomatik kaydetme olmadığı için bu bileşen tek güvenlik ağı.**  kaydetmenin
 * tek tetikleyicisi kullanıcının düğmeye basması. 
 *
 * İki ayrı kaçış yolu var ve ikisi farklı mekanizma gerektiriyor:
 *
 * 1. **Uygulama içi gezinme** (`<Link>`, geri tuşu) — react-router `useBlocker`.
 *    Kullanıcıya sorulur ve seçim ONA bırakılır: kaydedip çık, kaydetmeden çık,
 *    ya da sayfada kal.
 *
 * 2. **Sekmeyi kapatma / yenileme** — `beforeunload`. Burada asenkron iş
 *    YAPILAMAZ: tarayıcı bekletmez. `sendBeacon` da çare değil, çünkü kaydetme
 *    Authorization header'ı ve bir yanıt (idMap) gerektiriyor. Yapılabilecek tek
 *    şey tarayıcının kendi onay diyaloğunu tetiklemek.
 */
export function UnsavedChangesBlocker({ isDirty, saveNow }: UnsavedChangesBlockerProps) {
  const blocker = useBlocker(isDirty);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!isDirty) return;

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      // Eski tarayıcılar için; modern olanlar preventDefault'a bakıyor.
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [isDirty]);

  const open = blocker.state === 'blocked';

  const saveAndLeave = async () => {
    setIsSaving(true);
    try {
      await saveNow();
    } finally {
      setIsSaving(false);
    }
    // Kaydetme BAŞARISIZ olduysa çıkılmaz: diyalog açık kalır ve hata mesajı
    // toolbar'da görünür. Yine de çıkmak isteyen "Kaydetmeden çık" diyebilir.
    if (!isDirty) blocker.proceed?.();
  };

  return (
    <AlertDialog open={open} onOpenChange={next => !next && blocker.reset?.()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Kaydedilmemiş değişiklikler var</AlertDialogTitle>
          <AlertDialogDescription>
            Bu diyagramda kaydedilmemiş değişiklikler var. Kaydetmeden çıkarsanız bu değişiklikler kaybolur.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={() => blocker.reset?.()}>Sayfada kal</AlertDialogCancel>
          <Button variant='outline' disabled={isSaving} onClick={() => blocker.proceed?.()}>
            Kaydetmeden çık
          </Button>
          <AlertDialogAction disabled={isSaving} onClick={event => { event.preventDefault(); void saveAndLeave(); }}>
            {isSaving ? 'Kaydediliyor…' : 'Kaydet ve çık'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
