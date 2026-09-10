import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { CameraIcon, PencilIcon, PlusIcon, VideoIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Field, FieldLabel } from '@/components/ui/field';
import { useCabinets } from '@/hooks/use-cabinets';
import { useCameras } from '@/hooks/use-cameras';
import type { CameraDto } from '@/models/camera';
import { CameraFormDialog } from '@/components/camera/camera-form-dialog';
import { CameraStatusBadge } from '@/components/camera/camera-status-badge';

/**
 * Kamera yönetimi — `/admin/cameras`.
 *
 * Kameralar diyagramda YER ALMAZ (pini/kablosu yok, verisi SCADA'dan gelmez),
 * bu yüzden kendi ekranındalar. Ortak izleme alanlarının sözleşmesi sunucuda
 * `IMonitoredAsset`; ileride POS cihazı gibi başka bir izlenen tip geldiğinde
 * kendi tablosu ve kendi ekranı olacak.
 *
 * **Canlı görüntü bu ekranda YOK** — bilinçli olarak. Tanımlamak ve izlemek
 * ayrı işler; izleme `/cameras` altında ve o rota lazy yükleniyor, böylece
 * WebRTC kodu bu ekranı açan kullanıcının paketine girmiyor. Karttaki "İzle"
 * bağlantısı oraya götürür.
 */
export default function Cameras() {
  const cabinets = useCabinets();
  const [selectedCabinetId, setSelectedCabinetId] = useState<string>('');
  const [includePassive, setIncludePassive] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<CameraDto | null>(null);

  const activeCabinets = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);

  // İlk aktif kabin varsayılan seçim — kabin seçilmeden ekran boş bir kabuk
  // olurdu ve kullanıcı önce açılır listeyi fark etmek zorunda kalırdı.
  //
  // Bu bir EFEKT + setState DEĞİL, TÜRETME: `useEffect(() => setCabinetId(...))`
  // yazmak, listenin her gelişinde bir kaskad render tetikler (kod tabanında
  // aynı kural `use-mobile.ts` ve `diagram-toolbar.tsx`'te de düzeltilmişti).
  // `selectedCabinetId` kullanıcının açık seçimini tutar; boşken ilk kabine düşer.
  const cabinetId = selectedCabinetId || activeCabinets[0]?.id || '';

  const cameras = useCameras(cabinetId || undefined, includePassive);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Kameralar</h1>
          <p className='text-sm text-muted-foreground'>
            Kabin içindeki IP kameralar. Diyagramda yer almazlar; durumları düzenli yoklama ile belirlenir.
          </p>
        </div>
        <Button size='sm' onClick={() => setIsCreating(true)} disabled={!cabinetId}>
          <PlusIcon />
          Yeni kamera
        </Button>
      </div>

      <div className='flex flex-wrap items-end gap-4'>
        <Field className='w-full max-w-xs'>
          <FieldLabel htmlFor='camera-cabinet'>Kabin</FieldLabel>
          {/* Base UI'da `onValueChange` `string | null` veriyor (temizleme
              durumu); doğrudan `setCabinetId` geçmek tip hatası. */}
          <Select value={cabinetId} onValueChange={value => setSelectedCabinetId(value ?? '')}>
            <SelectTrigger id='camera-cabinet'>
              <SelectValue placeholder={cabinets.isPending ? 'Yükleniyor…' : 'Kabin seçin'} />
            </SelectTrigger>
            <SelectContent>
              {activeCabinets.map(cabinet => (
                <SelectItem key={cabinet.id} value={cabinet.id}>
                  {cabinet.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field orientation='horizontal' className='w-auto'>
          <FieldLabel htmlFor='camera-include-passive'>Pasifleri göster</FieldLabel>
          <Switch id='camera-include-passive' checked={includePassive} onCheckedChange={setIncludePassive} />
        </Field>
      </div>

      {activeCabinets.length === 0 && !cabinets.isPending && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>
            Önce bir kabin oluşturun — kamera bir kabine bağlıdır.
          </CardContent>
        </Card>
      )}

      {cameras.isError && <p className='text-sm text-destructive'>{cameras.error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3'>
        {cameras.isPending && cabinetId && Array.from({ length: 3 }, (_, i) => <Skeleton key={i} className='h-44 w-full rounded-xl' />)}
        {cameras.data?.map(camera => <CameraCard key={camera.id} camera={camera} onEdit={() => setEditing(camera)} />)}
      </div>

      {cameras.data?.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>
            Bu kabinde {includePassive ? '' : 'aktif '}kamera yok.
          </CardContent>
        </Card>
      )}

      <CameraFormDialog mode='create' cabinetId={cabinetId} open={isCreating} onOpenChange={setIsCreating} />
      <CameraFormDialog mode='edit' camera={editing} open={editing != null} onOpenChange={open => !open && setEditing(null)} />
    </div>
  );
}

function CameraCard({ camera, onEdit }: { camera: CameraDto; onEdit: () => void }) {
  return (
    <Card className={camera.isActive ? undefined : 'opacity-60'}>
      <CardHeader>
        <CardTitle className='flex items-center gap-2'>
          <CameraIcon className='size-4 shrink-0' />
          <span className='truncate'>{camera.name}</span>
        </CardTitle>
        <CardDescription className='truncate'>
          {camera.ipAddress}:{camera.rtspPort}
          {camera.model ? ` · ${camera.model}` : ''}
        </CardDescription>
      </CardHeader>

      <CardContent className='flex flex-col gap-3'>
        <div className='flex flex-wrap items-center gap-2'>
          <CameraStatusBadge camera={camera} />
          {/* Pasif kayitlar listede GORUNUR — geri alinabilsin diye. */}
          {!camera.isActive && <Badge variant='secondary'>Pasif</Badge>}
          {!camera.isMonitoringEnabled && <Badge variant='outline'>İzleme kapalı</Badge>}
          {!camera.password && <Badge variant='outline'>Parola yok</Badge>}
        </div>

        {camera.lastConnectionError && <p className='truncate text-xs text-destructive'>{camera.lastConnectionError}</p>}

        <div className='flex gap-2'>
          <Button size='sm' variant='outline' onClick={onEdit} className='flex-1'>
            <PencilIcon />
            Düzenle
          </Button>
          {/* Pasif kamera izlenemez — sunucu bilet vermez. */}
          {camera.isActive && (
            <Button size='sm' variant='secondary' className='flex-1' render={<Link to={`/cameras/${camera.id}`} />}>
              <VideoIcon />
              İzle
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
