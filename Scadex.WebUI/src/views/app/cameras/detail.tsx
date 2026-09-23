import { Link, useParams } from 'react-router';
import { ArrowLeftIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { CameraStatusBadge } from '@/components/camera/camera-status-badge';
import { CameraViewer } from '@/components/camera/camera-viewer';
import { useCamera } from '@/hooks/use-cameras';
import { toApiError } from '@/lib/axios-helper';

/**
 * Tek kamera — `/cameras/:cameraId`.
 *
 * Grid'den farkı **ana akım**: burada tek bir yayın var, dolayısıyla tam
 * çözünürlük hem mümkün hem gerekli (`Camera.MainStreamChannel`).
 *
 * Oynatıcı + çekim kontrolleri `CameraViewer`'da (bkz. `components/camera/camera-viewer.tsx`) —
 * burada yalnızca sayfaya özgü kroma (başlık, "Grid'e dön") kalır; sanal sinyalizasyon kabini
 * aynı bileşeni bir diyalog içinde kullanır.
 */
export default function CameraDetail() {
  const { cameraId } = useParams<{ cameraId: string }>();

  const camera = useCamera(cameraId);

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
        <Button variant='outline' nativeButton={false} render={<Link to='/cameras' />}>
          <ArrowLeftIcon />
          Kameralara dön
        </Button>
      </div>
    );
  }

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

        {/* Kameranın kendi kabin grid'ine döner, kabin seçimine değil. */}
        <Button variant='outline' size='sm' nativeButton={false} render={<Link to={`/cameras/cabinet/${camera.data.cabinetId}`} />}>
          <ArrowLeftIcon />
          Grid'e dön
        </Button>
      </div>

      <CameraViewer camera={camera.data} />
    </div>
  );
}
