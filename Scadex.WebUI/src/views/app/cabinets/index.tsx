import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { PencilIcon, PlusIcon, SearchIcon, WorkflowIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { StatusDot } from '@/components/diagram/template-node';
import { CabinetFormDialog } from '@/components/cabinet/cabinet-form-dialog';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
import type { CabinetDetailDto } from '@/models/cabinet';
import { deviceStatusLabel } from '@/models/enums';
import { useCabinets } from '@/hooks/use-cabinets';
import { useCabinetOverviewLive } from '@/hooks/use-cabinet-overview-live';

const COLUMNS: DataTableColumn<CabinetDetailDto>[] = [
  {
    id: 'name',
    header: 'Kabin',
    cell: cabinet => (
      <div className='flex items-center gap-2'>
        <span className='font-medium'>{cabinet.name}</span>
        {!cabinet.isActive && <Badge variant='secondary'>Pasif</Badge>}
      </div>
    )
  },
  { id: 'company', header: 'Firma', cell: cabinet => cabinet.companyName },
  {
    id: 'status',
    header: 'Durum',
    cell: cabinet => (
      <Badge variant='outline'>
        <StatusDot statusId={cabinet.deviceStatusId} />
        {deviceStatusLabel(cabinet.deviceStatusId)}
      </Badge>
    )
  },
  {
    id: 'location',
    header: 'Konum',
    className: 'max-w-xs truncate',
    cell: cabinet => cabinet.locationDescription ?? '—'
  }
];

/**
 * Kabin listesi — diyagram editörünün giriş noktası.
 *
 * Durum kolonu canlıdır (`useCabinetOverviewLive`). Arama + tablo düzeni Kameralar'ın kabin
 * seçimiyle (`views/app/cameras`) aynıdır.
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

      <DataTable
        columns={COLUMNS}
        rows={filteredCabinets}
        getRowKey={cabinet => cabinet.id}
        isLoading={isPending}
        isRowMuted={cabinet => !cabinet.isActive}
        emptyMessage={data?.length ? 'Aramaya uyan kabin yok.' : 'Henüz kabin yok.'}
        actions={cabinet => (
          <>
            <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/cabinets/${cabinet.id}/diagram`} />}>
              <WorkflowIcon />
              Diyagramı aç
            </Button>
            <Button size='sm' variant='outline' onClick={() => setEditing(cabinet)}>
              <PencilIcon />
              Düzenle
            </Button>
          </>
        )}
      />

      <CabinetFormDialog open={isCreating} onOpenChange={setIsCreating} />
      {/* `key` ile her kabin için TAZE bir dialog: form state'i bir önceki
          kabinden taşınmasın diye. */}
      {editing && (
        <CabinetFormDialog key={editing.id} open onOpenChange={open => !open && setEditing(null)} cabinet={editing} />
      )}
    </div>
  );
}
