import { useState } from 'react';
import { CheckCircle2Icon, CreditCardIcon, LoaderCircleIcon, ScanLineIcon, TriangleAlertIcon } from 'lucide-react';
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
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Combobox, ComboboxContent, ComboboxEmpty, ComboboxInput, ComboboxItem, ComboboxList } from '@/components/ui/combobox';
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { toApiError } from '@/lib/axios-helper';
import { formatUtcDateTime, toSearchKey } from '@/lib/utils';
import { useCardEnrollment, type EnrollmentRead } from '../hooks/use-card-enrollment';
import { useAssignSignalOperatorCard, useSignalOperators } from '../hooks/use-signal-config';
import type { SignalOperatorDto } from '../models/operator';

/**
 * Kabin raporundaki "Kart tanımlama" — sahada operatörün kartını sisteme tanıtmak için.
 *
 * Pencere kabinin `SignalCardPresented` yayınını izler (`useCardEnrollment`); okuma operatör işlem motoruna NORMAL gider — baypas
 * bilerek yok (2026-10-13 kullanıcı kararı): tanımlı kart iç kapıyı açabilir, tanımsız kart açık işleme red satırı yazabilir.
 * Okutulan kart tanımlıysa sahibi gösterilir; değilse aranabilir listeden bir kullanıcıya atanır. Kullanıcının zaten kartı varsa
 * önce onay sorulur. Motor yalnızca yapılandırılmış ve etkin kabinde yayınladığı için düğme yalnızca orada görünür.
 *
 * Bileşen yalnızca açıkken bağlanır (`key` = kabin).
 */
interface CardEnrollmentDialogProps {
  cabinetId: string;
  cabinetName: string;
  onClose: () => void;
}

export function CardEnrollmentDialog({ cabinetId, cabinetName, onClose }: CardEnrollmentDialogProps) {
  const { read, hubStatus, markAssigned } = useCardEnrollment(cabinetId);

  return (
    <Dialog open onOpenChange={next => !next && onClose()}>
      <DialogContent className='sm:max-w-md'>
        <DialogHeader>
          <DialogTitle>Kart tanımlama — {cabinetName}</DialogTitle>
          <DialogDescription>
            Kartı bu kabinin kart okuyucusuna okutun. Okuma normal işlenir: tanımlı bir operatörün kartı iç kapı kilidini açabilir, tanımsız
            kart reddedildi olarak kaydedilebilir.
          </DialogDescription>
        </DialogHeader>

        {hubStatus !== 'connected' && (
          <p className='flex items-center gap-2 rounded-lg bg-amber-500/10 p-3 text-xs text-amber-700 dark:text-amber-400'>
            <LoaderCircleIcon className='size-4 shrink-0 animate-spin' />
            Canlı bağlantı kuruluyor; bağlantı kurulmadan okutulan kart burada görünmez.
          </p>
        )}

        {read ? (
          // Her okuma yeni bir panel: önceki okumanın kullanıcı seçimi yeni karta taşınmasın.
          <CardReadPanel key={read.seq} read={read} onAssigned={markAssigned} />
        ) : (
          <div className='flex flex-col items-center gap-2 rounded-lg border border-dashed py-8 text-muted-foreground'>
            <ScanLineIcon className='size-8 animate-pulse' />
            <span className='text-sm'>Kart okutulması bekleniyor…</span>
          </div>
        )}

        <DialogFooter>
          <DialogClose render={<Button variant='outline' />}>Kapat</DialogClose>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function CardReadPanel({ read, onAssigned }: { read: EnrollmentRead; onAssigned: (userId: string, userFullName: string) => void }) {
  return (
    <div className='flex flex-col gap-4 rounded-lg border p-4'>
      <div className='flex items-start gap-3'>
        <CreditCardIcon className='mt-0.5 size-5 shrink-0 text-muted-foreground' />
        <div className='flex min-w-0 flex-1 flex-col gap-0.5'>
          <span className='font-mono text-lg font-semibold break-all'>{read.cardIdRaw}</span>
          <span className='text-xs text-muted-foreground'>{formatUtcDateTime(read.occurredAtUtc)}</span>
        </div>
        {read.userId ? <Badge variant='secondary'>Tanımlı</Badge> : <Badge variant='outline'>Tanımsız kart</Badge>}
      </div>

      {read.userId ? (
        <p className='flex items-center gap-2 text-sm'>
          <CheckCircle2Icon className='size-4 shrink-0 text-emerald-600' />
          <span>
            Bu kart <strong>{read.userFullName}</strong> kullanıcısına tanımlı.
          </span>
        </p>
      ) : (
        <AssignCardForm cardId={read.cardIdRaw} onAssigned={onAssigned} />
      )}
    </div>
  );
}

function AssignCardForm({ cardId, onAssigned }: { cardId: string; onAssigned: (userId: string, userFullName: string) => void }) {
  const operators = useSignalOperators();
  const assign = useAssignSignalOperatorCard();
  const [selected, setSelected] = useState<SignalOperatorDto | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const submit = (user: SignalOperatorDto, replaceExisting: boolean) => {
    assign.mutate(
      { userId: user.userId, request: { identityCardId: cardId, replaceExisting } },
      {
        onSuccess: () => {
          setConfirmOpen(false);
          toast.success(`Kart ${user.fullName} kullanıcısına tanımlandı.`);
          onAssigned(user.userId, user.fullName);
        },
        onError: error => {
          setConfirmOpen(false);
          toast.error(toApiError(error).message);
        }
      }
    );
  };

  // Kullanıcının başka bir kartı varsa sessizce ezilmez: önce onay.
  const onAssignClick = () => {
    if (!selected) return;
    if (selected.identityCardId) setConfirmOpen(true);
    else submit(selected, false);
  };

  // Base UI'ın varsayılan filtresi (`Intl.Collator`) "ilker" yazınca "İlker"i bulmuyor — `toSearchKey` (home ekranıyla aynı).
  // Kutuda seçili kullanıcının adı dururken liste yalnızca ona daralmasın.
  const matchesUserQuery = (user: SignalOperatorDto, query: string) =>
    query === selected?.fullName || toSearchKey(`${user.fullName} ${user.userName ?? ''}`).includes(toSearchKey(query));

  if (operators.isError) return <p className='text-sm text-destructive'>Kullanıcılar alınamadı: {operators.error.message}</p>;

  return (
    <div className='flex flex-col gap-2'>
      <span className='text-sm font-medium'>Bu kartı bir kullanıcıya ata</span>
      <div className='flex gap-2'>
        <Combobox
          items={operators.data ?? []}
          value={selected}
          onValueChange={user => setSelected(user ?? null)}
          itemToStringLabel={user => user.fullName}
          isItemEqualToValue={(a, b) => a.userId === b.userId}
          filter={matchesUserQuery}
          autoHighlight
        >
          <ComboboxInput
            className='min-w-0 flex-1'
            placeholder={operators.isPending ? 'Kullanıcılar yükleniyor…' : 'Kullanıcı ara…'}
            disabled={operators.isPending || assign.isPending}
            showClear={selected !== null}
          />
          <ComboboxContent>
            <ComboboxEmpty>Kullanıcı bulunamadı.</ComboboxEmpty>
            <ComboboxList className='scrollbar-thin'>
              {(user: SignalOperatorDto) => (
                <ComboboxItem key={user.userId} value={user}>
                  <div className='flex min-w-0 flex-col'>
                    <span className='truncate'>{user.fullName}</span>
                    <span className='truncate text-xs text-muted-foreground'>
                      {[user.userName, user.identityCardId ? `Kart: ${user.identityCardId}` : 'Kartı yok'].filter(Boolean).join(' · ')}
                    </span>
                  </div>
                </ComboboxItem>
              )}
            </ComboboxList>
          </ComboboxContent>
        </Combobox>
        <Button onClick={onAssignClick} disabled={!selected || assign.isPending}>
          {assign.isPending && !confirmOpen && <LoaderCircleIcon className='animate-spin' />}
          Ata
        </Button>
      </div>

      {selected?.identityCardId && (
        <p className='flex items-start gap-2 text-xs text-amber-700 dark:text-amber-400'>
          <TriangleAlertIcon className='mt-0.5 size-3.5 shrink-0' />
          Bu kullanıcının zaten bir kartı var ({selected.identityCardId}); atama onay ister.
        </p>
      )}

      <AlertDialog open={confirmOpen} onOpenChange={open => !open && !assign.isPending && setConfirmOpen(false)}>
        <AlertDialogContent>
          {selected && (
            <>
              <AlertDialogHeader>
                <AlertDialogTitle>Mevcut kart değiştirilsin mi?</AlertDialogTitle>
                <AlertDialogDescription>
                  <strong>{selected.fullName}</strong> kullanıcısına tanımlı <span className='font-mono'>{selected.identityCardId}</span> kartı kaldırılıp{' '}
                  <span className='font-mono'>{cardId}</span> kartı atanacak. Eski kart artık bu kullanıcıyı tanımlamaz.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel disabled={assign.isPending}>Vazgeç</AlertDialogCancel>
                <Button disabled={assign.isPending} onClick={() => submit(selected, true)}>
                  {assign.isPending && <LoaderCircleIcon className='animate-spin' />}
                  Kartı değiştir
                </Button>
              </AlertDialogFooter>
            </>
          )}
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
