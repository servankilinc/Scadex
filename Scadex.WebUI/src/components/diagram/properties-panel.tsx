import { EyeIcon, LockOpenIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { useAppSelector } from '@/hooks';
import type { DiagramEditor } from '@/hooks/use-diagram-editor';
import { MIN_ALIGN } from '@/lib/diagram/align';
import { AlignToolbar } from './align-toolbar';
import { AnnotationForm } from './annotation-form';
import { DeleteButton } from './confirm-delete';
import { ConnectionForm } from './connection-form';
import { DeviceForm } from './device-form';

/**
 * Seçili öğenin, canvas üzerinde düzenlenemeyen alanları — ve seçime uygulanan eylemler. Formlar öğe tipine göre ayrı
 * dosyalardadır: `device-form.tsx` (kendi Kaydet'i var), `connection-form.tsx` ve `annotation-form.tsx` (anında yazar);
 * ortak girdiler `panel-fields.tsx`'te.
 *
 * **Her eylemin görünür bir karşılığı var.** Silme, kopyalama, hizalama ve konum girişi buradan yapılabiliyor; klavye
 * hiçbirinin TEK yolu değil — arayüzden yönetilebilen bir editör, ezberlenmesi gereken bir kısayol listesinden iyidir.
 *
 * Seçim Redux'ta durur (`diagramSlice`), veri React Flow state'inde. Panel ikisini birleştirir; yazma her zaman `editor`
 * üzerinden gider.
 *
 * `readOnly` (`EditDiagram` izni yok): alanlar yerel `fieldset disabled` ile kilitlenir, okuma ve kumanda geçmişi kalır.
 * Çağıran ayrıca yazan fonksiyonları etkisiz bir editör verir (bkz. `views/app/diagram`).
 */
export function PropertiesPanel({ editor, readOnly = false }: { editor: DiagramEditor; readOnly?: boolean }) {
  const selectedNodeIds = useAppSelector(s => s.diagram.selectedNodeIds);
  const selectedEdgeIds = useAppSelector(s => s.diagram.selectedEdgeIds);

  const total = selectedNodeIds.length + selectedEdgeIds.length;

  return (
    <aside className='bg-sidebar flex w-72 shrink-0 flex-col border-l'>
      <header className='border-b px-3 py-2'>
        <h2 className='text-sm font-semibold'>Özellikler</h2>
        {readOnly && <p className='text-muted-foreground text-xs'>Salt okunur — diyagramı düzenleme yetkiniz yok.</p>}
      </header>

      <ScrollArea className='flex-1'>
        <fieldset disabled={readOnly} className='min-w-0 p-3'>
          {renderBody()}
        </fieldset>
      </ScrollArea>
    </aside>
  );

  function renderBody() {
    if (total === 0) {
      return (
        <>
          <p className='text-muted-foreground text-xs'>Bir cihaz, kablo veya not seçin.</p>
          <UnreachableItems editor={editor} />
        </>
      );
    }

    // Çoklu seçimde alan düzenlemesi YOK: "20 cihazın adını birden değiştir"in
    // anlamlı bir karşılığı yok. Toplu olarak anlamlı olan iki şey — hizalama ve
    // silme — burada.
    if (total > 1) {
      return <MultiSelectionForm editor={editor} nodeIds={selectedNodeIds} edgeIds={selectedEdgeIds} />;
    }

    const edgeId = selectedEdgeIds[0];
    if (edgeId) {
      const edge = editor.edges.find(e => e.id === edgeId);
      if (!edge?.data) return null;
      return (
        <ConnectionForm
          key={edge.id}
          connection={edge.data.connection}
          onChange={patch => editor.updateConnection(edge.id, patch)}
          onDelete={() => editor.deleteElements([], [edge.id])}
        />
      );
    }

    const nodeId = selectedNodeIds[0];
    const node = editor.nodes.find(n => n.id === nodeId);
    if (!node) return null;

    if (node.type === 'annotation') return <AnnotationForm key={node.id} node={node} editor={editor} />;

    return <DeviceForm key={node.id} node={node} editor={editor} readOnly={readOnly} />;
  }
}

// ──────────────────────────────────────────────────────────── çoklu seçim

function MultiSelectionForm({ editor, nodeIds, edgeIds }: { editor: DiagramEditor; nodeIds: string[]; edgeIds: string[] }) {
  const selectedNodes = editor.nodes.filter(node => nodeIds.includes(node.id));
  const total = nodeIds.length + edgeIds.length;

  return (
    <div className='flex flex-col gap-3'>
      <p className='text-muted-foreground text-xs'>
        {total} öğe seçili.
        {nodeIds.length > 1 && ' Sürükleyerek hepsini birlikte taşıyabilirsiniz.'}
      </p>

      {selectedNodes.length >= MIN_ALIGN && (
        <div className='border-t pt-3'>
          <AlignToolbar nodes={selectedNodes} onMove={editor.moveNodes} />
        </div>
      )}

      <div className='border-t pt-3'>
        <DeleteButton
          label={`${total} öğeyi sil`}
          title='Seçilenleri sil'
          summary={<>{describeSelection(nodeIds.length, edgeIds.length)} silinecek. Cihazlara bağlı kablolar da silinir ve geri alınamaz.</>}
          onConfirm={() => editor.deleteElements(nodeIds, edgeIds)}
        />
      </div>
    </div>
  );
}

/** "3 cihaz ve 2 kablo" — sayılar 0 ise o parça hiç yazılmaz. */
function describeSelection(nodeCount: number, edgeCount: number): string {
  const parts: string[] = [];
  if (nodeCount > 0) parts.push(`${nodeCount} öğe`);
  if (edgeCount > 0) parts.push(`${edgeCount} kablo`);
  return parts.join(' ve ');
}

/**
 * Canvas'tan seçilemez hâle gelmiş öğeler — gizlemenin ve kilitlemenin çıkış yolu.
 *
 * Bu liste olmadan ikisi de TEK YÖNLÜ kapı olurdu:
 *
 * - **Gizli** node RF'te `hidden` olur, tıklanamaz, panele bir daha getirilemez.
 * - **Kilitli NOT** ayrıca `selectable: false` alıyor (bkz. `to-rf-nodes.ts`) —
 *   bu bilinçli: arka planda duran büyük bir çerçeve kilitlendiğinde tıklamalar
 *   altındaki cihaza geçmeli. Ama yan etkisi, o notu bir daha seçememek.
 *
 * Cihazlar kilitliyken seçilebilir kalıyor (yalnızca sürüklenemiyorlar), o
 * yüzden burada yalnızca gizli olanları listeleniyorlar.
 */
interface UnreachableItem {
  id: string;
  name: string;
  reason: 'gizli' | 'kilitli';
  kind: 'annotation' | 'device';
}

function UnreachableItems({ editor }: { editor: DiagramEditor }) {
  // Geri dönüş tipi AÇIKÇA yazılıyor: yazılmazsa çıkarım ilk dalın literal
  // tipine kilitleniyor ve "kilitli" dalı ona uymuyor.
  const items: UnreachableItem[] = editor.nodes.flatMap((node): UnreachableItem[] => {
    if (node.type === 'annotation') {
      const { annotation } = node.data;
      if (!annotation.isVisible) return [{ id: node.id, name: annotation.name, reason: 'gizli', kind: 'annotation' }];
      if (annotation.isLocked) return [{ id: node.id, name: annotation.name, reason: 'kilitli', kind: 'annotation' }];
      return [];
    }
    const { device } = node.data;
    return device.isVisible ? [] : [{ id: node.id, name: device.name, reason: 'gizli', kind: 'device' }];
  });

  if (items.length === 0) return null;

  return (
    <div className='mt-3 flex flex-col gap-1.5 border-t pt-3'>
      <p className='text-muted-foreground text-xs font-medium'>Seçilemeyen öğeler ({items.length})</p>
      <p className='text-muted-foreground text-[10px]'>Gizli ve kilitli öğeler canvas'ta tıklanamaz; buradan geri getirin.</p>

      {items.map(item => (
        <div key={item.id} className='flex items-center justify-between gap-2 text-xs'>
          <span className='truncate'>
            {item.name} <span className='text-muted-foreground'>· {item.reason}</span>
          </span>
          <Button
            size='xs'
            variant='outline'
            onClick={() => {
              const patch = item.reason === 'gizli' ? { isVisible: true } : { isLocked: false };
              if (item.kind === 'annotation') editor.updateAnnotation(item.id, patch);
              else editor.updateDeviceLayout(item.id, patch);
            }}>
            {item.reason === 'gizli' ? <EyeIcon /> : <LockOpenIcon />}
            {item.reason === 'gizli' ? 'Göster' : 'Kilidi aç'}
          </Button>
        </div>
      ))}
    </div>
  );
}
