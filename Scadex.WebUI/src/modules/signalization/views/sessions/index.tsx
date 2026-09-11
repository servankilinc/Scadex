import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { CameraIcon, ChevronLeftIcon, ChevronRightIcon, RotateCcwIcon, SirenIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useCabinets } from '@/hooks/use-cabinets';
import { cn, formatUtcDateTime } from '@/lib/utils';
import { SessionFlagBadges, SessionOperatorList, SessionPhaseBadge, SessionStatusBadge } from '../../components/session-badges';
import { useNow } from '../../hooks/use-now';
import { useOpenSessions, useSessionList } from '../../hooks/use-operator-sessions';
import { useSignalAuthorities, useSignalOperators } from '../../hooks/use-signal-config';
import { formatDuration, localToUtcIso } from '../../lib';
import { ALERT_FLAGS, OperatorSessionStatus, OperatorSessionStatusLabels, SESSION_FLAG_LIST } from '../../models/enums';
import type { OperatorSessionListItemDto, OperatorSessionOpenDto } from '../../models/session';

/** Base UI Select boş string'i "seçim yok" sayar; "hepsi" için sentinel. */
const ALL = 'all';
/** Bayrak filtresinde "herhangi bir güvenlik uyarısı". */
const ANY_ALERT = 'alert';
const PAGE_SIZE = 25;
/** Canlı panel bu ekrandayken daha sık yoklanır; uyarı yoklayıcısıyla anahtar ortak, tek istek gider. */
const LIVE_PANEL_POLL_MS = 5_000;

const STATUS_OPTIONS: OperatorSessionStatus[] = [
  OperatorSessionStatus.Open,
  OperatorSessionStatus.Completed,
  OperatorSessionStatus.CompletedWithWarning,
  OperatorSessionStatus.TimedOut
];

/**
 * Operatör işlemleri — `/signalization/sessions`.
 *
 * Üstte CANLI panel (açık oturumlar, 5 sn yoklama), altta filtreli GEÇMİŞ (sayfalı, yoklamasız). İkisi ayrı
 * uçlardan beslenir: canlı liste yalnızca dış kapısı kapanmamış oturumları taşır.
 *
 * Ekran salt okunurdur — oturumu yalnızca motor yazar; onay/kapama gibi bir eylem bilerek yoktur.
 */
export default function OperatorSessions() {
  return (
    <div className='flex flex-col gap-6 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Operatör İşlemleri</h1>
        <p className='text-sm text-muted-foreground'>
          Dış kapının açılmasıyla başlayıp kapanmasıyla biten işlemler. Kayıtları yalnızca SCADA olayları üretir; buradan değiştirilemez.
        </p>
      </div>

      <LivePanel />
      <SessionHistory />
    </div>
  );
}

// ─────────────────────────────────────────────────────────── canlı panel

function LivePanel() {
  const open = useOpenSessions(LIVE_PANEL_POLL_MS);
  const now = useNow();

  // Yanıttan bu yana geçen yerel süre; sunucunun `elapsedSec`'ine eklenir.
  const sinceFetchSec = open.dataUpdatedAt ? Math.max(0, Math.floor((now - open.dataUpdatedAt) / 1000)) : 0;

  return (
    <section className='flex flex-col gap-3'>
      <div className='flex items-center gap-2'>
        <h2 className='font-medium'>Devam eden işlemler</h2>
        {open.data && <Badge variant={open.data.length > 0 ? 'default' : 'secondary'}>{open.data.length}</Badge>}
        <span className='relative ml-1 flex size-2'>
          <span className='absolute inline-flex size-full animate-ping rounded-full bg-emerald-500/60' />
          <span className='relative inline-flex size-2 rounded-full bg-emerald-500' />
        </span>
        <span className='text-xs text-muted-foreground'>canlı</span>
      </div>

      {open.isError && <p className='text-sm text-destructive'>{open.error.message}</p>}

      {open.isPending && (
        <div className='grid gap-3 sm:grid-cols-2 xl:grid-cols-3'>
          {Array.from({ length: 2 }, (_, i) => (
            <Skeleton key={i} className='h-36 w-full rounded-xl' />
          ))}
        </div>
      )}

      {open.data?.length === 0 && (
        <Card>
          <CardContent className='py-6 text-center text-sm text-muted-foreground'>Şu an devam eden işlem yok.</CardContent>
        </Card>
      )}

      {open.data && open.data.length > 0 && (
        <div className='grid gap-3 sm:grid-cols-2 xl:grid-cols-3'>
          {open.data.map(session => (
            <LiveSessionCard key={session.id} session={session} elapsedSec={session.elapsedSec + sinceFetchSec} />
          ))}
        </div>
      )}
    </section>
  );
}

function LiveSessionCard({ session, elapsedSec }: { session: OperatorSessionOpenDto; elapsedSec: number }) {
  const sirenOn = session.sirenRequested && session.cabinetSirenIsOn;

  return (
    <Card className={cn(session.hasAlert && 'ring-2 ring-destructive/60')}>
      <CardHeader>
        <CardTitle className='flex items-center justify-between gap-2'>
          <span className='truncate'>{session.outerDoorName}</span>
          <span className='font-mono text-base tabular-nums'>{formatDuration(elapsedSec)}</span>
        </CardTitle>
        <CardDescription className='truncate'>
          {session.cabinetName ?? '—'} · işlem #{session.id}
        </CardDescription>
      </CardHeader>

      <CardContent className='flex flex-col gap-3 text-sm'>
        <div className='flex flex-wrap items-center gap-1.5'>
          <SessionPhaseBadge phase={session.phase} />
          {sirenOn ? (
            <Badge variant='destructive'>
              <SirenIcon className='animate-pulse' />
              Siren çalıyor
            </Badge>
          ) : (
            session.sirenRequested && (
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

        <SessionOperatorList operators={session.operators} />
        <SessionFlagBadges flags={session.flags} />

        <div className='flex items-center justify-between gap-2 text-xs text-muted-foreground'>
          <span>Başlangıç: {formatUtcDateTime(session.startedAtUtc)}</span>
          <Button size='xs' variant='outline' nativeButton={false} render={<Link to={`/signalization/sessions/${session.id}`} />}>
            Detay
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── geçmiş

function SessionHistory() {
  const navigate = useNavigate();
  const cabinets = useCabinets();
  const authorities = useSignalAuthorities();
  const operators = useSignalOperators();

  const [cabinetId, setCabinetId] = useState(ALL);
  const [status, setStatus] = useState(ALL);
  const [flag, setFlag] = useState(ALL);
  const [authorityId, setAuthorityId] = useState(ALL);
  const [userId, setUserId] = useState(ALL);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);

  const cabinetOptions = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);

  const sessions = useSessionList({
    cabinetId: cabinetId === ALL ? null : cabinetId,
    status: status === ALL ? null : (Number(status) as OperatorSessionStatus),
    flags: flag === ALL ? null : flag === ANY_ALERT ? ALERT_FLAGS : Number(flag),
    authorityId: authorityId === ALL ? null : authorityId,
    userId: userId === ALL ? null : userId,
    fromUtc: localToUtcIso(from),
    toUtc: localToUtcIso(to),
    page,
    pageSize: PAGE_SIZE
  });

  const isFiltered = [cabinetId, status, flag, authorityId, userId].some(v => v !== ALL) || from !== '' || to !== '';

  /** Filtre değişince ilk sayfaya dönülür — eski sayfa numarası yeni sonuçta olmayabilir. */
  const change = (setter: (value: string) => void) => (value: string | null) => {
    setter(value ?? ALL);
    setPage(1);
  };

  const resetFilters = () => {
    setCabinetId(ALL);
    setStatus(ALL);
    setFlag(ALL);
    setAuthorityId(ALL);
    setUserId(ALL);
    setFrom('');
    setTo('');
    setPage(1);
  };

  return (
    <section className='flex flex-col gap-3'>
      <h2 className='font-medium'>Geçmiş</h2>

      <div className='flex flex-wrap items-end gap-3'>
        <FilterSelect
          id='session-cabinet'
          label='Kabin'
          value={cabinetId}
          onChange={change(setCabinetId)}
          allLabel='Tüm kabinler'
          options={cabinetOptions.map(c => ({ value: c.id, label: c.name }))}
        />
        <FilterSelect
          id='session-status'
          label='Durum'
          value={status}
          onChange={change(setStatus)}
          allLabel='Tüm durumlar'
          options={STATUS_OPTIONS.map(s => ({ value: String(s), label: OperatorSessionStatusLabels[s] }))}
        />
        <FilterSelect
          id='session-flag'
          label='Bayrak'
          value={flag}
          onChange={change(setFlag)}
          allLabel='Hepsi'
          options={[{ value: ANY_ALERT, label: 'Güvenlik uyarısı (herhangi)' }, ...SESSION_FLAG_LIST.map(f => ({ value: String(f.flag), label: f.label }))]}
        />
        <FilterSelect
          id='session-authority'
          label='Kurum'
          value={authorityId}
          onChange={change(setAuthorityId)}
          allLabel='Tüm kurumlar'
          options={(authorities.data ?? []).map(a => ({ value: a.id, label: a.isActive ? a.name : `${a.name} (pasif)` }))}
        />
        <FilterSelect
          id='session-operator'
          label='Operatör'
          value={userId}
          onChange={change(setUserId)}
          allLabel='Tüm operatörler'
          options={(operators.data ?? []).map(o => ({ value: o.userId, label: o.fullName }))}
        />

        <Field className='w-full max-w-[13rem]'>
          <FieldLabel htmlFor='session-from'>Başlangıç</FieldLabel>
          <Input
            id='session-from'
            type='datetime-local'
            value={from}
            onChange={e => {
              setFrom(e.target.value);
              setPage(1);
            }}
          />
        </Field>
        <Field className='w-full max-w-[13rem]'>
          <FieldLabel htmlFor='session-to'>Bitiş</FieldLabel>
          <Input
            id='session-to'
            type='datetime-local'
            value={to}
            onChange={e => {
              setTo(e.target.value);
              setPage(1);
            }}
          />
        </Field>

        {isFiltered && (
          <Button size='sm' variant='ghost' onClick={resetFilters}>
            <RotateCcwIcon />
            Filtreyi temizle
          </Button>
        )}
      </div>

      {sessions.isError && <p className='text-sm text-destructive'>{sessions.error.message}</p>}
      {sessions.isPending && <Skeleton className='h-64 w-full rounded-xl' />}

      {sessions.data && (
        <>
          <div className='overflow-x-auto rounded-xl border'>
            <table className='w-full min-w-[60rem] text-sm'>
              <thead className='bg-muted/50 text-muted-foreground'>
                <tr className='[&>th]:px-3 [&>th]:py-2 [&>th]:text-left [&>th]:font-medium'>
                  <th className='w-16'>#</th>
                  <th>Kabin / dış kapı</th>
                  <th>Operatör</th>
                  <th className='w-44'>Başlangıç</th>
                  <th className='w-44'>Bitiş</th>
                  <th className='w-20'>Süre</th>
                  <th className='w-40'>Durum</th>
                </tr>
              </thead>
              <tbody>
                {sessions.data.data.map(session => (
                  <SessionRow key={session.id} session={session} onOpen={() => navigate(`/signalization/sessions/${session.id}`)} />
                ))}
              </tbody>
            </table>

            {sessions.data.data.length === 0 && (
              <p className='py-8 text-center text-sm text-muted-foreground'>
                {isFiltered ? 'Bu filtreye uyan işlem yok.' : 'Henüz işlem kaydı yok.'}
              </p>
            )}
          </div>

          <div className='flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground'>
            <span>
              {sessions.data.dataCount} işlem
              {sessions.data.pageCount > 1 && ` · sayfa ${sessions.data.page}/${sessions.data.pageCount}`}
            </span>

            {sessions.data.pageCount > 1 && (
              <div className='flex items-center gap-2'>
                <Button size='sm' variant='outline' disabled={!sessions.data.hasPrevious} onClick={() => setPage(p => p - 1)}>
                  <ChevronLeftIcon />
                  Önceki
                </Button>
                <Button size='sm' variant='outline' disabled={!sessions.data.hasNext} onClick={() => setPage(p => p + 1)}>
                  Sonraki
                  <ChevronRightIcon />
                </Button>
              </div>
            )}
          </div>
        </>
      )}
    </section>
  );
}

function SessionRow({ session, onOpen }: { session: OperatorSessionListItemDto; onOpen: () => void }) {
  return (
    <tr className={cn('cursor-pointer border-t align-top hover:bg-muted/40 [&>td]:px-3 [&>td]:py-2', session.hasAlert && 'bg-destructive/5')} onClick={onOpen}>
      <td className='font-mono text-xs'>
        {/* Satır tıklanabilir; bağlantı klavye ve "yeni sekmede aç" için. */}
        <Link to={`/signalization/sessions/${session.id}`} className='underline-offset-4 hover:underline' onClick={e => e.stopPropagation()}>
          {session.id}
        </Link>
      </td>
      <td className='max-w-[16rem]'>
        <div className='truncate font-medium'>{session.outerDoorName}</div>
        <div className='truncate text-xs text-muted-foreground'>{session.cabinetName ?? '—'}</div>
      </td>
      <td className='max-w-[18rem]'>
        <SessionOperatorList operators={session.operators} />
      </td>
      <td className='font-mono text-xs whitespace-nowrap'>{formatUtcDateTime(session.startedAtUtc)}</td>
      <td className='font-mono text-xs whitespace-nowrap'>{formatUtcDateTime(session.endedAtUtc)}</td>
      <td className='font-mono text-xs tabular-nums'>{formatDuration(session.durationSec)}</td>
      <td>
        <div className='flex flex-col gap-1'>
          <SessionStatusBadge status={session.status} />
          <SessionFlagBadges flags={session.flags} />
        </div>
      </td>
    </tr>
  );
}

interface FilterOption {
  value: string;
  label: string;
}

function FilterSelect({
  id,
  label,
  value,
  onChange,
  allLabel,
  options
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string | null) => void;
  allLabel: string;
  options: FilterOption[];
}) {
  const selectedLabel = value === ALL ? allLabel : options.find(o => o.value === value)?.label;

  return (
    <Field className='w-full max-w-[12rem]'>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger id={id} className='w-full'>
          <SelectValue>{selectedLabel}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={ALL}>{allLabel}</SelectItem>
          {options.map(option => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}
