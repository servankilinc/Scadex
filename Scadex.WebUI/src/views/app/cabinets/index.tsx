import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { IdCardIcon, PencilIcon, PlusIcon, SearchIcon, WorkflowIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { StatusDot } from '@/components/diagram/template-node';
import { CabinetFormDialog } from '@/components/cabinet/cabinet-form-dialog';
import { CardEnrollmentDialog } from '@/components/cabinet/card-enrollment-dialog';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
import type { CabinetDetailDto } from '@/models/cabinet';
import { deviceStatusLabel } from '@/models/enums';
import { useCabinets } from '@/hooks/use-cabinets';
import { useCabinetOverviewLive } from '@/hooks/use-cabinet-overview-live';
import { usePermission } from '@/lib/auth-session';

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
  // Düğmeler sunucunun zorladığı izinlerle gizlenir: kabin yazımı SCADA adresini taşır, kart tanımlama geçiş yetkisidir.
  const can = usePermission();
  const canConfigure = can('ConfigureSystem');
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<CabinetDetailDto | null>(null);
  /** Kart tanımlama penceresi açık kabin. */
  const [enrollCabinet, setEnrollCabinet] = useState<CabinetDetailDto | null>(null);

  // Pasif kabinler varsayılan olarak gizli; tercih bilerek hatırlanmaz — ekran her girişte kapalı başlar.
  const [showPassive, setShowPassive] = useState(false);
  const passiveCount = (data ?? []).filter(cabinet => !cabinet.isActive).length;

  // Arama yalnızca kabin ADINA bakar (firma dahil değil); Türkçe büyük/küçük harf.
  const [search, setSearch] = useState('');
  const filteredCabinets = useMemo(() => {
    const visible = (data ?? []).filter(cabinet => showPassive || cabinet.isActive);
    const term = search.trim().toLocaleLowerCase('tr');
    if (!term) return visible;
    return visible.filter(cabinet => cabinet.name.toLocaleLowerCase('tr').includes(term));
  }, [data, search, showPassive]);

  const emptyMessage = !data?.length
    ? 'Henüz kabin yok.'
    : search.trim()
      ? 'Aramaya uyan kabin yok.'
      : "Yalnızca pasif kabin var; görmek için 'Pasifleri göster'i açın.";

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Kabinler</h1>
          <p className='text-sm text-muted-foreground'>Diyagramını açmak için bir kabin seçin.</p>
        </div>
        {canConfigure && (
          <Button size='sm' onClick={() => setIsCreating(true)}>
            <PlusIcon />
            Yeni kabin
          </Button>
        )}
      </div>

      <div className='flex flex-wrap items-center gap-4'>
        <div className='relative w-full max-w-xs'>
          <SearchIcon className='pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground' />
          <Input className='pl-8' placeholder='Kabin ara…' value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        {passiveCount > 0 && (
          <Field orientation='horizontal' className='w-auto'>
            <Switch id='cabinets-show-passive' checked={showPassive} onCheckedChange={checked => setShowPassive(checked)} />
            <FieldLabel htmlFor='cabinets-show-passive' className='font-normal whitespace-nowrap'>
              Pasifleri göster ({passiveCount})
            </FieldLabel>
          </Field>
        )}
      </div>

      {isError && <p className='text-sm text-destructive'>{error.message}</p>}

      <DataTable
        columns={COLUMNS}
        rows={filteredCabinets}
        getRowKey={cabinet => cabinet.id}
        isLoading={isPending}
        isRowMuted={cabinet => !cabinet.isActive}
        emptyMessage={emptyMessage}
        actions={cabinet => (
          <>
            {can('ViewDiagram') && (
              <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/cabinets/${cabinet.id}/diagram`} />}>
                <WorkflowIcon />
                Diyagramı aç
              </Button>
            )}
            {cabinet.isActive && can('ManageAccessCards') && (
              <Button size='sm' variant='outline' onClick={() => setEnrollCabinet(cabinet)} title='Kabinin kart okuyucusunda okutulan kartı bir kullanıcıya tanımla'>
                <IdCardIcon />
                Kart tanımlama
              </Button>
            )}
            {canConfigure && (
              <Button size='sm' variant='outline' onClick={() => setEditing(cabinet)}>
                <PencilIcon />
                Düzenle
              </Button>
            )}
          </>
        )}
      />

      <CabinetFormDialog open={isCreating} onOpenChange={setIsCreating} />
      {/* `key` ile her kabin için TAZE bir dialog: form state'i bir önceki
          kabinden taşınmasın diye. */}
      {editing && (
        <CabinetFormDialog key={editing.id} open onOpenChange={open => !open && setEditing(null)} cabinet={editing} />
      )}
      {enrollCabinet && (
        <CardEnrollmentDialog key={enrollCabinet.id} cabinetId={enrollCabinet.id} cabinetName={enrollCabinet.name} onClose={() => setEnrollCabinet(null)} />
      )}
    </div>
  );
}
