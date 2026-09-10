import { DownloadIcon, FilmIcon, ImageIcon, LoaderIcon } from 'lucide-react';
import { captureFileUrl } from '@/api/camera-stream';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import type { CameraCaptureDto } from '@/models/camera/queries/cameraCaptureDto';
import { CaptureStatus, CaptureStatusLabels, CaptureType, CaptureTypeLabels } from '@/models/enums/entityEnums';

/**
 * Kameranın çekim geçmişi.
 *
 * `Pending` bir satır varken liste kendini yokluyor (bkz. `useCaptures`), bu
 * yüzden burada ayrıca bir zamanlayıcı YOK — iki yerde yoklama olsaydı hangisinin
 * listeyi tazelediği belirsizleşirdi.
 */
export function CaptureHistory({ captures, isPending }: { captures?: CameraCaptureDto[]; isPending: boolean }) {
  if (isPending) {
    return (
      <div className='flex flex-col gap-2'>
        {Array.from({ length: 3 }, (_, i) => (
          <Skeleton key={i} className='h-16 w-full rounded-lg' />
        ))}
      </div>
    );
  }

  if (!captures?.length) {
    return <p className='py-6 text-center text-sm text-muted-foreground'>Henüz çekim yok.</p>;
  }

  return (
    <ul className='flex flex-col gap-2'>
      {captures.map(capture => (
        <CaptureRow key={capture.id} capture={capture} />
      ))}
    </ul>
  );
}

function CaptureRow({ capture }: { capture: CameraCaptureDto }) {
  const isClip = capture.type === CaptureType.Clip;

  return (
    <li className='flex items-start gap-3 rounded-lg border p-3'>
      <div className='mt-0.5 text-muted-foreground'>
        {capture.status === CaptureStatus.Pending ? (
          <LoaderIcon className='size-4 animate-spin' />
        ) : isClip ? (
          <FilmIcon className='size-4' />
        ) : (
          <ImageIcon className='size-4' />
        )}
      </div>

      <div className='flex min-w-0 flex-1 flex-col gap-1'>
        <div className='flex flex-wrap items-center gap-2'>
          <span className='text-sm font-medium'>{CaptureTypeLabels[capture.type]}</span>
          <CaptureStatusBadge status={capture.status} />
          {isClip && capture.durationSec != null && <Badge variant='outline'>{capture.durationSec} sn</Badge>}
        </div>

        <p className='text-xs text-muted-foreground'>
          {new Date(capture.capturedAtUtc).toLocaleString('tr-TR')}
          {capture.sizeBytes != null && ` · ${formatBytes(capture.sizeBytes)}`}
        </p>

        {/* Başarısız çekim de bir satır bırakır — "o anda görüntü YOK"
            bilgisinin kendisi delildir. Sebebi göstermek şart, yoksa satır
            açıklanamaz bir boşluk olarak durur. */}
        {capture.failureReason && <p className='text-xs text-destructive'>{capture.failureReason}</p>}

        {capture.expiresAt && (
          <p className='text-xs text-muted-foreground'>
            Saklama sonu: {new Date(capture.expiresAt).toLocaleDateString('tr-TR')}
          </p>
        )}
      </div>

      {capture.status === CaptureStatus.Available && capture.relativePath && (
        <Button
          size='sm'
          variant='outline'
          render={<a href={captureFileUrl(capture.relativePath)} target='_blank' rel='noreferrer' />}
        >
          <DownloadIcon />
          Aç
        </Button>
      )}
    </li>
  );
}

function CaptureStatusBadge({ status }: { status: CaptureStatus }) {
  const variant =
    status === CaptureStatus.Available ? 'default' : status === CaptureStatus.Failed ? 'destructive' : 'secondary';

  return <Badge variant={variant}>{CaptureStatusLabels[status]}</Badge>;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
