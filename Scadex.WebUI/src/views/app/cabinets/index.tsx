import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { CpuIcon, MapPinIcon, PencilIcon, PlusIcon, SearchIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusDot } from '@/components/diagram/template-node';
import { CabinetFormDialog } from '@/components/cabinet/cabinet-form-dialog';
import ScadaPanelImage from '@/assets/bg-scada-diagram.jpg';
import { GLASS_BADGE, GLASS_BUTTON } from '@/lib/glass-styles';
import { cn } from '@/lib/utils';
import type { CabinetDetailDto } from '@/models/cabinet';
import { deviceStatusLabel } from '@/models/enums';
import { useCabinets } from '@/hooks/use-cabinets';
import { useCabinetOverviewLive } from '@/hooks/use-cabinet-overview-live';

/**
 * Kabin kartları — diyagram editörünün giriş noktası.
 *
 * Tablo değil kart: kabin sayısı azdır ve karar verirken bakılan şey durum ve
 * konumdur, sıralanabilir sütunlar değil. Düzen Canlı İzleme'nin kabin seçimiyle
 * (`views/app/cameras`) aynıdır: arama + resimli kart grid'i.
 */
export default function Cabinets() {
  const { data, isPending, isError, error } = useCabinets();
  useCabinetOverviewLive();
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<CabinetDetailDto | null>(null);

  // Arama yalnızca kabin ADINA bakar (firma dahil değil); Türkçe büyük/küçük harf.
  const [search, setSearch] = useState('');
  const filteredCabinets = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('tr');
    if (!term) return data ?? [];
    return (data ?? []).filter(cabinet => cabinet.name.toLocaleLowerCase('tr').includes(term));
  }, [data, search]);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Kabinler</h1>
          <p className='text-sm text-muted-foreground'>Diyagramını açmak için bir kabin seçin.</p>
        </div>
        <Button size='sm' onClick={() => setIsCreating(true)}>
          <PlusIcon />
          Yeni kabin
        </Button>
      </div>

      <div className='relative w-full max-w-xs'>
        <SearchIcon className='pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground' />
        <Input className='pl-8' placeholder='Kabin ara…' value={search} onChange={e => setSearch(e.target.value)} />
      </div>

      {isError && <p className='text-sm text-destructive'>{error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4'>
        {isPending && Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className='h-40 w-full rounded-xl' />)}
        {filteredCabinets.map(cabinet => (
          <CabinetCard key={cabinet.id} cabinet={cabinet} onEdit={() => setEditing(cabinet)} />
        ))}
      </div>

      {data?.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>Henüz kabin yok.</CardContent>
        </Card>
      )}

      {!!data?.length && filteredCabinets.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>Aramaya uyan kabin yok.</CardContent>
        </Card>
      )}

      <CabinetFormDialog open={isCreating} onOpenChange={setIsCreating} />
      {/* `key` ile her kabin için TAZE bir dialog: form state'i bir önceki
          kabinden taşınmasın diye. */}
      {editing && (
        <CabinetFormDialog key={editing.id} open onOpenChange={open => !open && setEditing(null)} cabinet={editing} />
      )}
    </div>
  );
}

function CabinetCard({ cabinet, onEdit }: { cabinet: CabinetDetailDto; onEdit: () => void }) {
  return (
    <Card className={cn('relative isolate min-h-40 overflow-hidden p-0 shadow-md', !cabinet.isActive && 'opacity-60')}>
      {/* Kabin fotoğrafı yok; arka plan kabinin ne olduğunu (pano içi) temsil eden bir illüstrasyon.
          Detaylar, resmin üstündeki koyu katmanla okunaklı kalacak şekilde ÖN planda — Canlı İzleme
          kabin kartıyla aynı iki katman. */}
      <img src={ScadaPanelImage} alt='' className='absolute inset-0 -z-20 size-full bg-muted object-cover opacity-50' />
      <div className='absolute inset-0 -z-10 bg-black/70 dark:bg-black/10' />

      <div className='flex h-full flex-col gap-3 p-4 text-white'>
        <div className='min-w-0'>
          <h3 className='flex items-center gap-2 text-base font-medium'>
            <CpuIcon className='size-4 shrink-0' />
            <span className='truncate'>{cabinet.name}</span>
          </h3>
          <p className='truncate text-sm text-white/70'>{cabinet.companyName}</p>
        </div>

        <div className='flex flex-wrap items-center gap-1.5'>
          {/* `CabinetStatusBadge` yerine cam rozet + durum noktası: onun "Bilinmiyor" hâli
              `text-foreground` kullanır ve açık temada koyu katmanın üstünde görünmez olurdu. */}
          <Badge variant='outline' className={GLASS_BADGE}>
            <StatusDot statusId={cabinet.deviceStatusId} />
            {deviceStatusLabel(cabinet.deviceStatusId)}
          </Badge>
          {/* Pasif kayitlar listede GORUNUR — IsActive global query filter'i
              bilerek yok, pasife alinan bir kabin geri alinabilsin diye. */}
          {!cabinet.isActive && (
            <Badge variant='outline' className={GLASS_BADGE}>
              Pasif
            </Badge>
          )}
        </div>

        {cabinet.locationDescription && (
          <p className='flex items-center gap-1 truncate text-xs text-white/70'>
            <MapPinIcon className='size-3 shrink-0' />
            {cabinet.locationDescription}
          </p>
        )}

        <div className='mt-auto flex gap-2'>
          <Button
            size='sm'
            variant='outline'
            className={cn('flex-1', GLASS_BUTTON)}
            nativeButton={false}
            render={<Link to={`/cabinets/${cabinet.id}/diagram`} />}>

            Diyagramı aç
          </Button>
          <Button size='sm' variant='outline' className={GLASS_BUTTON} onClick={onEdit} aria-label={`${cabinet.name} kabinini düzenle`}>
            <PencilIcon />
          </Button>
        </div>
      </div>
    </Card>
  );
}
