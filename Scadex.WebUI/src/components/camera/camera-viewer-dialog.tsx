import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { useCamera } from '@/hooks/use-cameras';
import { toApiError } from '@/lib/axios-helper';
import { CameraViewer } from './camera-viewer';

/**
 * `CameraViewer`'ı grid'e/`/cameras/:id`'ye gitmeden bir diyalog içinde açar.
 *
 * Sanal sinyalizasyon kabini için: operatör kameraya kabin ekranından tıkladığında ana
 * detay sayfasının kromu (breadcrumb, "Grid'e dön") anlamsız — oradan gelmedi. Kapanışta
 * `Dialog` içeriği söker (Base UI varsayılanı), bu da `useCameraStream`'in temizliğini
 * (WHEP oturumunu kapatma) tetikler — arkaplanda açık kalan bir yayın kalmaz.
 *
 * `showClipAndHistory` varsayılan olarak KAPALI: bu diyalog anlık bir bakış içindir, klip
 * çekimi/geçmişi tam detay sayfasına özgü kalır.
 */
export function CameraViewerDialog({
  cameraId,
  onOpenChange,
  showClipAndHistory = false
}: {
  cameraId: string | null;
  onOpenChange: (open: boolean) => void;
  showClipAndHistory?: boolean;
}) {
  const camera = useCamera(cameraId ?? undefined);

  return (
    <Dialog open={cameraId !== null} onOpenChange={onOpenChange}>
      <DialogContent className='max-w-[calc(100%-2rem)] sm:max-w-3xl lg:max-w-5xl'>
        <DialogHeader>
          <DialogTitle>{camera.data?.name ?? 'Kamera'}</DialogTitle>
          {camera.data && (
            <DialogDescription>
              {camera.data.ipAddress} · Ana akım (kanal {camera.data.mainStreamChannel})
            </DialogDescription>
          )}
        </DialogHeader>

        {camera.isPending && <Skeleton className='aspect-video w-full rounded-xl' />}
        {camera.isError && <p className='text-sm text-destructive'>{toApiError(camera.error).message}</p>}
        {camera.data && <CameraViewer camera={camera.data} showClipAndHistory={showClipAndHistory} />}
      </DialogContent>
    </Dialog>
  );
}
