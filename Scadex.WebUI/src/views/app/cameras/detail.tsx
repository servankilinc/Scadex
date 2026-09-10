import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeftIcon, CameraIcon, CropIcon, FilmIcon, LoaderIcon, VideoOffIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { CameraStatusBadge } from '@/components/camera/camera-status-badge';
import { CaptureHistory } from '@/components/camera/capture-history';
import { useCamera } from '@/hooks/use-cameras';
import { useCaptures, useCreateCapture } from '@/hooks/use-camera-captures';
import { useCameraStream } from '@/hooks/use-camera-stream';
import { grabVideoFrame } from '@/lib/camera/frame-grab';
import { toApiError } from '@/lib/axios-helper';
import { CaptureType, StreamProfile } from '@/models/enums/entityEnums';
import { toast } from 'sonner';

const DEFAULT_CLIP_SECONDS = 15;

/**
 * Tek kamera — `/cameras/:cameraId`.
 *
 * Grid'den farkı **ana akım**: burada tek bir yayın var, dolayısıyla tam
 * çözünürlük hem mümkün hem gerekli (`Camera.MainStreamChannel`).
 */
export default function CameraDetail() {
  const { cameraId } = useParams<{ cameraId: string }>();

  const videoRef = useRef<HTMLVideoElement | null>(null);

  const camera = useCamera(cameraId);
  const stream = useCameraStream(videoRef, camera.data, StreamProfile.Main);
  const captures = useCaptures(cameraId);
  const createCapture = useCreateCapture(cameraId ?? '');

  const [clipSeconds, setClipSeconds] = useState(DEFAULT_CLIP_SECONDS);
  const [frameUrl, setFrameUrl] = useState<string | null>(null);

  // `URL.createObjectURL` blob'ları kendiliğinden serbest bırakılmaz. Temizliği
  // efektin kendisine bırakmak, elle yapmaktan daha güvenli: cleanup hem
  // `frameUrl` değiştiğinde (eskisini bırakır) hem de bileşen söküldüğünde
  // (sonuncusunu bırakır) çalışır — iki durumu ayrı ayrı hatırlamak gerekmez.
  useEffect(() => {
    if (!frameUrl) return;
    return () => URL.revokeObjectURL(frameUrl);
  }, [frameUrl]);

  const handleGrabFrame = async () => {
    const video = videoRef.current;
    if (!video) return;

    try {
      const frame = await grabVideoFrame(video);
      setFrameUrl(frame.objectUrl);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Kare yakalanamadı.');
    }
  };

  if (camera.isPending) {
    return (
      <div className='flex flex-col gap-4 p-4'>
        <Skeleton className='h-8 w-64' />
        <Skeleton className='aspect-video w-full rounded-xl' />
      </div>
    );
  }

  if (camera.isError || !camera.data) {
    return (
      <div className='flex flex-col gap-4 p-4'>
        <p className='text-sm text-destructive'>{toApiError(camera.error).message}</p>
        <Button variant='outline' render={<Link to='/cameras' />}>
          <ArrowLeftIcon />
          Canlı izlemeye dön
        </Button>
      </div>
    );
  }

  const isBusy = createCapture.isPending;

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div className='flex flex-col gap-1'>
          <h1 className='text-lg font-semibold'>{camera.data.name}</h1>
          <div className='flex flex-wrap items-center gap-2'>
            <span className='text-sm text-muted-foreground'>
              {camera.data.ipAddress} · Ana akım (kanal {camera.data.mainStreamChannel})
            </span>
            <CameraStatusBadge camera={camera.data} />
            {!camera.data.isActive && <Badge variant='secondary'>Pasif</Badge>}
          </div>
        </div>

        <Button variant='outline' size='sm' render={<Link to='/cameras' />}>
          <ArrowLeftIcon />
          Grid'e dön
        </Button>
      </div>

      <div className='grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(320px,1fr)]'>
        <div className='flex flex-col gap-3'>
          <div className='relative aspect-video overflow-hidden rounded-xl border bg-black'>
            <video
              ref={videoRef}
              autoPlay
              playsInline
              controls
              muted
              // `contain`: detayda görüntünün tamamı görünmeli. Grid'de `cover`
              // kullanılıyor çünkü orada kutucuğun doldurulması önemliydi.
              className='size-full object-contain'
            />

            {stream.state !== 'connected' && (
              <div className='pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/60 text-center'>
                {stream.state === 'failed' ? (
                  <>
                    <VideoOffIcon className='size-6 text-white/70' />
                    <p className='text-sm text-white/80'>{stream.error ?? 'Yayın açılamadı.'}</p>
                  </>
                ) : (
                  <>
                    <LoaderIcon className='size-6 animate-spin text-white/70' />
                    <p className='text-sm text-white/70'>
                      {stream.state === 'reconnecting' ? 'Bağlantı koptu, yeniden deneniyor…' : 'Bağlanıyor…'}
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

          {frameUrl && (
            <Card>
              <CardHeader>
                <CardTitle className='text-sm'>Yakalanan kare</CardTitle>
              </CardHeader>
              <CardContent className='flex flex-col gap-2'>
                <img src={frameUrl} alt='Yakalanan kare' className='w-full rounded-lg border' />
                <div className='flex gap-2'>
                  <Button size='sm' variant='outline' render={<a href={frameUrl} download={`${camera.data.name}.jpg`} />}>
                    İndir
                  </Button>
                  <Button size='sm' variant='ghost' onClick={() => setFrameUrl(null)}>
                    Kapat
                  </Button>
                </div>
                <FieldDescription>
                  Bu kare tarayıcıda üretildi ve sunucuya kaydedilmedi. Delil kaydı için "Görüntüyü kaydet" kullanın.
                </FieldDescription>
              </CardContent>
            </Card>
          )}
        </div>

        <div className='flex flex-col gap-4'>
          <Card>
            <CardHeader>
              <CardTitle className='text-sm'>Çekim</CardTitle>
            </CardHeader>
            <CardContent className='flex flex-col gap-3'>
              {/* Sunucuya HİÇ gitmez: operatör görüntüyü zaten izliyorken aynı
                  kareyi kameradan tekrar istemek gereksiz. */}
              <Button variant='outline' onClick={handleGrabFrame} disabled={stream.state !== 'connected'}>
                <CropIcon />
                Kareyi yakala
              </Button>

              {/* Delil yolu: sunucu kameradan taze görüntü çeker, diske yazar
                  ve CameraCapture satırı bırakır. */}
              <Button
                onClick={() => createCapture.mutate({ type: CaptureType.Snapshot })}
                disabled={isBusy || !camera.data.isActive}
              >
                <CameraIcon />
                Görüntüyü kaydet
              </Button>

              <Field>
                <FieldLabel htmlFor='clip-seconds'>Klip süresi (sn)</FieldLabel>
                <Input
                  id='clip-seconds'
                  type='number'
                  min={1}
                  max={120}
                  value={clipSeconds}
                  onChange={event => setClipSeconds(Number(event.target.value))}
                />
                <FieldDescription>
                  Kayıt <b>şimdi</b> başlar ve bu kadar sürer; olay öncesini kapsamaz.
                </FieldDescription>
              </Field>

              <Button
                variant='secondary'
                onClick={() => createCapture.mutate({ type: CaptureType.Clip, durationSec: clipSeconds })}
                disabled={isBusy || !camera.data.isActive || !camera.data.mainStreamEnabled}
              >
                <FilmIcon />
                {isBusy ? 'Başlatılıyor…' : 'Klip çek'}
              </Button>

              {!camera.data.mainStreamEnabled && (
                <p className='text-xs text-destructive'>Klip ana akımdan alınır; bu kamerada ana akım kapalı.</p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className='text-sm'>Çekim geçmişi</CardTitle>
            </CardHeader>
            <CardContent>
              <CaptureHistory captures={captures.data} isPending={captures.isPending} />
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
