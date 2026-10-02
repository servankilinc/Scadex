import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useQueries, type UseQueryResult } from '@tanstack/react-query';
import { SearchIcon, VideoIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
import { getCamerasByCabinet } from '@/api/camera';
import { cameraKeys } from '@/api/query-keys';
import { useCabinets } from '@/hooks/use-cabinets';
import type { CabinetDetailDto } from '@/models/cabinet';
import type { CameraDto } from '@/models/camera';

/**
 * Canlı izleme, adım 1 — `/cameras`: kabin seçimi.
 *
 * Bu sayfada hiçbir yayın başlamaz; satır başına yalnızca REST'ten kamera listesi (sayı) çekilir.
 * Kabinin kameraları ve yönetimi ayrı bir sayfada: `/cameras/cabinet/:cabinetId`
 * (`cabinet.tsx`). Rota lazy yükleniyor (bkz. `main.tsx`) — WebRTC kodu, hiç kamera izlemeyen
 * kullanıcının paketine girmesin.
 */
export default function CabinetPicker() {
  const cabinets = useCabinets();
  const activeCabinets = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);

  // Satırdaki sayı AKTİF kameralardır — izlenebilecek olanlar. Pasifler kabin sayfasında
  // "Pasifleri göster" ile listelenir.
  const cameraQueries = useQueries({
    queries: activeCabinets.map(cabinet => ({
      queryKey: cameraKeys.byCabinet(cabinet.id, false),
      queryFn: () => getCamerasByCabinet(cabinet.id, false)
    }))
  });

  // Sorgular her zaman TAM (filtresiz) listeden kurulur — arama yalnızca görünümü daraltır,
  // `useQueries` girdisini değil; aksi hâlde arama yazarken sorgu listesi her tuş vuruşunda yeniden
  // kurulup önbelleği boşa harcardı. Satır bu yüzden id ile eşleşir, konumla (index) değil.
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

  const columns: DataTableColumn<CabinetDetailDto>[] = [
    { id: 'name', header: 'Kabin', cell: cabinet => <span className='font-medium'>{cabinet.name}</span> },
    { id: 'company', header: 'Firma', cell: cabinet => cabinet.companyName },
    { id: 'cameras', header: 'Kamera', cell: cabinet => <CameraCountBadge cameras={camerasByCabinetId.get(cabinet.id)!} /> }
  ];

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

      {/* Satır tıklanabilir DEĞİL: yönlendirme yalnızca bağlantı-düğmeyle — satırın herhangi bir yerine
          yanlışlıkla dokunmak yayınları başlatmasın; gerçek bir bağlantı olduğu için yeni sekmede de açılır. */}
      <DataTable
        columns={columns}
        rows={filteredCabinets}
        getRowKey={cabinet => cabinet.id}
        isLoading={cabinets.isPending}
        emptyMessage={activeCabinets.length > 0 ? 'Aramaya uyan kabin yok.' : 'Önce bir kabin oluşturun — kamera bir kabine bağlıdır.'}
        actions={cabinet => (
          <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/cameras/cabinet/${cabinet.id}`} />}>
            <VideoIcon />
            Kameraları izle
          </Button>
        )}
      />
    </div>
  );
}

function CameraCountBadge({ cameras }: { cameras: UseQueryResult<CameraDto[]> }) {
  return (
    <Badge variant='outline'>
      <VideoIcon />
      {cameras.isPending ? 'Yükleniyor…' : `${cameras.data?.length ?? 0} kamera`}
    </Badge>
  );
}
