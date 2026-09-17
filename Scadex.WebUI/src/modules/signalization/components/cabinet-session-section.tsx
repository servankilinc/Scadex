import { Link } from 'react-router';
import { CameraIcon, SirenIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { cn, formatUtcDateTime } from '@/lib/utils';
import type { CabinetPanelSectionProps } from '../../types';
import { useCabinetLatestSession } from '../hooks/use-operator-sessions';
import { useNow } from '../hooks/use-now';
import { formatDuration } from '../lib';
import type { OperatorSessionListItemDto, OperatorSessionOpenDto } from '../models/session';
import { OperatorIdCard } from './operator-id-card';
import { SessionFlagBadges, SessionPhaseBadge, SessionStatusBadge } from './session-badges';

/**
 * Ana sayfa haritasındaki kabin detay panelinin sinyalizasyon bölümü (modülün `CabinetPanelSection`'ı).
 *
 * Kabinde DEVAM EDEN işlem varsa onu (birden çok dış kapı → birden çok blok), yoksa en son biten işlemi
 * gösterir. Salt okunurdur; oturumu yalnızca motor yazar.
 */
export default function CabinetSessionSection({ cabinetId }: CabinetPanelSectionProps) {
  const { openSessions, lastSession, openUpdatedAt, isPending, isError, error, moduleOff } = useCabinetLatestSession(cabinetId);
  const now = useNow();

  // Backend'de modül kapalı: panel bu bölümü hiç göstermemeli (`VITE_MODULES` ayrı ayarlanır).
  if (moduleOff) return null;

  const isLive = openSessions.length > 0;

  // Yanıttan bu yana geçen yerel süre; sunucunun `elapsedSec`'ine eklenir — istemci saati kaymış olabilir.
  const sinceFetchSec = openUpdatedAt ? Math.max(0, Math.floor((now - openUpdatedAt) / 1000)) : 0;

  return (
    <section className='space-y-3 border-t pt-4'>
      <div className='flex items-center gap-2'>
        <p className='text-sm font-medium text-muted-foreground'>Operatör İşlemi</p>
        {isLive && (
          <>
            <span className='relative flex size-2'>
              <span className='absolute inline-flex size-full animate-ping rounded-full bg-emerald-500/60' />
              <span className='relative inline-flex size-2 rounded-full bg-emerald-500' />
            </span>
            <span className='text-xs text-muted-foreground'>canlı</span>
          </>
        )}
      </div>

      {isPending ? (
        <Skeleton className='h-28 w-full rounded-xl' />
      ) : isError ? (
        <p className='text-xs text-destructive'>{error?.message ?? 'İşlem bilgisi alınamadı.'}</p>
      ) : isLive ? (
        openSessions.map(session => (
          <SessionBlock key={session.id} session={session} elapsedSec={session.elapsedSec + sinceFetchSec} />
        ))
      ) : lastSession ? (
        <SessionBlock session={lastSession} />
      ) : (
        <p className='text-xs text-muted-foreground'>Bu kabinde kayıtlı operatör işlemi yok.</p>
      )}
    </section>
  );
}

/**
 * Tek bir işlem bloğu. `elapsedSec` verilirse işlem DEVAM EDİYOR demektir: süre saniyede bir ilerler ve
 * aşama rozeti gösterilir; verilmezse kapanmış kaydın toplam süresi yazılır.
 */
function SessionBlock({ session, elapsedSec }: { session: OperatorSessionListItemDto; elapsedSec?: number }) {
  const isLive = elapsedSec != null;
  const openSession = isLive ? (session as OperatorSessionOpenDto) : null;
  const sirenOn = openSession?.sirenRequested && openSession.cabinetSirenIsOn;

  return (
    <div className={cn('space-y-2.5 rounded-xl border p-3', session.hasAlert && 'ring-1 ring-destructive/50')}>
      <div className='flex items-center justify-between gap-2'>
        <span className='font-mono text-lg font-medium tabular-nums'>
          {formatDuration(isLive ? elapsedSec : session.durationSec)}
        </span>
        {openSession ? <SessionPhaseBadge phase={openSession.phase} /> : <SessionStatusBadge status={session.status} />}
      </div>

      <div className='space-y-0.5 text-xs text-muted-foreground'>
        <p className='truncate' title={session.outerDoorName}>
          {session.outerDoorName} · işlem #{session.id}
        </p>
        <p>Başlangıç: {formatUtcDateTime(session.startedAtUtc)}</p>
        {session.endedAtUtc && <p>Bitiş: {formatUtcDateTime(session.endedAtUtc)}</p>}
      </div>

      {(sirenOn || openSession?.sirenRequested || session.captureCount > 0) && (
        <div className='flex flex-wrap items-center gap-1.5'>
          {sirenOn ? (
            <Badge variant='destructive'>
              <SirenIcon className='animate-pulse' />
              Siren çalıyor
            </Badge>
          ) : (
            openSession?.sirenRequested && (
              <Badge variant='outline'>
                <SirenIcon />
                Siren talebi
              </Badge>
            )
          )}
          {session.captureCount > 0 && (
            <Badge variant='outline'>
              <CameraIcon />
              {session.captureCount}
            </Badge>
          )}
        </div>
      )}

      <SessionFlagBadges flags={session.flags} />

      {session.operators.length === 0 ? (
        <p className='text-xs text-muted-foreground italic'>kart okutulmadı</p>
      ) : (
        <div className='space-y-2'>
          {session.operators.map(operator => (
            <OperatorIdCard key={operator.userId} operator={operator} isLive={isLive} compact />
          ))}
        </div>
      )}

      <Button
        size='xs'
        variant='outline'
        className='w-full'
        nativeButton={false}
        render={<Link to={`/signalization/sessions/${session.id}`} />}
      >
        İşlem Detayı
      </Button>
    </div>
  );
}
