import { useState } from 'react';
import { Grid2X2Icon, HandIcon, MousePointer2Icon, SettingsIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Switch } from '@/components/ui/switch';
import { BackgroundVariant, BackgroundVariantLabels, DeviceStatus, DeviceStatusLabels } from '@/models/enums';
import type { DiagramCabinetDto, DiagramCanvasSettingsDto } from '@/models/diagram';
import { canvasSettingsUpsertSchema } from '@/models/canvasSettings';
import { useAppDispatch, useAppSelector } from '@/hooks';
import { useCanvasSettings } from '@/hooks/use-canvas-settings';
import type { SaveController } from '@/hooks/use-diagram-save';
import { useLiveCabinet } from '@/lib/diagram/live-store';
import type { HubStatus } from '@/lib/signalr/diagram-hub';
import { cn } from '@/lib/utils';
import { setMode } from '@/store/reducers/diagramSlice';
import { LiveIndicator } from './live-indicator';
import { SaveIndicator } from './save-indicator';

interface DiagramToolbarProps {
  cabinet: DiagramCabinetDto;
  settings: DiagramCanvasSettingsDto;
  save: SaveController;
  isDirty: boolean;
  hubStatus: HubStatus;
}

export function DiagramToolbar({ cabinet, settings, save, isDirty, hubStatus }: DiagramToolbarProps) {
  // Canlı değerler sunucu anlık görüntüsünü EZER: snapshot grafın çekildiği andaki
  // durumu taşır, store ondan sonrasını.
  const live = useLiveCabinet(cabinet.id);
  const statusId = live ? live.statusId : cabinet.deviceStatusId;
  const lastIngestAt = live ? live.scadaLastIngestAt : cabinet.scadaLastIngestAt;

  return (
    <header className='flex items-center gap-3 border-b px-3 py-2'>
      <div className='min-w-0'>
        <h1 className='truncate text-sm font-semibold'>{cabinet.name}</h1>
        <p className='text-muted-foreground text-xs'>
          {/* null = hic telemetri alinmadi; Offline ile AYNI SEY DEGIL */}
          {statusId == null ? 'Telemetri yok' : (DeviceStatusLabels[statusId] ?? cabinet.deviceStatusName ?? 'Bilinmiyor')}
        </p>
      </div>

      {!cabinet.scadaIsEnabled && (
        <Badge variant='secondary' className='shrink-0'>
          SCADA kapalı
        </Badge>
      )}

      <ModeSwitch />

      <div className='ml-auto flex items-center gap-3'>
        {/* SCADA'si kapali kabinde canli gosterge YANILTICI olurdu: hicbir zaman
            veri gelmeyecek, "baglanti yok" ise yanlis teshise goturur. */}
        {cabinet.scadaIsEnabled && <LiveIndicator status={hubStatus} lastIngestAt={lastIngestAt} />}
        <SaveIndicator save={save} isDirty={isDirty} />
        <span className='text-muted-foreground hidden items-center gap-1 text-xs sm:flex'>
          <Grid2X2Icon className='size-3.5' />
          {settings.gridSize}px
        </span>
        <CanvasSettingsPopover cabinetId={cabinet.id} settings={settings} />
      </div>
    </header>
  );
}

/**
 * Sol tuşun ne yaptığını seçen anahtar.
 *
 * React Flow'un varsayılanında boş alanı sürüklemek canvas'ı KAYDIRIR ve çoklu
 * seçim ancak Shift basılıyken yapılabilir. Bu, çoklu seçimi klavyeye bağlı tek
 * yol hâline getiriyordu — hizalama düğmeleri de çoklu seçim olmadan işe
 * yaramadığı için, aslında bütün F2 klavyeye bağlıydı.
 *
 * Görünür bir mod anahtarı bunu çözüyor: "Seç" modunda sürüklemek kutu seçimi
 * yapar. Shift yine çalışıyor, ama artık tek yol değil.
 */
function ModeSwitch() {
  const dispatch = useAppDispatch();
  const mode = useAppSelector(s => s.diagram.mode);

  return (
    <div className='flex shrink-0 items-center gap-1'>
      <Button
        size='xs'
        variant={mode === 'select' ? 'default' : 'outline'}
        title='Sürüklemek kutu seçimi yapar'
        onClick={() => dispatch(setMode('select'))}>
        <MousePointer2Icon />
        Seç
      </Button>
      <Button size='xs' variant={mode === 'pan' ? 'default' : 'outline'} title='Sürüklemek canvas’ı kaydırır' onClick={() => dispatch(setMode('pan'))}>
        <HandIcon />
        Kaydır
      </Button>
    </div>
  );
}

const VARIANTS: BackgroundVariant[] = [BackgroundVariant.None, BackgroundVariant.Dots, BackgroundVariant.Lines, BackgroundVariant.Cross];

function CanvasSettingsPopover({ cabinetId, settings }: { cabinetId: string; settings: DiagramCanvasSettingsDto }) {
  const mutation = useCanvasSettings(cabinetId);
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(settings);
  const [issue, setIssue] = useState<string | null>(null);

  /**
   * Taslak, panel ACILIRKEN tazelenir — bir effect'le degil.
   *
   * `useEffect(() => setDraft(settings), [settings])` yazmak kademeli render
   * tetikler (ayni kural `use-mobile.ts`'te de duzeltilmisti) ve dahasi
   * kullanici panelde bir seyi degistirmisken sunucudan gelen bir guncelleme
   * yazdiklarini silerdi. Acilis ani, taze degerlerle baslamak icin dogru an.
   */
  function handleOpenChange(next: boolean) {
    if (next) {
      setDraft(settings);
      setIssue(null);
    }
    setOpen(next);
  }

  function save() {
    const parsed = canvasSettingsUpsertSchema.safeParse(draft);
    if (!parsed.success) {
      // Sinirlar backend validator'iyla ayni; burada yakalamak kullaniciya
      // sunucuya gidip 400 ile donmekten daha hizli geri bildirim veriyor.
      setIssue(parsed.error.issues[0]?.message ?? 'Geçersiz ayar');
      return;
    }
    setIssue(null);
    mutation.mutate(parsed.data, { onSuccess: () => setOpen(false) });
  }

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger
        render={
          <Button variant='outline' size='sm'>
            <SettingsIcon /> Canvas
          </Button>
        }
      />
      <PopoverContent className='w-64'>
        <div className='flex flex-col gap-3'>
          <div className='flex items-center justify-between gap-2'>
            <Label htmlFor='gridSize'>Grid boyutu</Label>
            <Input
              id='gridSize'
              type='number'
              min={1}
              max={500}
              className='h-7 w-20'
              value={draft.gridSize}
              onChange={e => setDraft({ ...draft, gridSize: Number(e.target.value) })}
            />
          </div>

          <div className='flex items-center justify-between gap-2'>
            <Label htmlFor='snapToGrid'>Grid'e yapış</Label>
            <Switch id='snapToGrid' checked={draft.snapToGrid} onCheckedChange={checked => setDraft({ ...draft, snapToGrid: checked })} />
          </div>

          <div className='flex flex-col gap-1.5'>
            <Label>Arka plan</Label>
            <div className='grid grid-cols-2 gap-1'>
              {VARIANTS.map(variant => (
                <Button
                  key={variant}
                  size='xs'
                  variant={draft.backgroundVariant === variant ? 'default' : 'outline'}
                  onClick={() => setDraft({ ...draft, backgroundVariant: variant })}>
                  {BackgroundVariantLabels[variant]}
                </Button>
              ))}
            </div>
          </div>

          {issue && <p className='text-destructive text-xs'>{issue}</p>}

          <Button size='sm' onClick={save} disabled={mutation.isPending}>
            {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}

/** Kabin durumu rozeti — kabin listesinde de kullanılır. */
export function CabinetStatusBadge({ statusId, className }: { statusId: DeviceStatus | null; className?: string }) {
  if (statusId == null) {
    return (
      <Badge variant='outline' className={cn('shrink-0', className)}>
        Telemetri yok
      </Badge>
    );
  }

  return (
    <Badge variant={statusId === DeviceStatus.Online ? 'default' : statusId === DeviceStatus.Offline ? 'secondary' : 'destructive'} className={cn('shrink-0', className)}>
      {DeviceStatusLabels[statusId]}
    </Badge>
  );
}
