import { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';
import { CameraIcon, CropIcon, DownloadIcon, FilmIcon, LoaderIcon, VideoOffIcon, XIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { useCaptures, useCreateCapture } from '@/hooks/use-camera-captures';
import { useCameraStream } from '@/hooks/use-camera-stream';
import { grabVideoFrame } from '@/lib/camera/frame-grab';
import { CaptureType, StreamProfile } from '@/models/enums/entityEnums';
import type { CameraDto } from '@/models/camera';
import { CaptureHistory } from './capture-history';

const DEFAULT_CLIP_SECONDS = 15;

/**
 * Kamera ana akım oynatıcı + çekim kontrolleri.
 *
 * `/cameras/:cameraId` sayfasından ayrıştırıldı (2026-09-23): sanal sinyalizasyon kabini de
 * aynı görüntüleme deneyimini bir diyalog içinde kullanıyor, bu yüzden yayın/kayıt mantığı
 * sayfa kromundan (başlık, "Grid'e dön" butonu, breadcrumb) bağımsız olmalı.
 *
 * `showClipAndHistory=false`: sanal kabin diyaloğu gibi kısa bakış yerlerinde klip çekimi ve
 * çekim geçmişi listesi gösterilmez — kare yakalama/görüntü kaydetme kalır.
 */
export function CameraViewer({ camera, showClipAndHistory = true }: { camera: CameraDto; showClipAndHistory?: boolean }) {
  const videoRef = useRef<HTMLVideoElement | null>(null);

  const stream = useCameraStream(videoRef, camera, StreamProfile.Main);
  const captures = useCaptures(showClipAndHistory ? camera.id : undefined);
  const createCapture = useCreateCapture(camera.id);

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

  const isBusy = createCapture.isPending;

  return (
    <div className={showClipAndHistory ? 'grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(320px,1fr)]' : 'flex flex-col gap-3'}>
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
                  <p className='text-sm text-white/70'>{stream.state === 'reconnecting' ? 'Bağlantı koptu, yeniden deneniyor…' : 'Bağlanıyor…'}</p>
                </>
              )}
            </div>
          )}

          {/* Kısa bakış (showClipAndHistory=false): tek eylem olduğu için ayrı bir kart yerine
              görüntünün üstünde, yarı saydam bir buton — sağ panele yer açmaya değmez. */}
          {!showClipAndHistory && (
            <Button
              type='button'
              size='sm'
              onClick={handleGrabFrame}
              disabled={stream.state !== 'connected'}
              className='absolute top-2 right-2 bg-black/50 text-white backdrop-blur-sm hover:bg-black/70'
            >
              <CropIcon />
              Görüntü Yakala
            </Button>
          )}
        </div>

        {stream.state === 'failed' && (
          <Button variant='outline' size='sm' onClick={stream.retry} className='self-start'>
            Tekrar dene
          </Button>
        )}

        {frameUrl && !showClipAndHistory && (
          <div className='relative w-32 self-end overflow-hidden rounded-lg border shadow-sm'>
            <img src={frameUrl} alt='Yakalanan kare' className='block w-full' />
            <div className='absolute top-1 right-1 flex gap-1'>
              <Button
                size='icon-sm'
                render={<a href={frameUrl} download={`${camera.name}.jpg`} title='İndir' />}
                className='bg-black/50 text-white backdrop-blur-sm hover:bg-black/70'
              >
                <DownloadIcon />
              </Button>
              <Button
                size='icon-sm'
                onClick={() => setFrameUrl(null)}
                title='Kapat'
                className='bg-black/50 text-white backdrop-blur-sm hover:bg-black/70'
              >
                <XIcon />
              </Button>
            </div>
          </div>
        )}

        {frameUrl && showClipAndHistory && (
          <Card>
            <CardHeader>
              <CardTitle className='text-sm'>Yakalanan kare</CardTitle>
            </CardHeader>
            <CardContent className='flex flex-col gap-2'>
              <img src={frameUrl} alt='Yakalanan kare' className='w-full rounded-lg border' />
              <div className='flex gap-2'>
                <Button size='sm' variant='outline' render={<a href={frameUrl} download={`${camera.name}.jpg`} />}>
                  İndir
                </Button>
                <Button size='sm' variant='ghost' onClick={() => setFrameUrl(null)}>
                  Kapat
                </Button>
              </div>
              <FieldDescription>Bu kare tarayıcıda üretildi ve sunucuya kaydedilmedi. Delil kaydı için "Görüntüyü kaydet" kullanın.</FieldDescription>
            </CardContent>
          </Card>
        )}
      </div>

      {showClipAndHistory && (
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
              <Button onClick={() => createCapture.mutate({ type: CaptureType.Snapshot })} disabled={isBusy || !camera.isActive}>
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
                disabled={isBusy || !camera.isActive || !camera.mainStreamEnabled}
              >
                <FilmIcon />
                {isBusy ? 'Başlatılıyor…' : 'Klip çek'}
              </Button>

              {!camera.mainStreamEnabled && <p className='text-xs text-destructive'>Klip ana akımdan alınır; bu kamerada ana akım kapalı.</p>}
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
      )}
    </div>
  );
}
