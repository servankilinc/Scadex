import { useState, type DragEvent } from 'react';
import {
  ChevronRightIcon,
  CpuIcon,
  GaugeIcon,
  IdCardIcon,
  LightbulbIcon,
  LogInIcon,
  LogOutIcon,
  MoveRightIcon,
  PanelLeftCloseIcon,
  PanelLeftOpenIcon,
  PlugZapIcon,
  PuzzleIcon,
  RadarIcon,
  Rows3Icon,
  SquareIcon,
  StickyNoteIcon,
  ToggleLeftIcon,
  TypeIcon,
  ZapIcon,
  type LucideIcon
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Skeleton } from '@/components/ui/skeleton';
import { AnnotationShape, AnnotationShapeLabels, DeviceType, DeviceTypeLabels } from '@/models/enums';
import type { ComponentTemplatePaletteDto } from '@/models/componentTemplate';
import { readableTextColor, safeCssColor } from '@/lib/diagram/colors';
import { setTemplateDragData } from '@/lib/diagram/dnd';
import { useComponentTemplatePalette } from '@/hooks/use-component-templates';

/** Şablon kartında ve grup başlığında kullanılan, DeviceType başına TEK ikon. */
const DEVICE_TYPE_ICON: Record<DeviceType, LucideIcon> = {
  [DeviceType.ControlModule]: CpuIcon,
  [DeviceType.InputModule]: LogInIcon,
  [DeviceType.OutputModule]: LogOutIcon,
  [DeviceType.LedModule]: LightbulbIcon,
  [DeviceType.TerminalBlock]: Rows3Icon,
  [DeviceType.Sensor]: RadarIcon,
  [DeviceType.Peripheral]: PuzzleIcon,
  [DeviceType.PowerSupply]: ZapIcon,
  [DeviceType.MeasurementDevice]: GaugeIcon,
  [DeviceType.CardReader]: IdCardIcon,
  [DeviceType.Mains]: PlugZapIcon,
  [DeviceType.CircuitBreaker]: ToggleLeftIcon
};

/**
 * Grupların gösterim sırası: `DeviceType`'ın TANIM sırası (ControlModule →
 * CircuitBreaker). API yanıtının sırasına bağlı kalınmıyor — sunucu farklı bir
 * sırayla dönerse grupların yeri değişir, kullanıcı her seferinde aynı yerde
 * arar.
 */
const DEVICE_TYPE_ORDER = Object.values(DeviceType) as DeviceType[];

/** Katalogdaki tanım sırasını koruyarak DeviceType'a göre gruplar (bkz. roles/index.tsx > groupByCategory). */
function groupByDeviceType(templates: ComponentTemplatePaletteDto[]): [DeviceType, ComponentTemplatePaletteDto[]][] {
  const groups = new Map<DeviceType, ComponentTemplatePaletteDto[]>();
  for (const template of templates) {
    const group = groups.get(template.deviceTypeId);
    if (group) group.push(template);
    else groups.set(template.deviceTypeId, [template]);
  }
  return DEVICE_TYPE_ORDER.filter(type => groups.has(type)).map(type => [type, groups.get(type)!]);
}

/**
 * Stencil kütüphanesi + not araçları.
 *
 * Şablon kartları canvas'a sürüklenir; bırakılan şablondan sunucu cihazı ve
 * pinlerini TEK transaction'da üretir. Tıklamak bir şey yapmaz — bırakma noktası
 * olmadan cihazın nereye konacağı belirsiz olurdu.
 */
export function PalettePanel({ onAddAnnotation }: { onAddAnnotation: (shape: AnnotationShape) => void }) {
  const { data, isPending, isError, error } = useComponentTemplatePalette();
  // Sekme yenilenince ACIK'a döner: bilerek kalıcı değil — küçük ekranda
  // kapatılmış bir panelin bir sonraki oturumda unutulup kaybolması, tekrar
  // tekrar "şablonlar nerede" sorusunu doğururdu.
  const [collapsed, setCollapsed] = useState(false);

  if (collapsed) {
    return (
      <aside className='bg-sidebar flex w-9 shrink-0 flex-col items-center border-r py-2'>
        <Button variant='ghost' size='icon' className='size-7' title='Şablon panelini aç' aria-label='Şablon panelini aç' onClick={() => setCollapsed(false)}>
          <PanelLeftOpenIcon />
        </Button>
      </aside>
    );
  }

  return (
    // `min-h-0` ŞART: `flex-col` bir eleman varsayılan `min-height: auto`
    // taşır, yani içeriği ne kadar uzarsa o kadar büyür. Bu olmadan aşağıdaki
    // `ScrollArea`'nın `flex-1`'i asla sınırlı bir yükseklik bulamaz — kaydırma
    // hiç devreye girmez, panel şablon sayısı arttıkça sayfayı taşırarak büyür.
    <aside className='bg-sidebar flex min-h-0 w-56 shrink-0 flex-col border-r'>
      <header className='flex items-start justify-between gap-2 px-3 py-2'>
        <div className='min-w-0'>
          <h2 className='text-sm font-semibold'>Şablonlar</h2>
          <p className='text-muted-foreground text-xs'>{isPending ? '…' : `${data?.length ?? 0} bileşen — canvas'a sürükleyin`}</p>
        </div>
        <Button
          variant='ghost'
          size='icon'
          className='size-6 shrink-0'
          title='Şablon panelini daralt'
          aria-label='Şablon panelini daralt'
          onClick={() => setCollapsed(true)}>
          <PanelLeftCloseIcon />
        </Button>
      </header>

      <ScrollArea className='min-h-0 flex-1'>
        <div className='flex flex-col gap-0.5 p-2'>
          {isPending && Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className='h-11 w-full rounded-md' />)}

          {isError && <p className='text-destructive px-1 text-xs'>{error.message}</p>}

          {data?.length === 0 && <p className='text-muted-foreground px-1 text-xs'>Aktif şablon yok.</p>}

          {data && groupByDeviceType(data).map(([deviceType, templates]) => <DeviceTypeGroup key={deviceType} deviceType={deviceType} templates={templates} />)}
        </div>
      </ScrollArea>

      <AnnotationTools onAdd={onAddAnnotation} />
    </aside>
  );
}

const ANNOTATION_TOOLS: { shape: AnnotationShape; Icon: typeof TypeIcon }[] = [
  { shape: AnnotationShape.Text, Icon: TypeIcon },
  { shape: AnnotationShape.Rectangle, Icon: SquareIcon },
  { shape: AnnotationShape.Note, Icon: StickyNoteIcon },
  { shape: AnnotationShape.Arrow, Icon: MoveRightIcon }
];

/**
 * Not araçları.
 *
 * **Şablonların aksine sürüklenmiyor, tıklanıyor.** Cihazda bırakma noktası
 * anlamlı: hangi şablonun nereye konacağı ayrı iki bilgidir. Notta ise
 * kullanıcı zaten notu yazdıktan sonra yerine taşıyor — sürükleme zorunluluğu
 * fazladan bir adım olurdu. Görünür bir düğme aynı zamanda F2'nin kuralına
 * uyuyor: her eylemin arayüzde bir karşılığı var.
 */
function AnnotationTools({ onAdd }: { onAdd: (shape: AnnotationShape) => void }) {
  return (
    <div className='border-t p-2'>
      <p className='text-muted-foreground px-1 pb-1.5 text-xs font-medium'>Not ekle</p>
      <div className='grid grid-cols-4 gap-1'>
        {ANNOTATION_TOOLS.map(({ shape, Icon }) => (
          <Button
            key={shape}
            size='xs'
            variant='outline'
            aria-label={AnnotationShapeLabels[shape]}
            title={`${AnnotationShapeLabels[shape]} ekle — görünen alanın ortasına`}
            onClick={() => onAdd(shape)}>
            <Icon />
          </Button>
        ))}
      </div>
    </div>
  );
}

/**
 * DeviceType grubu — "tree" görünümünün tek düğümü.
 *
 * Varsayılan KAPALI: 12 grup birden açıkken panel eskisinden farksız uzun bir
 * liste oluyordu, gruplamanın asıl faydası (aradığın tipi bulmak için taraman
 * gereken alanı küçültmek) kayboluyordu. Durum bilerek KONTROLSÜZ
 * (`defaultOpen` verilmiyor, varsayılanı `false`) — 12 grubun her biri için
 * ayrı state tutmak, tek bir kullanım alanı (bu panel) için fazla.
 */
function DeviceTypeGroup({ deviceType, templates }: { deviceType: DeviceType; templates: ComponentTemplatePaletteDto[] }) {
  const Icon = DEVICE_TYPE_ICON[deviceType];

  return (
    <Collapsible>
      {/* `group`: Base UI `data-panel-open`'ı TETİKLEYİCİNİN kendisine yazıyor
          (`data-open`, Collapsible'ın KÖK elemanına yazılan AYRI bir öznitelik —
          ok ikonu tetikleyicinin çocuğu olduğu için o öznitelik ok'a hiç
          ulaşmıyordu, dönme hiç tetiklenmiyordu). */}
      <CollapsibleTrigger className='group hover:bg-accent flex w-full items-center gap-1.5 rounded-md px-1 py-1 text-left'>
        <ChevronRightIcon className='text-muted-foreground size-3.5 shrink-0 transition-transform duration-150 group-data-[panel-open]:rotate-90' />
        <Icon className='text-muted-foreground size-3.5 shrink-0' />
        <span className='flex-1 truncate text-xs font-medium'>{DeviceTypeLabels[deviceType]}</span>
        <span className='text-muted-foreground text-[10px]'>{templates.length}</span>
      </CollapsibleTrigger>
      <CollapsibleContent className='flex flex-col gap-1.5 py-1 pl-4'>
        {templates.map(template => (
          <PaletteCard key={template.id} template={template} />
        ))}
      </CollapsibleContent>
    </Collapsible>
  );
}

function PaletteCard({ template }: { template: ComponentTemplatePaletteDto }) {
  const onDragStart = (event: DragEvent<HTMLDivElement>) => setTemplateDragData(event.dataTransfer, template);
  const Icon = DEVICE_TYPE_ICON[template.deviceTypeId];

  return (
    <div
      draggable
      onDragStart={onDragStart}
      title={`${template.name} — canvas'a sürükleyin`}
      className='hover:bg-accent flex cursor-grab items-center gap-2 rounded-md border p-1.5 transition-colors active:cursor-grabbing'>
      <div
        // Pin sayısı yerine DeviceType ikonu: hangi tür cihaz olduğu bir
        // bakışta anlaşılsın diye — sayı zaten grup başlığında ve şablon
        // adının altındaki etikette (DeviceTypeLabels) mevcut.
        className='grid size-8 shrink-0 place-items-center rounded border'
        style={{
          backgroundColor: safeCssColor(template.backgroundColor),
          color: readableTextColor(template.backgroundColor)
        }}>
        <Icon className='size-4' />
      </div>
      <div className='min-w-0'>
        <p className='truncate text-xs font-medium'>{template.name}</p>
        <p className='text-muted-foreground truncate text-[10px]'>{DeviceTypeLabels[template.deviceTypeId]}</p>
      </div>
    </div>
  );
}
