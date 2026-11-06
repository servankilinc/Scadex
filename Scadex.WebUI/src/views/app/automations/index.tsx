import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { IdCardIcon, PencilIcon, PlusIcon, SearchIcon, WorkflowIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { StatusDot } from '@/components/diagram/template-node';
import { AutomationFormDialog } from '@/components/automation/automation-form-dialog';
import { CardEnrollmentDialog } from '@/components/automation/card-enrollment-dialog';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
import type { AutomationDetailDto } from '@/models/automation';
import { deviceStatusLabel } from '@/models/enums';
import { useAutomations } from '@/hooks/use-automations';
import { useAutomationOverviewLive } from '@/hooks/use-automation-overview-live';
import { usePermission } from '@/lib/auth-session';

const COLUMNS: DataTableColumn<AutomationDetailDto>[] = [
  {
    id: 'name',
    header: 'Otomasyon',
    cell: automation => (
      <div className='flex items-center gap-2'>
        <span className='font-medium'>{automation.name}</span>
        {!automation.isActive && <Badge variant='secondary'>Pasif</Badge>}
      </div>
    )
  },
  { id: 'company', header: 'Firma', cell: automation => automation.companyName },
  {
    id: 'status',
    header: 'Durum',
    cell: automation => (
      <Badge variant='outline'>
        <StatusDot statusId={automation.deviceStatusId} />
        {deviceStatusLabel(automation.deviceStatusId)}
      </Badge>
    )
  },
  {
    id: 'location',
    header: 'Konum',
    className: 'max-w-xs truncate',
    cell: automation => automation.locationDescription ?? '—'
  }
];

/**
 * Otomasyon listesi — diyagram editörünün giriş noktası.
 *
 * Durum kolonu canlıdır (`useAutomationOverviewLive`). Arama + tablo düzeni Kameralar'ın otomasyon
 * seçimiyle (`modules/cameramanagement/views/cameras`) aynıdır.
 */
export default function Automations() {
  const { data, isPending, isError, error } = useAutomations();
  useAutomationOverviewLive();
  // Düğmeler sunucunun zorladığı izinlerle gizlenir: otomasyon yazımı SCADA adresini taşır, kart tanımlama geçiş yetkisidir.
  const can = usePermission();
  const canConfigure = can('ConfigureSystem');
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<AutomationDetailDto | null>(null);
  /** Kart tanımlama penceresi açık otomasyon. */
  const [enrollAutomation, setEnrollAutomation] = useState<AutomationDetailDto | null>(null);

  // Pasif otomasyonlar varsayılan olarak gizli; tercih bilerek hatırlanmaz — ekran her girişte kapalı başlar.
  const [showPassive, setShowPassive] = useState(false);
  const passiveCount = (data ?? []).filter(automation => !automation.isActive).length;

  // Arama yalnızca otomasyon ADINA bakar (firma dahil değil); Türkçe büyük/küçük harf.
  const [search, setSearch] = useState('');
  const filteredAutomations = useMemo(() => {
    const visible = (data ?? []).filter(automation => showPassive || automation.isActive);
    const term = search.trim().toLocaleLowerCase('tr');
    if (!term) return visible;
    return visible.filter(automation => automation.name.toLocaleLowerCase('tr').includes(term));
  }, [data, search, showPassive]);

  const emptyMessage = !data?.length
    ? 'Henüz otomasyon yok.'
    : search.trim()
      ? 'Aramaya uyan otomasyon yok.'
      : "Yalnızca pasif otomasyon var; görmek için 'Pasifleri göster'i açın.";

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Otomasyonlar</h1>
          <p className='text-sm text-muted-foreground'>Diyagramını açmak için bir otomasyon seçin.</p>
        </div>
        {canConfigure && (
          <Button size='sm' onClick={() => setIsCreating(true)}>
            <PlusIcon />
            Yeni otomasyon
          </Button>
        )}
      </div>

      <div className='flex flex-wrap items-center gap-4'>
        <div className='relative w-full max-w-xs'>
          <SearchIcon className='pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground' />
          <Input className='pl-8' placeholder='Otomasyon ara…' value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        {passiveCount > 0 && (
          <Field orientation='horizontal' className='w-auto'>
            <Switch id='automations-show-passive' checked={showPassive} onCheckedChange={checked => setShowPassive(checked)} />
            <FieldLabel htmlFor='automations-show-passive' className='font-normal whitespace-nowrap'>
              Pasifleri göster ({passiveCount})
            </FieldLabel>
          </Field>
        )}
      </div>

      {isError && <p className='text-sm text-destructive'>{error.message}</p>}

      <DataTable
        columns={COLUMNS}
        rows={filteredAutomations}
        getRowKey={automation => automation.id}
        isLoading={isPending}
        isRowMuted={automation => !automation.isActive}
        emptyMessage={emptyMessage}
        actions={automation => (
          <>
            {can('ViewDiagram') && (
              <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/automations/${automation.id}/diagram`} />}>
                <WorkflowIcon />
                Diyagramı aç
              </Button>
            )}
            {automation.isActive && can('ManageAccessCards') && (
              <Button size='sm' variant='outline' onClick={() => setEnrollAutomation(automation)} title='Otomasyonun kart okuyucusunda okutulan kartı bir kullanıcıya tanımla'>
                <IdCardIcon />
                Kart tanımlama
              </Button>
            )}
            {canConfigure && (
              <Button size='sm' variant='outline' onClick={() => setEditing(automation)}>
                <PencilIcon />
                Düzenle
              </Button>
            )}
          </>
        )}
      />

      <AutomationFormDialog open={isCreating} onOpenChange={setIsCreating} />
      {/* `key` ile her otomasyon için TAZE bir dialog: form state'i bir önceki
          otomasyondan taşınmasın diye. */}
      {editing && (
        <AutomationFormDialog key={editing.id} open onOpenChange={open => !open && setEditing(null)} automation={editing} />
      )}
      {enrollAutomation && (
        <CardEnrollmentDialog key={enrollAutomation.id} automationId={enrollAutomation.id} automationName={enrollAutomation.name} onClose={() => setEnrollAutomation(null)} />
      )}
    </div>
  );
}
