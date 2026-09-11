import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeftIcon, ChevronLeftIcon, ChevronRightIcon, ImageOffIcon, LoaderIcon, ShieldAlertIcon, SirenIcon } from 'lucide-react';
import { captureFileUrl } from '@/api/camera-stream';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { cn, formatUtcDateTime, toUtcDate } from '@/lib/utils';
import { CaptureStatus } from '@/models/enums/entityEnums';
import { SessionFlagBadges, SessionStatusBadge } from '../../components/session-badges';
import { useSessionDetail } from '../../hooks/use-operator-sessions';
import { formatDuration } from '../../lib';
import { OperatorSessionStatus, SessionEventType, SessionEventTypeLabels, eventTone, formatEventDetail, type EventTone } from '../../models/enums';
import type { OperatorSessionCaptureDto, OperatorSessionDetailDto, OperatorSessionEventDto } from '../../models/session';

/**
 * İşlem detayı — `/signalization/sessions/:sessionId`.
 *
 * Kareler, iç kapı özeti ve olayların zaman çizelgesi. Oturum açıkken sayfa kendini 3 sn'de bir yoklar
 * (`useSessionDetail`), kapanınca durur. Salt okunur.
 */
export default function OperatorSessionDetail() {
  const { sessionId } = useParams();
  const id = Number(sessionId);
  const detail = useSessionDetail(id);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <Button size='sm' variant='ghost' nativeButton={false} render={<Link to='/signalization/sessions' />}>
          <ArrowLeftIcon />
          İşlemler
        </Button>
      </div>

      {!Number.isInteger(id) || id <= 0 ? (
        <p className='text-sm text-destructive'>Geçersiz işlem numarası.</p>
      ) : detail.isError ? (
        <p className='text-sm text-destructive'>{detail.error.message}</p>
      ) : detail.isPending ? (
        <div className='flex flex-col gap-3'>
          <Skeleton className='h-24 w-full rounded-xl' />
          <Skeleton className='h-48 w-full rounded-xl' />
          <Skeleton className='h-72 w-full rounded-xl' />
        </div>
      ) : (
        <SessionDetailContent session={detail.data} />
      )}
    </div>
  );
}

function SessionDetailContent({ session }: { session: OperatorSessionDetailDto }) {
  const isOpen = session.status === OperatorSessionStatus.Open;

  return (
    <>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div className='min-w-0'>
          <h1 className='flex flex-wrap items-center gap-2 text-lg font-semibold'>
            <span>
              İşlem #{session.id} — {session.outerDoorName}
            </span>
            <SessionStatusBadge status={session.status} />
          </h1>
          <p className='text-sm text-muted-foreground'>{session.cabinetName ?? 'Silinmiş kabin'}</p>
        </div>
        <SessionFlagBadges flags={session.flags} />
      </div>

      {session.hasAlert && (
        <div className='flex items-start gap-2 rounded-xl border border-destructive/40 bg-destructive/5 p-3 text-sm'>
          <ShieldAlertIcon className='mt-0.5 size-4 shrink-0 text-destructive' />
          <p>
            Bu işlemde <strong>güvenlik uyarısı</strong> var. Uyarılar onay beklemez; kayıtta kalıcı olarak durur ve raporda
            bayrak filtresiyle bulunur.
          </p>
        </div>
      )}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-4'>
        <Metric label='Başlangıç' value={formatUtcDateTime(session.startedAtUtc)} />
        <Metric label='Bitiş' value={isOpen ? 'sürüyor' : formatUtcDateTime(session.endedAtUtc)} />
        <Metric label='Süre' value={isOpen ? '—' : formatDuration(session.durationSec)} mono />
        <Metric
          label='Siren'
          value={
            session.sirenRequestedAtUtc
              ? session.sirenReleasedAtUtc
                ? `${formatDuration(secondsBetween(session.sirenRequestedAtUtc, session.sirenReleasedAtUtc))} çaldı`
                : 'talep açık'
              : 'talep edilmedi'
          }
          icon={session.sirenRequestedAtUtc ? <SirenIcon className='size-4 text-muted-foreground' /> : undefined}
        />
      </div>

      <div className='grid gap-4 lg:grid-cols-2'>
        <OperatorsCard session={session} />
        <DoorsCard session={session} />
      </div>

      <CaptureGallery captures={session.captures} />
      <Timeline session={session} />
    </>
  );
}

function Metric({ label, value, mono, icon }: { label: string; value: string; mono?: boolean; icon?: React.ReactNode }) {
  return (
    <div className='rounded-xl border p-3'>
      <div className='flex items-center justify-between text-xs text-muted-foreground'>
        {label}
        {icon}
      </div>
      <div className={cn('mt-1 text-sm font-medium', mono && 'font-mono tabular-nums')}>{value}</div>
    </div>
  );
}

function OperatorsCard({ session }: { session: OperatorSessionDetailDto }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Operatörler</CardTitle>
        <CardDescription>Ad ve kurum, işlem anındaki hâliyle saklanır.</CardDescription>
      </CardHeader>
      <CardContent>
        {session.operators.length === 0 ? (
          <p className='text-sm text-muted-foreground'>Bu işlemde yetkili kart okutulmadı.</p>
        ) : (
          <ul className='flex flex-col gap-2'>
            {session.operators.map(op => (
              <li key={op.userId} className='flex flex-wrap items-baseline justify-between gap-2 rounded-lg border p-2.5 text-sm'>
                <div className='min-w-0'>
                  <div className='truncate font-medium'>{op.fullName}</div>
                  <div className='text-xs text-muted-foreground'>
                    {op.authorityName} · kart <span className='font-mono'>{op.cardIdRaw}</span>
                  </div>
                </div>
                <div className='text-right font-mono text-xs text-muted-foreground'>
                  <div>ilk: {formatTime(op.firstCardAtUtc)}</div>
                  <div>son: {formatTime(op.lastCardAtUtc)}</div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function DoorsCard({ session }: { session: OperatorSessionDetailDto }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>İç kapılar</CardTitle>
        <CardDescription>Olaylardan türetilir: ilk açılış, son kapanış ve son kilitlenme.</CardDescription>
      </CardHeader>
      <CardContent>
        {session.doors.length === 0 ? (
          <p className='text-sm text-muted-foreground'>Bu işlemde hiçbir iç kapıya dokunulmadı.</p>
        ) : (
          <div className='overflow-x-auto'>
            <table className='w-full min-w-[30rem] text-sm'>
              <thead className='text-muted-foreground'>
                <tr className='[&>th]:px-2 [&>th]:py-1.5 [&>th]:text-left [&>th]:font-medium'>
                  <th>Kapı</th>
                  <th>Kilit açıldı</th>
                  <th>Açıldı</th>
                  <th>Kapandı</th>
                  <th>Kilitlendi</th>
                  <th className='text-right'>Açılış</th>
                </tr>
              </thead>
              <tbody>
                {session.doors.map(door => (
                  <tr key={door.innerDoorId} className='border-t [&>td]:px-2 [&>td]:py-1.5'>
                    <td>
                      <div className='font-medium'>{door.name}</div>
                      <div className='text-xs text-muted-foreground'>{door.authorityName ?? '—'}</div>
                      {door.wasForcedOpen && (
                        <Badge variant='destructive' className='mt-1'>
                          <ShieldAlertIcon />
                          Zorla açma
                        </Badge>
                      )}
                    </td>
                    <td className='font-mono text-xs'>{formatTime(door.firstUnlockedAtUtc)}</td>
                    <td className='font-mono text-xs'>{formatTime(door.firstOpenedAtUtc)}</td>
                    <td className='font-mono text-xs'>{formatTime(door.lastClosedAtUtc)}</td>
                    <td className='font-mono text-xs'>{formatTime(door.lastLockedAtUtc)}</td>
                    <td className='text-right font-mono text-xs'>{door.openCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── kareler

function CaptureGallery({ captures }: { captures: OperatorSessionCaptureDto[] }) {
  const [viewerIndex, setViewerIndex] = useState<number | null>(null);
  // Satırı `Available` görünen ama dosyası diskte olmayan kareler (elle silinmiş, disk taşınmış…). Görsel
  // yüklenemeyince burada işaretlenir; kırık resim yerine açıklama gösterilir ve büyütme listesinden çıkar.
  const [brokenIds, setBrokenIds] = useState<ReadonlySet<number>>(() => new Set());
  const markBroken = (id: number) => setBrokenIds(prev => (prev.has(id) ? prev : new Set(prev).add(id)));

  // Büyütülebilir olanlar: yalnızca dosyası duran kareler.
  const viewable = captures.filter(capture => isViewable(capture) && !brokenIds.has(capture.cameraCaptureId));
  const current = viewerIndex != null ? viewable[viewerIndex] : undefined;

  const step = (delta: number) => setViewerIndex(index => (index == null || viewable.length === 0 ? index : (index + delta + viewable.length) % viewable.length));

  return (
    <Card>
      <CardHeader>
        <CardTitle>Giriş kareleri</CardTitle>
        <CardDescription>
          Dış kapı açıldığında kameradan çekilen kareler. Dosyalar kamera ayarındaki saklama süresine tabidir; süresi dolan karenin
          satırı kalır, görüntüsü gider.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {captures.length === 0 ? (
          <p className='text-sm text-muted-foreground'>Bu işlem için kare yok (dış kapıya kamera bağlı değil ya da kare sayısı 0).</p>
        ) : (
          <div className='grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5'>
            {captures.map(capture => (
              <CaptureTile
                key={capture.cameraCaptureId}
                capture={capture}
                isBroken={brokenIds.has(capture.cameraCaptureId)}
                onBroken={() => markBroken(capture.cameraCaptureId)}
                onOpen={viewable.includes(capture) ? () => setViewerIndex(viewable.indexOf(capture)) : undefined}
              />
            ))}
          </div>
        )}
      </CardContent>

      {current && (
        <Dialog open onOpenChange={open => !open && setViewerIndex(null)}>
          <DialogContent
            className='sm:max-w-4xl'
            onKeyDown={e => {
              if (e.key === 'ArrowLeft') step(-1);
              if (e.key === 'ArrowRight') step(1);
            }}>
            <DialogHeader>
              <DialogTitle>Kare {current.sequence}</DialogTitle>
              <DialogDescription>{formatUtcDateTime(current.capturedAtUtc)}</DialogDescription>
            </DialogHeader>

            <img src={captureFileUrl(current.relativePath!)} alt={`Kare ${current.sequence}`} className='max-h-[70vh] w-full rounded-lg bg-muted object-contain' />

            {viewable.length > 1 && (
              <div className='flex items-center justify-between'>
                <Button size='sm' variant='outline' onClick={() => step(-1)}>
                  <ChevronLeftIcon />
                  Önceki
                </Button>
                <span className='text-xs text-muted-foreground'>
                  {(viewerIndex ?? 0) + 1} / {viewable.length}
                </span>
                <Button size='sm' variant='outline' onClick={() => step(1)}>
                  Sonraki
                  <ChevronRightIcon />
                </Button>
              </div>
            )}
          </DialogContent>
        </Dialog>
      )}
    </Card>
  );
}

function isViewable(capture: OperatorSessionCaptureDto): boolean {
  return capture.status === CaptureStatus.Available && Boolean(capture.relativePath);
}

function CaptureTile({
  capture,
  isBroken,
  onBroken,
  onOpen
}: {
  capture: OperatorSessionCaptureDto;
  isBroken: boolean;
  onBroken: () => void;
  onOpen?: () => void;
}) {
  const caption = (
    <div className='flex items-center justify-between gap-2 px-2 py-1.5 text-xs'>
      <span className='font-medium'>#{capture.sequence}</span>
      <span className='font-mono text-muted-foreground'>{formatTime(capture.capturedAtUtc)}</span>
    </div>
  );

  if (onOpen && capture.relativePath) {
    return (
      <button type='button' onClick={onOpen} className='overflow-hidden rounded-lg border text-left transition hover:ring-2 hover:ring-ring/50'>
        <img
          src={captureFileUrl(capture.relativePath)}
          alt={`Kare ${capture.sequence}`}
          loading='lazy'
          onError={onBroken}
          className='aspect-video w-full bg-muted object-cover'
        />
        {caption}
      </button>
    );
  }

  const placeholder = isBroken
    ? { icon: <ImageOffIcon className='size-5' />, text: 'Dosya sunucuda bulunamadı' }
    : capture.status === CaptureStatus.Pending
      ? { icon: <LoaderIcon className='size-5 animate-spin' />, text: 'Çekiliyor…' }
      : capture.status === CaptureStatus.Failed
        ? { icon: <ImageOffIcon className='size-5' />, text: capture.failureReason ?? 'Çekim başarısız' }
        : capture.status === CaptureStatus.Available
          ? { icon: <ImageOffIcon className='size-5' />, text: 'Saklama süresi doldu; dosya silindi' }
          : { icon: <ImageOffIcon className='size-5' />, text: 'Çekim kaydı bulunamadı' };

  return (
    <div className='overflow-hidden rounded-lg border'>
      <div className='flex aspect-video w-full flex-col items-center justify-center gap-1.5 bg-muted p-2 text-center text-xs text-muted-foreground'>
        {placeholder.icon}
        <span className='line-clamp-3'>{placeholder.text}</span>
      </div>
      {caption}
    </div>
  );
}

// ─────────────────────────────────────────────────────────── zaman çizelgesi

const TONE_DOT: Record<EventTone, string> = {
  default: 'bg-primary',
  success: 'bg-emerald-500',
  warning: 'bg-amber-500',
  danger: 'bg-destructive',
  muted: 'bg-muted-foreground/40'
};

function Timeline({ session }: { session: OperatorSessionDetailDto }) {
  // Başarılı kare olayları galeride zaten görünüyor; varsayılan olarak gizlenir, çizelge kapı akışına
  // odaklanır. Başarısız kare HER ZAMAN görünür — "o anda görüntü yok" bilgisi delildir.
  const [showSnapshots, setShowSnapshots] = useState(false);
  const events = showSnapshots ? session.events : session.events.filter(e => e.type !== SessionEventType.SnapshotTaken);
  const hiddenCount = session.events.length - events.length;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Zaman çizelgesi</CardTitle>
        <CardDescription>Olaylar sahadaki gerçekleşme anına göre sıralıdır; süre, dış kapının açılışından itibarendir.</CardDescription>
      </CardHeader>
      <CardContent className='flex flex-col gap-3'>
        {(hiddenCount > 0 || showSnapshots) && (
          <div>
            <Button size='xs' variant='ghost' onClick={() => setShowSnapshots(v => !v)}>
              {showSnapshots ? 'Kare olaylarını gizle' : `Kare olaylarını göster (${hiddenCount})`}
            </Button>
          </div>
        )}

        {events.length === 0 ? (
          <p className='text-sm text-muted-foreground'>Olay yok.</p>
        ) : (
          <ol className='relative flex flex-col gap-3 border-l pl-5'>
            {events.map(event => (
              <TimelineItem key={event.id} event={event} startedAtUtc={session.startedAtUtc} />
            ))}
          </ol>
        )}
      </CardContent>
    </Card>
  );
}

function TimelineItem({ event, startedAtUtc }: { event: OperatorSessionEventDto; startedAtUtc: string }) {
  const label = SessionEventTypeLabels[event.type] ?? `#${event.type}`;
  const detail = formatEventDetail(event.type, event.detail);
  const offset = secondsBetween(startedAtUtc, event.occurredAtUtc);
  // SCADA damgası ile bize ulaşma arasında belirgin fark varsa gösterilir (gecikmeli iletim ya da saat kayması).
  const receivedLag = secondsBetween(event.occurredAtUtc, event.receivedAtUtc);

  return (
    <li className='relative text-sm'>
      <span className={cn('absolute top-1.5 -left-[1.66rem] size-2.5 rounded-full ring-4 ring-background', TONE_DOT[eventTone(event.type)])} />
      <div className='flex flex-wrap items-baseline gap-x-2 gap-y-0.5'>
        <span className='font-mono text-xs text-muted-foreground tabular-nums'>{formatTime(event.occurredAtUtc)}</span>
        <span className='font-mono text-xs text-muted-foreground tabular-nums'>+{formatDuration(Math.max(0, offset))}</span>
        <span className='font-medium'>{label}</span>
        {event.innerDoorName && <Badge variant='outline'>{event.innerDoorName}</Badge>}
      </div>
      {(event.userFullName || event.cardIdRaw || detail || Math.abs(receivedLag) >= 3) && (
        <div className='mt-0.5 flex flex-wrap gap-x-3 text-xs text-muted-foreground'>
          {event.userFullName && <span>{event.userFullName}</span>}
          {event.cardIdRaw && <span className='font-mono'>kart {event.cardIdRaw}</span>}
          {detail && <span>{detail}</span>}
          {Math.abs(receivedLag) >= 3 && <span>alındı: {formatTime(event.receivedAtUtc)}</span>}
        </div>
      )}
    </li>
  );
}

// ─────────────────────────────────────────────────────────── yardımcılar

/** Yalnızca saat — tarih üstteki Başlangıç/Bitiş kutularında zaten var. */
function formatTime(value: string | null): string {
  const date = toUtcDate(value);
  return date ? date.toLocaleTimeString('tr-TR') : '—';
}

function secondsBetween(fromUtc: string, toUtc: string): number {
  const from = toUtcDate(fromUtc);
  const to = toUtcDate(toUtc);
  return from && to ? Math.round((to.getTime() - from.getTime()) / 1000) : 0;
}
