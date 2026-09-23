import { useMemo, useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeftIcon, PencilIcon, PlusIcon, VideoOffIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Field, FieldLabel } from '@/components/ui/field';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { CameraFormDialog } from '@/components/camera/camera-form-dialog';
import { CameraStatusBadge } from '@/components/camera/camera-status-badge';
import { CameraTile } from '@/components/camera/camera-tile';
import { useCabinets } from '@/hooks/use-cabinets';
import { useCameras } from '@/hooks/use-cameras';
import { cn } from '@/lib/utils';
import type { CameraDto } from '@/models/camera';

/**
 * Canlı izleme, adım 2 — `/cameras/cabinet/:cabinetId`: seçilen kabinin kameraları.
 *
 * Hem izleme hem yönetim. Aktif kameralar tali akımla grid'de oynar (`CameraTile`); her kutucuğun
 * altında tanım bilgisi ve "düzenle" durur, başlıkta "Yeni kamera". Tanım/CRUD eskiden ayrı bir
 * ekrandaydı (`/admin/cameras`); iki ekran aynı kabin → kamera listesini gösterdiği için burada
 * birleşti (2026-09-23).
 *
 * Kameralar diyagramda YER ALMAZ (pini/kablosu yok, verisi SCADA'dan gelmez); ortak izleme alanlarının
 * sözleşmesi sunucuda `IMonitoredAsset`.
 */
export default function CabinetCameras() {
  const { cabinetId } = useParams<{ cabinetId: string }>();
  const cabinets = useCabinets();
  const [includePassive, setIncludePassive] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<CameraDto | null>(null);

  // Seçim sayfası yalnızca aktif kabinleri listeler; doğrudan URL ile pasif kabine gelinirse de aynı kural.
  const cabinet = cabinets.data?.find(c => c.id === cabinetId && c.isActive);

  // Liste pasiflerle BİRLİKTE çekilir, anahtar istemcide süzülür: sorgu anahtarı değişseydi anahtara
  // her dokunuşta liste boşalıp dolar, oynayan kutucuklar unmount olup yayınlar yeniden bağlanırdı.
  const cameras = useCameras(cabinet?.id, true);
  const visibleCameras = useMemo(
    () => (includePassive ? cameras.data : cameras.data?.filter(camera => camera.isActive)) ?? [],
    [cameras.data, includePassive]
  );

  if (cabinets.isPending) {
    return (
      <div className='flex flex-col gap-4 p-4'>
        <Skeleton className='h-8 w-64' />
        <Skeleton className='aspect-video w-full max-w-md rounded-xl' />
      </div>
    );
  }

  if (!cabinet) {
    return (
      <div className='flex flex-col items-start gap-4 p-4'>
        <p className='text-sm text-destructive'>{cabinets.isError ? 'Kabinler yüklenemedi.' : 'Kabin bulunamadı ya da pasif.'}</p>
        <Button variant='outline' size='sm' nativeButton={false} render={<Link to='/cameras' />}>
          <ArrowLeftIcon />
          Kabin seçimine dön
        </Button>
      </div>
    );
  }

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <Button variant='ghost' size='sm' className='-ml-2 mb-1' nativeButton={false} render={<Link to='/cameras' />}>
            <ArrowLeftIcon />
            Kabin değiştir
          </Button>
          <h1 className='text-lg font-semibold'>{cabinet.name}</h1>
          <p className='text-sm text-muted-foreground'>
            Kabindeki kameraların tali akımı. Tek kamerayı ana akımla izlemek için kutucuğa girin.
          </p>
        </div>
        <Button size='sm' onClick={() => setIsCreating(true)}>
          <PlusIcon />
          Yeni kamera
        </Button>
      </div>

      <Field orientation='horizontal' className='w-auto self-start'>
        <FieldLabel htmlFor='camera-include-passive'>Pasifleri göster</FieldLabel>
        <Switch id='camera-include-passive' checked={includePassive} onCheckedChange={setIncludePassive} />
      </Field>

      {cameras.isError && <p className='text-sm text-destructive'>{cameras.error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4'>
        {cameras.isPending && Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className='aspect-[4/3] w-full rounded-xl' />)}

        {/* Görüntü ve tanım bilgisi TEK kart: kutucuğun kendi kenarlığı/köşesi kapatılır, bilgi şeridi
            aynı kartın içinde üst çizgiyle ayrılır — ayrı ayrı iki kutu gibi durmasınlar. */}
        {visibleCameras.map(camera => (
          <Card key={camera.id} className={cn('gap-0 p-0', !camera.isActive && 'opacity-60')}>
            {camera.isActive ? (
              <CameraTile camera={camera} className='rounded-none border-0' />
            ) : (
              <PassiveCameraPlaceholder camera={camera} />
            )}
            <CameraFacts camera={camera} onEdit={() => setEditing(camera)} />
          </Card>
        ))}
      </div>

      {cameras.data && visibleCameras.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>
            Bu kabinde {includePassive ? '' : 'aktif '}kamera yok.
          </CardContent>
        </Card>
      )}

      <CameraFormDialog mode='create' cabinetId={cabinet.id} open={isCreating} onOpenChange={setIsCreating} />
      <CameraFormDialog mode='edit' camera={editing} open={editing != null} onOpenChange={open => !open && setEditing(null)} />
    </div>
  );
}

/** Pasif kamera izlenemez — sunucu bilet vermez. Yayın yerine yalnızca yer tutucu; düzenleyip aktif etmek için listede. */
function PassiveCameraPlaceholder({ camera }: { camera: CameraDto }) {
  return (
    <div className='flex aspect-video flex-col items-center justify-center gap-1.5 bg-muted p-3 text-center'>
      <VideoOffIcon className='size-6 text-muted-foreground' />
      <p className='truncate text-sm font-medium'>{camera.name}</p>
      <p className='text-xs text-muted-foreground'>Pasif — izlenemez</p>
    </div>
  );
}

/** Kartın alt şeridi: yoklama durumu, uyarı rozetleri, adres ve düzenleme. */
function CameraFacts({ camera, onEdit }: { camera: CameraDto; onEdit: () => void }) {
  return (
    <div className='flex flex-col gap-1.5 border-t p-3'>
      <div className='flex items-start gap-2'>
        <div className='flex min-w-0 flex-1 flex-wrap items-center gap-1.5'>
          <CameraStatusBadge camera={camera} />
          {/* Pasif kayitlar listede GORUNUR — geri alinabilsin diye. */}
          {!camera.isActive && <Badge variant='secondary'>Pasif</Badge>}
          {!camera.isMonitoringEnabled && <Badge variant='outline'>İzleme kapalı</Badge>}
          {!camera.password && <Badge variant='outline'>Parola yok</Badge>}
        </div>
        <Button size='icon-sm' variant='outline' onClick={onEdit} aria-label={`${camera.name} kamerasını düzenle`} title='Düzenle'>
          <PencilIcon />
        </Button>
      </div>

      <p className='truncate text-xs text-muted-foreground'>
        {camera.ipAddress}:{camera.rtspPort}
        {camera.model ? ` · ${camera.model}` : ''}
      </p>
      {camera.lastConnectionError && (
        <p className='truncate text-xs text-destructive' title={camera.lastConnectionError}>
          {camera.lastConnectionError}
        </p>
      )}
    </div>
  );
}
