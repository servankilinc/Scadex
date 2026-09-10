import { useMemo, useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Field, FieldLabel } from '@/components/ui/field';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { CameraTile } from '@/components/camera/camera-tile';
import { useCabinets } from '@/hooks/use-cabinets';
import { useCameras } from '@/hooks/use-cameras';

/**
 * Canlı izleme grid'i — `/cameras`.
 *
 * Tanım/CRUD ekranından (`/admin/cameras`) AYRI: biri kamerayı tanımlamak,
 * diğeri izlemek için. Ayrıca bu rota lazy yükleniyor (bkz. `main.tsx`) —
 * WebRTC kodu, hiç kamera izlemeyen kullanıcının paketine girmesin.
 *
 * Grid **tali akım** kullanır; ana akım yalnızca detay ekranında açılır.
 */
export default function CameraGrid() {
  const cabinets = useCabinets();
  const [selectedCabinetId, setSelectedCabinetId] = useState<string>('');

  const activeCabinets = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);

  // Türetme, efekt DEĞİL — `/admin/cameras` ile aynı kural: `useEffect` +
  // `setState` listenin her gelişinde bir kaskad render tetikler.
  const cabinetId = selectedCabinetId || activeCabinets[0]?.id || '';

  // Pasif kamera izlenemez (sunucu bilet vermez), bu yüzden burada
  // "pasifleri göster" anahtarı YOK — tanım ekranındakinin aksine.
  const cameras = useCameras(cabinetId || undefined, false);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-end justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Canlı İzleme</h1>
          <p className='text-sm text-muted-foreground'>
            Kabindeki kameraların tali akımı. Tek kamerayı ana akımla izlemek için kutucuğa girin.
          </p>
        </div>

        <Field className='w-full max-w-xs'>
          <FieldLabel htmlFor='live-cabinet'>Kabin</FieldLabel>
          {/* Base UI'da `onValueChange` `string | null` veriyor. */}
          <Select value={cabinetId} onValueChange={value => setSelectedCabinetId(value ?? '')}>
            <SelectTrigger id='live-cabinet'>
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
      </div>

      {activeCabinets.length === 0 && !cabinets.isPending && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>
            Önce bir kabin oluşturun — kamera bir kabine bağlıdır.
          </CardContent>
        </Card>
      )}

      {cameras.isError && <p className='text-sm text-destructive'>{cameras.error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4'>
        {cameras.isPending &&
          cabinetId &&
          Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className='aspect-video w-full rounded-xl' />)}

        {cameras.data?.map(camera => <CameraTile key={camera.id} camera={camera} />)}
      </div>

      {cameras.data?.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>
            Bu kabinde aktif kamera yok.
          </CardContent>
        </Card>
      )}
    </div>
  );
}
