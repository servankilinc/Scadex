import { useState } from 'react';
import { LoaderCircleIcon, TriangleAlertIcon } from 'lucide-react';
import { toast } from 'sonner';
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
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Textarea } from '@/components/ui/textarea';
import { toApiError } from '@/lib/axios-helper';
import { useCloseSession } from '../hooks/use-operator-sessions';
import { CLOSE_REASON_MAX_LENGTH, operatorSessionCloseSchema } from '../models/session';

interface CloseSessionDialogProps {
  /** Kapatılacak işlem; `null` iken diyalog kapalı. */
  session: { id: number; outerDoorName: string } | null;
  onClose: () => void;
}

/**
 * Açık işlemi elle kapatma onayı. Gerekçe zorunludur ve zaman çizelgesinde kapatanla birlikte görünür.
 *
 * İstek motor bitirene kadar bloklar (otomatik kilitleme SCADA'yı bekleyebilir); bu yüzden diyalog `isPending` boyunca
 * açık ve kilitli kalır — aynı işlem ikinci kez gönderilemesin.
 */
export function CloseSessionDialog({ session, onClose }: CloseSessionDialogProps) {
  const closeSession = useCloseSession();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const reset = () => {
    setReason('');
    setError(null);
    closeSession.reset();
  };

  const dismiss = () => {
    if (closeSession.isPending) return;
    reset();
    onClose();
  };

  const submit = () => {
    if (!session) return;

    const parsed = operatorSessionCloseSchema.safeParse({ reason });
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Geçersiz gerekçe');
      return;
    }

    setError(null);
    closeSession.mutate(
      { id: session.id, request: parsed.data },
      {
        onSuccess: () => {
          toast.success(`İşlem #${session.id} kapatıldı.`);
          reset();
          onClose();
        },
        onError: err => setError(toApiError(err).message)
      }
    );
  };

  return (
    <AlertDialog open={session !== null} onOpenChange={open => !open && dismiss()}>
      <AlertDialogContent>
        {session && (
          <>
            <AlertDialogHeader>
              <AlertDialogTitle>
                İşlem #{session.id} — {session.outerDoorName}
              </AlertDialogTitle>
              <AlertDialogDescription>Devam eden işlem elle kapatılacak ve durumu "Elle kapatıldı" olacak.</AlertDialogDescription>
            </AlertDialogHeader>

            <p className='flex items-start gap-2 rounded-lg bg-amber-500/10 p-3 text-xs text-amber-700 dark:text-amber-400'>
              <TriangleAlertIcon className='mt-0.5 size-3.5 shrink-0' />
              <span>
                Sahaya komut gider: işlemin siren talebi bırakılır ve dış kapının ardındaki kilitsiz iç kapılar otomatik kilitlenmeye
                çalışılır. Açık duran kapı kilitlenmez, "iç kapı açık kaldı" olarak işaretlenir.
              </span>
            </p>

            <Field data-invalid={error ? true : undefined}>
              <FieldLabel htmlFor='close-session-reason'>Gerekçe</FieldLabel>
              <Textarea
                id='close-session-reason'
                value={reason}
                maxLength={CLOSE_REASON_MAX_LENGTH}
                disabled={closeSession.isPending}
                aria-invalid={error ? true : undefined}
                placeholder='Örn. dış kapı anahtarı arızalı, operatör sahadan ayrıldı'
                onChange={e => {
                  setReason(e.target.value);
                  if (error) setError(null);
                }}
              />
              <div className='flex items-start justify-between gap-2'>
                {error ? <FieldError>{error}</FieldError> : <span />}
                <span className='shrink-0 text-xs text-muted-foreground tabular-nums'>
                  {reason.length}/{CLOSE_REASON_MAX_LENGTH}
                </span>
              </div>
            </Field>

            <AlertDialogFooter>
              <AlertDialogCancel disabled={closeSession.isPending}>Vazgeç</AlertDialogCancel>
              <Button variant='destructive' disabled={closeSession.isPending} onClick={submit}>
                {closeSession.isPending && <LoaderCircleIcon className='animate-spin' />}
                İşlemi kapat
              </Button>
            </AlertDialogFooter>
          </>
        )}
      </AlertDialogContent>
    </AlertDialog>
  );
}
