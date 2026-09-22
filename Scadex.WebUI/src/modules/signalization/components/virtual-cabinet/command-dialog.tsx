import { LoaderCircleIcon, TriangleAlertIcon } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { formatUtcDateTime } from '@/lib/utils';
import type { SignalCabinetOutput } from '../../models/virtual-cabinet';

/**
 * Cihaza tıklanınca açılan onay adımı. Komut tek tıkla gitmez: bir sireni çaldırmak ya da bir kilidi açmak sahada
 * fiziksel bir sonuç doğurur.
 *
 * İstek SCADA cevaplayana kadar bloklar (kabinin komut zaman aşımı, 5-180 sn); bu yüzden diyalog `isPending` boyunca
 * açık ve kilitli kalır — kullanıcı aynı komutu ikinci kez gönderemesin.
 */
export interface CommandTarget {
  output: SignalCabinetOutput;
  /** Siren için `null`. */
  targetId: string | null;
  title: string;
  /** Cihazın o anki durumu (`null` = bilinmiyor) ve ne zamandan beri öyle olduğu. */
  isOn: boolean | null;
  changedAtUtc: string | null;
  /** `turnOn: true` ve `false` için buton metinleri. */
  onLabel: string;
  offLabel: string;
  /** O anki durumun okunuşu ("çalıyor", "kilitli"…). */
  stateLabel: string;
  /** Engel değil bilgi: komut yine de gönderilebilir (örn. kapı açıkken kilitleme). */
  warning?: string;
}

interface CommandDialogProps {
  target: CommandTarget | null;
  isPending: boolean;
  onClose: () => void;
  onSend: (turnOn: boolean) => void;
}

export function CommandDialog({ target, isPending, onClose, onSend }: CommandDialogProps) {
  return (
    <AlertDialog open={target !== null} onOpenChange={open => !open && !isPending && onClose()}>
      <AlertDialogContent>
        {target && (
          <>
            <AlertDialogHeader>
              <AlertDialogTitle>{target.title}</AlertDialogTitle>
              <AlertDialogDescription>
                Şu an <strong>{target.stateLabel}</strong>
                {target.changedAtUtc && ` — ${formatUtcDateTime(target.changedAtUtc)}'den beri`}.
              </AlertDialogDescription>
            </AlertDialogHeader>

            {target.warning && (
              <p className='flex items-start gap-2 rounded-lg bg-amber-500/10 p-3 text-xs text-amber-700 dark:text-amber-400'>
                <TriangleAlertIcon className='mt-0.5 size-3.5 shrink-0' />
                <span>{target.warning}</span>
              </p>
            )}

            <AlertDialogFooter>
              <AlertDialogCancel disabled={isPending}>Vazgeç</AlertDialogCancel>
              <Button variant='outline' disabled={isPending} onClick={() => onSend(false)}>
                {isPending && <LoaderCircleIcon className='animate-spin' />}
                {target.offLabel}
              </Button>
              <Button disabled={isPending} onClick={() => onSend(true)}>
                {isPending && <LoaderCircleIcon className='animate-spin' />}
                {target.onLabel}
              </Button>
            </AlertDialogFooter>
          </>
        )}
      </AlertDialogContent>
    </AlertDialog>
  );
}
