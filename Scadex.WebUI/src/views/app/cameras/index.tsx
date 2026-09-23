import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useQueries, type UseQueryResult } from '@tanstack/react-query';
import { SearchIcon, VideoIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import CameraIllustration from '@/assets/camera-illustration.png';
import { getCamerasByCabinet } from '@/api/camera';
import { cameraKeys } from '@/api/query-keys';
import { GLASS_BADGE, GLASS_BUTTON } from '@/lib/glass-styles';
import { cn } from '@/lib/utils';
import { useCabinets } from '@/hooks/use-cabinets';
import type { CabinetDetailDto } from '@/models/cabinet';
import type { CameraDto } from '@/models/camera';

/**
 * Canlı izleme, adım 1 — `/cameras`: kabin seçimi.
 *
 * Bu sayfada hiçbir yayın başlamaz; kart başına yalnızca REST'ten kamera listesi (sayı) çekilir.
 * Kabinin kameraları ve yönetimi ayrı bir sayfada: `/cameras/cabinet/:cabinetId`
 * (`cabinet.tsx`). Rota lazy yükleniyor (bkz. `main.tsx`) — WebRTC kodu, hiç kamera izlemeyen
 * kullanıcının paketine girmesin.
 */
export default function CabinetPicker() {
  const cabinets = useCabinets();
  const activeCabinets = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);

  // Karttaki sayı AKTİF kameralardır — izlenebilecek olanlar. Pasifler kabin sayfasında
  // "Pasifleri göster" ile listelenir.
  const cameraQueries = useQueries({
    queries: activeCabinets.map(cabinet => ({
      queryKey: cameraKeys.byCabinet(cabinet.id, false),
      queryFn: () => getCamerasByCabinet(cabinet.id, false)
    }))
  });

  // Sorgular her zaman TAM (filtresiz) listeden kurulur — arama yalnızca görünümü daraltır,
  // `useQueries` girdisini değil; aksi hâlde arama yazarken sorgu listesi her tuş vuruşunda yeniden
  // kurulup önbelleği boşa harcardı. Kart bu yüzden id ile eşleşir, konumla (index) değil.
  const camerasByCabinetId = useMemo(
    () => new Map(activeCabinets.map((cabinet, i) => [cabinet.id, cameraQueries[i]!])),
    [activeCabinets, cameraQueries]
  );

  // Arama yalnızca kabin ADINA bakar (firma dahil değil); Türkçe büyük/küçük harf.
  const [search, setSearch] = useState('');
  const filteredCabinets = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('tr');
    if (!term) return activeCabinets;
    return activeCabinets.filter(cabinet => cabinet.name.toLocaleLowerCase('tr').includes(term));
  }, [activeCabinets, search]);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Kameralar</h1>
        <p className='text-sm text-muted-foreground'>İzlemek ya da kameralarını yönetmek istediğiniz kabini seçin.</p>
      </div>

      <div className='relative w-full max-w-xs'>
        <SearchIcon className='pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground' />
        <Input className='pl-8' placeholder='Kabin ara…' value={search} onChange={e => setSearch(e.target.value)} />
      </div>

      {cabinets.isError && <p className='text-sm text-destructive'>Kabinler yüklenemedi.</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4'>
        {cabinets.isPending && Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className='h-32 w-full rounded-xl' />)}

        {filteredCabinets.map(cabinet => (
          <CabinetPickerCard key={cabinet.id} cabinet={cabinet} cameras={camerasByCabinetId.get(cabinet.id)!} />
        ))}
      </div>

      {activeCabinets.length === 0 && !cabinets.isPending && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>Önce bir kabin oluşturun — kamera bir kabine bağlıdır.</CardContent>
        </Card>
      )}

      {activeCabinets.length > 0 && filteredCabinets.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>Aramaya uyan kabin yok.</CardContent>
        </Card>
      )}
    </div>
  );
}

function CabinetPickerCard({ cabinet, cameras }: { cabinet: CabinetDetailDto; cameras: UseQueryResult<CameraDto[]> }) {
  const list = cameras.data ?? [];

  // Kart tıklanabilir DEĞİL: yönlendirme yalnızca alttaki bağlantı-düğmeyle — kartın herhangi bir
  // yerine yanlışlıkla dokunmak yayınları başlatmasın; gerçek bir bağlantı olduğu için yeni sekmede de açılır.
  return (
    <Card className='relative isolate min-h-32 overflow-hidden p-0 shadow-md'>
      {/* Kabin fotoğrafı yok; arka plan bu ekranın anlamını (izleme) temsil eden bir kamera
          illüstrasyonu — kabinin kendisini değil. Detaylar, resmin üstündeki koyu katmanla
          (aşağıdaki div) okunaklı kalacak şekilde ÖN planda: header/body ayrımı yerine tek
          katmanlı bir bindirme, kartın kapladığı alandan tasarruf ettirir. */}
      <img src={CameraIllustration} alt='' className='absolute inset-0 -z-20 size-full bg-muted object-cover opacity-50' />
      <div className='absolute inset-0 -z-10 bg-black/70 dark:bg-black/10' />

      <div className='flex h-full flex-col gap-2 p-4 text-white'>
        <div className='min-w-0'>
          <h3 className='truncate text-base font-medium'>{cabinet.name}</h3>
          <p className='truncate text-sm text-white/70'>{cabinet.companyName}</p>
        </div>

        <Badge variant='outline' className={cn('w-fit', GLASS_BADGE)}>
          <VideoIcon />
          {cameras.isPending ? 'Yükleniyor…' : `${list.length} kamera`}
        </Badge>

        <Button
          size='sm'
          variant='outline'
          className={cn('mt-auto w-full', GLASS_BUTTON)}
          nativeButton={false}
          render={<Link to={`/cameras/cabinet/${cabinet.id}`} />}>
          <VideoIcon />
          Kameraları izle
        </Button>
      </div>
    </Card>
  );
}
