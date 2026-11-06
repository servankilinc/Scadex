import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useQueries, type UseQueryResult } from '@tanstack/react-query';
import { SearchIcon, VideoIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
import { getCamerasByAutomation } from '../../api/camera';
import { cameraKeys } from '../../api/query-keys';
import { useAutomations } from '@/hooks/use-automations';
import type { AutomationDetailDto } from '@/models/automation';
import type { CameraDto } from '../../models/camera';

/**
 * Canlı izleme, adım 1 — `/cameras`: otomasyon seçimi.
 *
 * Bu sayfada hiçbir yayın başlamaz; satır başına yalnızca REST'ten kamera listesi (sayı) çekilir.
 * Otomasyonun kameraları ve yönetimi ayrı bir sayfada: `/cameras/automation/:automationId`
 * (`automation.tsx`). Rota lazy yükleniyor (bkz. `main.tsx`) — WebRTC kodu, hiç kamera izlemeyen
 * kullanıcının paketine girmesin.
 */
export default function AutomationPicker() {
  const automations = useAutomations();
  const activeAutomations = useMemo(() => automations.data?.filter(c => c.isActive) ?? [], [automations.data]);

  // Satırdaki sayı AKTİF kameralardır — izlenebilecek olanlar. Pasifler otomasyon sayfasında
  // "Pasifleri göster" ile listelenir.
  const cameraQueries = useQueries({
    queries: activeAutomations.map(automation => ({
      queryKey: cameraKeys.byAutomation(automation.id, false),
      queryFn: () => getCamerasByAutomation(automation.id, false)
    }))
  });

  // Sorgular her zaman TAM (filtresiz) listeden kurulur — arama yalnızca görünümü daraltır,
  // `useQueries` girdisini değil; aksi hâlde arama yazarken sorgu listesi her tuş vuruşunda yeniden
  // kurulup önbelleği boşa harcardı. Satır bu yüzden id ile eşleşir, konumla (index) değil.
  const camerasByAutomationId = useMemo(
    () => new Map(activeAutomations.map((automation, i) => [automation.id, cameraQueries[i]!])),
    [activeAutomations, cameraQueries]
  );

  // Arama yalnızca otomasyon ADINA bakar (firma dahil değil); Türkçe büyük/küçük harf.
  const [search, setSearch] = useState('');
  const filteredAutomations = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('tr');
    if (!term) return activeAutomations;
    return activeAutomations.filter(automation => automation.name.toLocaleLowerCase('tr').includes(term));
  }, [activeAutomations, search]);

  const columns: DataTableColumn<AutomationDetailDto>[] = [
    { id: 'name', header: 'Otomasyon', cell: automation => <span className='font-medium'>{automation.name}</span> },
    { id: 'company', header: 'Firma', cell: automation => automation.companyName },
    { id: 'cameras', header: 'Kamera', cell: automation => <CameraCountBadge cameras={camerasByAutomationId.get(automation.id)!} /> }
  ];

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Kameralar</h1>
        <p className='text-sm text-muted-foreground'>İzlemek ya da kameralarını yönetmek istediğiniz otomasyonu seçin.</p>
      </div>

      <div className='relative w-full max-w-xs'>
        <SearchIcon className='pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground' />
        <Input className='pl-8' placeholder='Otomasyon ara…' value={search} onChange={e => setSearch(e.target.value)} />
      </div>

      {automations.isError && <p className='text-sm text-destructive'>Otomasyonlar yüklenemedi.</p>}

      {/* Satır tıklanabilir DEĞİL: yönlendirme yalnızca bağlantı-düğmeyle — satırın herhangi bir yerine
          yanlışlıkla dokunmak yayınları başlatmasın; gerçek bir bağlantı olduğu için yeni sekmede de açılır. */}
      <DataTable
        columns={columns}
        rows={filteredAutomations}
        getRowKey={automation => automation.id}
        isLoading={automations.isPending}
        emptyMessage={activeAutomations.length > 0 ? 'Aramaya uyan otomasyon yok.' : 'Önce bir otomasyon oluşturun — kamera bir otomasyona bağlıdır.'}
        actions={automation => (
          <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/cameras/automation/${automation.id}`} />}>
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
