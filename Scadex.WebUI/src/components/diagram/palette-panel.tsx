import type { DragEvent } from 'react';
import { MoveRightIcon, SquareIcon, StickyNoteIcon, TypeIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Skeleton } from '@/components/ui/skeleton';
import { AnnotationShape, AnnotationShapeLabels, DeviceTypeLabels } from '@/models/enums';
import type { ComponentTemplatePaletteDto } from '@/models/componentTemplate';
import { readableTextColor, safeCssColor } from '@/lib/diagram/colors';
import { setTemplateDragData } from '@/lib/diagram/dnd';
import { useComponentTemplatePalette } from '@/hooks/use-component-templates';

/**
 * Stencil kütüphanesi + not araçları.
 *
 * Şablon kartları canvas'a sürüklenir; bırakılan şablondan sunucu cihazı ve
 * pinlerini TEK transaction'da üretir. Tıklamak bir şey yapmaz — bırakma noktası
 * olmadan cihazın nereye konacağı belirsiz olurdu.
 */
export function PalettePanel({ onAddAnnotation }: { onAddAnnotation: (shape: AnnotationShape) => void }) {
  const { data, isPending, isError, error } = useComponentTemplatePalette();

  return (
    <aside className='bg-sidebar flex w-56 shrink-0 flex-col border-r'>
      <header className='px-3 py-2'>
        <h2 className='text-sm font-semibold'>Şablonlar</h2>
        <p className='text-muted-foreground text-xs'>{isPending ? '…' : `${data?.length ?? 0} bileşen — canvas'a sürükleyin`}</p>
      </header>

      <ScrollArea className='flex-1'>
        <div className='flex flex-col gap-1.5 p-2'>
          {isPending && Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className='h-11 w-full rounded-md' />)}

          {isError && <p className='text-destructive px-1 text-xs'>{error.message}</p>}

          {data?.length === 0 && <p className='text-muted-foreground px-1 text-xs'>Aktif şablon yok.</p>}

          {data?.map(template => <PaletteCard key={template.id} template={template} />)}
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

function PaletteCard({ template }: { template: ComponentTemplatePaletteDto }) {
  const onDragStart = (event: DragEvent<HTMLDivElement>) => setTemplateDragData(event.dataTransfer, template);

  return (
    <div
      draggable
      onDragStart={onDragStart}
      title={`${template.name} — canvas'a sürükleyin`}
      className='hover:bg-accent flex cursor-grab items-center gap-2 rounded-md border p-1.5 transition-colors active:cursor-grabbing'>
      <div
        // Kartta sablonun gercek en-boy oranini gostermek, palet ile canvas
        // arasindaki zihinsel esleşmeyi kuruyor.
        className='grid size-8 shrink-0 place-items-center rounded border text-[9px] font-semibold'
        style={{
          backgroundColor: safeCssColor(template.backgroundColor),
          color: readableTextColor(template.backgroundColor)
        }}>
        {template.pins.length}
      </div>
      <div className='min-w-0'>
        <p className='truncate text-xs font-medium'>{template.name}</p>
        <p className='text-muted-foreground truncate text-[10px]'>{DeviceTypeLabels[template.deviceTypeId]}</p>
      </div>
    </div>
  );
}
