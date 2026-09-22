import { Link, useParams } from 'react-router';
import { DoorClosedIcon, DoorOpenIcon, HelpCircleIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import CabinetImage from '@/assets/cabinet-inproces-2.png';
import { useSignalCabinetLive } from '../../hooks/use-virtual-cabinet';
import type { SignalOuterDoorLiveDto } from '../../models/virtual-cabinet';

/**
 * Sanal kabin ekranının ara adımı — `/signalization/virtual-cabinet/:cabinetId`.
 *
 * Kabin tek bir "iç" değildir: her dış kapının ardında başka iç kapılar, başka bir kamera ve başka bir aydınlatma
 * vardır. Yönetim ekranı bu yüzden kabine değil **dış kapıya** açılır; burası o seçimi yaptırır.
 */
export default function VirtualCabinetPicker() {
  const { cabinetId = '' } = useParams<{ cabinetId: string }>();
  const { data, isPending, error } = useSignalCabinetLive(cabinetId);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Sanal Kabin{data?.cabinetName ? ` · ${data.cabinetName}` : ''}</h1>
        <p className='text-sm text-muted-foreground'>Yönetmek istediğiniz dış kapıyı seçin. Kabinin sireni ortaktır; kamera, aydınlatma ve iç kapılar dış kapıya bağlıdır.</p>
      </div>

      {error && <p className='text-sm text-destructive'>{error.message}</p>}

      {isPending && !error && (
        <div className='grid gap-4 sm:grid-cols-2 lg:grid-cols-3'>
          <Skeleton className='h-72 w-full rounded-xl' />
          <Skeleton className='h-72 w-full rounded-xl' />
        </div>
      )}

      {data && data.outerDoors.length === 0 && (
        <p className='text-sm text-muted-foreground'>
          Bu kabinde tanımlı dış kapı yok.{' '}
          <Link to={`/signalization/cabinets?cabinetId=${cabinetId}`} className='underline underline-offset-4'>
            Kapı yapılandırmasından
          </Link>{' '}
          ekleyebilirsiniz.
        </p>
      )}

      {data && !data.isEnabled && data.outerDoors.length > 0 && (
        <p className='text-sm text-amber-700 dark:text-amber-400'>Bu kabinin sinyalizasyonu pasif: motor kabinin saha olaylarını yok sayıyor. Elle komut yine de gönderilebilir.</p>
      )}

      {data && data.outerDoors.length > 0 && (
        <div className='grid gap-4 sm:grid-cols-2 lg:grid-cols-3'>
          {data.outerDoors.map(door => (
            <OuterDoorCard key={door.id} cabinetId={cabinetId} door={door} />
          ))}
        </div>
      )}
    </div>
  );
}

function OuterDoorCard({ cabinetId, door }: { cabinetId: string; door: SignalOuterDoorLiveDto }) {
  return (
    <Link to={`/signalization/virtual-cabinet/${cabinetId}/${door.id}`} className='group block focus-visible:outline-none'>
      <Card className='h-full transition-colors hover:border-primary/60 group-focus-visible:border-primary'>
        <CardHeader>
          <CardTitle className='flex items-center justify-between gap-2'>
            <span className='truncate' title={door.name}>
              {door.name}
            </span>
            <DoorStateBadge isOpen={door.isOpen} />
          </CardTitle>
        </CardHeader>

        <CardContent>
          <img src={CabinetImage} alt='' aria-hidden className='mx-auto h-40 w-auto transition-transform group-hover:scale-105' />
        </CardContent>

        <CardFooter className='flex-col items-start gap-2 border-t'>
          <p className='text-xs font-medium tracking-wide text-muted-foreground uppercase'>Ardındaki iç kapılar</p>
          {door.innerDoors.length === 0 ? (
            <p className='text-xs text-muted-foreground italic'>İç kapı tanımlı değil</p>
          ) : (
            <div className='flex flex-wrap gap-1.5'>
              {door.innerDoors.map(inner => (
                <Badge key={inner.id} variant='outline' title={inner.authorityName ?? undefined}>
                  {inner.name}
                </Badge>
              ))}
            </div>
          )}
        </CardFooter>
      </Card>
    </Link>
  );
}

/** `null` = anahtar okunamıyor; kapalıdan ayrı gösterilir. */
function DoorStateBadge({ isOpen }: { isOpen: boolean | null }) {
  if (isOpen === null) {
    return (
      <Badge variant='outline' className='shrink-0'>
        <HelpCircleIcon />
        Bilinmiyor
      </Badge>
    );
  }

  return isOpen ? (
    <Badge variant='destructive' className='shrink-0'>
      <DoorOpenIcon />
      Açık
    </Badge>
  ) : (
    <Badge variant='outline' className='shrink-0'>
      <DoorClosedIcon />
      Kapalı
    </Badge>
  );
}
