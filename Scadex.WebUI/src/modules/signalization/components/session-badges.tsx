import { ShieldAlertIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  OperatorSessionStatus,
  OperatorSessionStatusLabels,
  SessionPhase,
  SessionPhaseLabels,
  flagsOf,
  isAlertFlag
} from '../models/enums';
import type { OperatorSessionOperatorDto } from '../models/session';

export function SessionStatusBadge({ status }: { status: OperatorSessionStatus }) {
  const variant =
    status === OperatorSessionStatus.Completed
      ? 'secondary'
      : status === OperatorSessionStatus.Open
        ? 'default'
        : status === OperatorSessionStatus.TimedOut
          ? 'destructive'
          : 'outline';

  return <Badge variant={variant}>{OperatorSessionStatusLabels[status] ?? `#${status}`}</Badge>;
}

export function SessionPhaseBadge({ phase }: { phase: SessionPhase }) {
  const variant = phase === SessionPhase.AwaitingCard ? 'outline' : phase === SessionPhase.Inside ? 'default' : 'secondary';
  return <Badge variant={variant}>{SessionPhaseLabels[phase] ?? `#${phase}`}</Badge>;
}

/**
 * Bayrak rozetleri. Güvenlik uyarıları (kartsız giriş, zorla açma) kırmızı ve kalkan ikonlu; diğerleri
 * nötr. Onay akışı yoktur — bayrak oturum kaydında kalıcıdır.
 */
export function SessionFlagBadges({ flags }: { flags: number }) {
  const list = flagsOf(flags);
  if (list.length === 0) return null;

  return (
    <div className='flex flex-wrap gap-1'>
      {list.map(info =>
        isAlertFlag(info.flag) ? (
          <Badge key={info.flag} variant='destructive' title={info.description}>
            <ShieldAlertIcon />
            {info.label}
          </Badge>
        ) : (
          <Badge key={info.flag} variant='outline' title={info.description}>
            {info.label}
          </Badge>
        )
      )}
    </div>
  );
}

/** Oturumdaki operatörler: ad, o anki kurum ve okutulan kart (enstantane). */
export function SessionOperatorList({ operators, compact = false }: { operators: OperatorSessionOperatorDto[]; compact?: boolean }) {
  if (operators.length === 0) return <span className='text-muted-foreground italic'>kart okutulmadı</span>;

  return (
    <ul className='flex flex-col gap-0.5'>
      {operators.map(op => (
        <li key={op.userId} className='min-w-0 truncate'>
          <span className='font-medium'>{op.fullName}</span>
          <span className='text-muted-foreground'> · {op.authorityName}</span>
          {!compact && <span className='ml-1.5 font-mono text-xs text-muted-foreground'>{op.cardIdRaw}</span>}
        </li>
      ))}
    </ul>
  );
}
