import { Link } from 'react-router';
import { CreditCardIcon, ExternalLinkIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { formatUtcDateTime } from '@/lib/utils';
import { useCabinetCardReads } from '../../hooks/use-virtual-cabinet';
import { SessionEventType, formatEventDetail } from '../../models/enums';
import type { SignalCardReadDto } from '../../models/session';

/**
 * Kart okuyucunun son okumaları. Liste yalnızca açıkken çekilir ve açıkken gelen her okumada kendiliğinden tazelenir
 * (`useVirtualCabinetLive` anahtarı invalidate eder).
 *
 * Açık oturum yokken reddedilen kart kayda geçmediği için burada görünmez — okuyucu efekti yine oynar.
 */
interface CardReadsDialogProps {
  cabinetId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CardReadsDialog({ cabinetId, open, onOpenChange }: CardReadsDialogProps) {
  const { data, isPending, error } = useCabinetCardReads(cabinetId, open);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='sm:max-w-lg'>
        <DialogHeader>
          <DialogTitle>Kart okuyucu</DialogTitle>
          <DialogDescription>Bu kabinde okutulan son kartlar. Oturum yokken reddedilen kartlar kayda geçmez.</DialogDescription>
        </DialogHeader>

        {error ? (
          <p className='text-sm text-destructive'>{error.message}</p>
        ) : isPending ? (
          <div className='flex flex-col gap-2'>
            {Array.from({ length: 3 }, (_, index) => (
              <Skeleton key={index} className='h-16 w-full' />
            ))}
          </div>
        ) : data.length === 0 ? (
          <p className='py-6 text-center text-sm text-muted-foreground'>Bu kabinde henüz kart okutulmadı.</p>
        ) : (
          <ul className='flex flex-col gap-2'>
            {data.map(read => (
              <CardReadRow key={read.id} read={read} />
            ))}
          </ul>
        )}
      </DialogContent>
    </Dialog>
  );
}

function CardReadRow({ read }: { read: SignalCardReadDto }) {
  const reason = read.isAccepted ? null : formatEventDetail(SessionEventType.AccessDenied, read.detail);

  return (
    <li className='flex items-start gap-3 rounded-lg border p-3'>
      <CreditCardIcon className={read.isAccepted ? 'mt-0.5 size-4 shrink-0 text-emerald-600' : 'mt-0.5 size-4 shrink-0 text-destructive'} />

      <div className='flex min-w-0 flex-1 flex-col gap-0.5'>
        <div className='flex flex-wrap items-center gap-2'>
          <span className='truncate font-medium'>{read.userFullName ?? 'Tanımsız kart'}</span>
          <Badge variant={read.isAccepted ? 'secondary' : 'destructive'}>{read.isAccepted ? 'Kabul' : 'Red'}</Badge>
        </div>

        <p className='text-xs text-muted-foreground'>
          {[read.authorityName, read.innerDoorName].filter(Boolean).join(' · ') || reason || '—'}
        </p>
        {read.isAccepted && read.detail && <p className='text-xs text-muted-foreground'>{formatEventDetail(SessionEventType.CardPresented, read.detail)}</p>}

        <p className='text-xs text-muted-foreground'>
          <span className='font-mono'>{read.cardIdRaw ?? '—'}</span> · {formatUtcDateTime(read.occurredAtUtc)}
        </p>
      </div>

      <Link
        to={`/signalization/sessions/${read.sessionId}`}
        className='shrink-0 text-muted-foreground hover:text-foreground'
        aria-label='Okumanın ait olduğu işlemi aç'
        title='İşlemi aç'
      >
        <ExternalLinkIcon className='size-4' />
      </Link>
    </li>
  );
}
