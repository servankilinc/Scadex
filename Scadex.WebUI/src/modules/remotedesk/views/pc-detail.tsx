import { useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeftIcon, EyeIcon, LoaderIcon, MonitorIcon, MonitorOffIcon, VideoOffIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { cn, formatUtcDateTime } from '@/lib/utils';
import { usePc } from '../hooks/use-pcs';
import { usePcStream } from '../hooks/use-pc-stream';
import { ScreenStreamStateLabels, type PcDetailDto, type PcMonitorDto } from '../models/pc';

/**
 * PC ekranı — `/remote-desk/pcs/:deviceId`: monitör seçici + oynatıcı.
 *
 * Aynı PC'yi (aynı ya da farklı monitörlerini) istenen sayıda kullanıcı aynı anda izleyebilir; aynı monitörün izleyicileri PC'deki
 * tek yayını paylaşır. Monitör değiştirmek eski kiralamayı bırakıp yenisini açar (oynatıcı `key` ile yeniden kurulur).
 */
export default function RemoteDeskPcDetail() {
  const { deviceId } = useParams();
  const pc = usePc(deviceId);
  const [selected, setSelected] = useState<number | null>(null);

  if (pc.error) return <p className='p-4 text-sm text-destructive'>{pc.error.message}</p>;
  if (!pc.data) return <Skeleton className='m-4 h-96 rounded-xl' />;

  const monitors = pc.data.monitors;
  // Seçim yoksa birincil monitör; seçilen monitör çıkarıldıysa da birincile düş.
  const monitorIndex =
    selected !== null && monitors.some(m => m.index === selected) ? selected : (monitors.find(m => m.isPrimary) ?? monitors[0])?.index;

  return (
    <div className='flex flex-col gap-4 p-4'>
      <PcHeader pc={pc.data} />

      {!pc.data.isConnected ? (
        <div className='flex flex-col items-center gap-2 rounded-xl border border-dashed p-10 text-center text-sm text-muted-foreground'>
          <MonitorOffIcon className='size-8' />
          <p>Bu PC merkeze bağlı değil.</p>
          <p className='max-w-lg'>
            PC'de Scadex RemoteDesk istemcisinin çalıştığından ve diyagramdaki MAC adresinin ({pc.data.macAddress ?? 'tanımlı değil'})
            PC'nin ağ kartıyla aynı olduğundan emin olun. İstemci ekranı gönderdiği MAC'leri gösterir.
          </p>
        </div>
      ) : (
        <>
          <MonitorPicker monitors={monitors} selected={monitorIndex} onSelect={setSelected} />
          {monitorIndex !== undefined && <PcPlayer key={monitorIndex} deviceId={pc.data.deviceId} monitorIndex={monitorIndex} />}
        </>
      )}
    </div>
  );
}

function PcHeader({ pc }: { pc: PcDetailDto }) {
  return (
    <div className='flex flex-wrap items-start justify-between gap-3'>
      <div className='flex items-start gap-3'>
        <Button size='icon-sm' variant='ghost' render={<Link to='/remote-desk/pcs' title='PC listesine dön' />}>
          <ArrowLeftIcon />
        </Button>
        <div>
          <h1 className='flex items-center gap-2 text-lg font-semibold'>
            {pc.deviceName}
            {pc.isConnected ? <Badge variant='secondary'>Bağlı</Badge> : <Badge variant='outline'>Bağlı değil</Badge>}
          </h1>
          <p className='text-sm text-muted-foreground'>
            {pc.cabinetName}
            {pc.isConnected && (
              <>
                {' · '}
                {pc.machineName} ({pc.userName}) · istemci {pc.clientVersion} · bağlandı {formatUtcDateTime(pc.connectedUtc)}
              </>
            )}
          </p>
        </div>
      </div>
      {pc.viewerCount > 0 && (
        <Badge variant='outline'>
          <EyeIcon />
          Bu PC'yi şu an {pc.viewerCount} izleyici izliyor
        </Badge>
      )}
    </div>
  );
}

function MonitorPicker({ monitors, selected, onSelect }: { monitors: PcMonitorDto[]; selected: number | undefined; onSelect: (index: number) => void }) {
  if (monitors.length === 0) return <p className='text-sm text-muted-foreground'>PC monitör bildirmedi.</p>;

  return (
    <div className='flex flex-wrap gap-2'>
      {monitors.map(m => (
        <button
          key={m.index}
          type='button'
          onClick={() => onSelect(m.index)}
          className={cn(
            'flex min-w-44 flex-col items-start gap-0.5 rounded-lg border px-3 py-2 text-left text-sm transition-colors hover:bg-muted',
            m.index === selected && 'border-primary bg-primary/5 ring-1 ring-primary'
          )}>
          <span className='flex items-center gap-1.5 font-medium'>
            <MonitorIcon className='size-4' />
            Monitör {m.index}
            {m.isPrimary && <span className='text-xs font-normal text-muted-foreground'>(birincil)</span>}
          </span>
          <span className='text-xs text-muted-foreground'>
            {m.width}×{m.height}
            {m.streamState !== null && ` · ${ScreenStreamStateLabels[m.streamState]}`}
            {m.viewerCount > 0 && ` · ${m.viewerCount} izleyici`}
          </span>
        </button>
      ))}
    </div>
  );
}

function PcPlayer({ deviceId, monitorIndex }: { deviceId: string; monitorIndex: number }) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const stream = usePcStream(videoRef, deviceId, monitorIndex);

  return (
    <div className='flex flex-col gap-2'>
      <div className='relative aspect-video max-h-[calc(100vh-14rem)] overflow-hidden rounded-xl border bg-black'>
        {/* `contain`: ekranın tamamı görünmeli (ileride fare koordinatı da çizilen görüntü dikdörtgenine göre hesaplanacak, § 12.3). */}
        <video ref={videoRef} autoPlay playsInline muted className='size-full object-contain' />

        {stream.state !== 'connected' && (
          <div className='pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/60 px-6 text-center'>
            {stream.state === 'failed' ? (
              <>
                <VideoOffIcon className='size-6 text-white/70' />
                <p className='text-sm text-white/80'>{stream.error ?? 'Yayın açılamadı.'}</p>
              </>
            ) : (
              <>
                <LoaderIcon className='size-6 animate-spin text-white/70' />
                <p className='text-sm text-white/70'>
                  {stream.state === 'reconnecting'
                    ? (stream.error ?? 'Bağlantı koptu, yeniden deneniyor…')
                    : 'PC yayını başlatıyor… (ilk izlemede PC kodlayıcıları sınar, 20 sn kadar sürebilir)'}
                </p>
              </>
            )}
          </div>
        )}
      </div>

      {stream.state === 'failed' && (
        <Button variant='outline' size='sm' onClick={stream.retry} className='self-start'>
          Tekrar dene
        </Button>
      )}
    </div>
  );
}
