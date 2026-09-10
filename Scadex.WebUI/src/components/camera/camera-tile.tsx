import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router';
import { AlertTriangleIcon, LoaderIcon, VideoOffIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useCameraStream } from '@/hooks/use-camera-stream';
import type { CameraDto } from '@/models/camera';
import { StreamProfile } from '@/models/enums/entityEnums';
import { cn } from '@/lib/utils';

/**
 * Grid'deki tek kamera kutucuğu — **tali akım**.
 *
 * Grid her zaman tali akımı kullanır: 12 kutucuklu bir ekranda ana akım kamera
 * başına ~4 Mbps demek ve kabinlerin ağırlıklı olarak GSM ile bağlandığı bir
 * kurulumda bu hattı doldurur. Ana akım yalnızca tek kamera açıldığında
 * (detay ekranı) kullanılır.
 */
export function CameraTile({ camera }: { camera: CameraDto }) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const [visible, setVisible] = useState(false);

  // GÖRÜNMEYEN KUTUCUK BAĞLANMAZ. Sayfa açılışında hepsini birden başlatmak,
  // ekranda olmayan kameralar için de RTSP oturumu açmak demekti; 32 kameralı
  // bir kabinde bu hem tarayıcıyı hem geçidi dize getirir.
  useEffect(() => {
    const element = containerRef.current;
    if (!element) return;

    const observer = new IntersectionObserver(
      entries => setVisible(entries[0]?.isIntersecting ?? false),
      // Kutucuk görünmeden hemen önce başlasın ki kullanıcı kaydırdığında
      // siyah bir kare görmesin.
      { rootMargin: '200px' }
    );

    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const stream = useCameraStream(videoRef, camera, StreamProfile.Sub, { enabled: visible });

  return (
    <div ref={containerRef} className='group relative aspect-video overflow-hidden rounded-xl border bg-black'>
      <video
        ref={videoRef}
        autoPlay
        playsInline
        // Grid'de ses ZORUNLU olarak kapalı: tarayıcılar kullanıcı etkileşimi
        // olmadan sesli oynatmayı engeller ve `muted` olmasaydı hiçbir kutucuk
        // kendiliğinden başlamazdı.
        muted
        className='size-full object-cover'
      />

      {stream.state !== 'connected' && (
        <div className='absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/60 p-3 text-center'>
          <StreamPlaceholder state={stream.state} error={stream.error} onRetry={stream.retry} />
        </div>
      )}

      <div className='pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between gap-2 bg-gradient-to-b from-black/80 to-transparent p-2'>
        <span className='truncate text-sm font-medium text-white drop-shadow'>{camera.name}</span>
        <StreamStateDot state={stream.state} />
      </div>

      <div className='absolute inset-x-0 bottom-0 flex items-center justify-between gap-2 bg-gradient-to-t from-black/80 to-transparent p-2 opacity-0 transition-opacity group-hover:opacity-100'>
        <span className='truncate text-xs text-white/70'>{camera.ipAddress}</span>
        <Button size='sm' variant='secondary' render={<Link to={`/cameras/${camera.id}`} />}>
          Detay
        </Button>
      </div>
    </div>
  );
}

function StreamPlaceholder({ state, error, onRetry }: { state: string; error?: string; onRetry: () => void }) {
  if (state === 'failed') {
    return (
      <>
        <VideoOffIcon className='size-6 text-white/70' />
        <p className='text-xs text-white/80'>{error ?? 'Yayın açılamadı.'}</p>
        <Button size='sm' variant='secondary' onClick={onRetry}>
          Tekrar dene
        </Button>
      </>
    );
  }

  if (state === 'reconnecting') {
    return (
      <>
        <AlertTriangleIcon className='size-5 text-amber-400' />
        <p className='text-xs text-white/80'>Bağlantı koptu, yeniden deneniyor…</p>
      </>
    );
  }

  return (
    <>
      <LoaderIcon className='size-5 animate-spin text-white/70' />
      <p className='text-xs text-white/70'>{state === 'queued' ? 'Sırada…' : 'Bağlanıyor…'}</p>
    </>
  );
}

function StreamStateDot({ state }: { state: string }) {
  const tone =
    state === 'connected'
      ? 'bg-emerald-500'
      : state === 'failed'
        ? 'bg-destructive'
        : state === 'reconnecting'
          ? 'bg-amber-500'
          : 'bg-white/40';

  return <span className={cn('mt-1 size-2 shrink-0 rounded-full', tone)} />;
}
