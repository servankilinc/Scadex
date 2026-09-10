import { useCallback, useMemo, useRef } from 'react';
import { Link, useParams } from 'react-router';
import { ReactFlowProvider, useReactFlow } from '@xyflow/react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { DiagramCanvas } from '@/components/diagram/diagram-canvas';
import { DiagramToolbar } from '@/components/diagram/diagram-toolbar';
import { LiveDisconnectedBanner } from '@/components/diagram/live-indicator';
import { PalettePanel } from '@/components/diagram/palette-panel';
import { PropertiesPanel } from '@/components/diagram/properties-panel';
import { UnsavedChangesBlocker } from '@/components/diagram/unsaved-changes-blocker';
import { useDiagramEditor, type DiagramEditor } from '@/hooks/use-diagram-editor';
import { useDiagramGraph } from '@/hooks/use-diagram-graph';
import { useDiagramLive } from '@/hooks/use-diagram-live';
import { DiagramCanvasProvider } from '@/lib/diagram/canvas-context';
import type { AnnotationShape } from '@/models/enums';
import type { DiagramDto, PointDto } from '@/models/diagram';

/**
 * Diyagram editörü — route: `/cabinets/:cabinetId/diagram`
 *
 * Bu modül `main.tsx`'te lazy yüklenir: `@xyflow/react` ağır bir bağımlılıktır ve
 * editöre hiç girmeyen kullanıcı onu indirmemeli.
 */

// AppLayout'un <main>'i flex kolonu; burada flex-1 ile kalan yuksekligi
// doldurmak, calc(100svh-4rem) gibi sabit bir hesaptan daha saglam: inset
// sidebar'in kenar bosluklari hesaba katilmadigi icin o yol tasma uretiyordu.
// React Flow konteynerinin cozulmus bir yuksekligi olmak ZORUNDA, yoksa 0px
// yuksekliginde render olur ve canvas bos gorunur.
const shellClass = 'flex min-h-0 flex-1 flex-col';

export default function Diagram() {
  const { cabinetId } = useParams<{ cabinetId: string }>();
  const { data, isPending, isError, error } = useDiagramGraph(cabinetId);

  if (isPending) {
    return (
      <div className={shellClass}>
        <div className='flex items-center gap-3 border-b px-3 py-2'>
          <Skeleton className='h-4 w-40' />
        </div>
        <div className='flex flex-1'>
          <Skeleton className='w-56 rounded-none' />
          <Skeleton className='flex-1 rounded-none' />
        </div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className={`${shellClass} items-center justify-center gap-3`}>
        <p className='text-destructive text-sm'>{error.message}</p>
        <Button variant='outline' size='sm' render={<Link to='/cabinets' />}>
          Kabinlere dön
        </Button>
      </div>
    );
  }

  // Editör AYRI bir bileşende: `useDiagramEditor` başlangıç state'ini graftan
  // kurar, dolayısıyla graf gelmeden çağrılamaz — koşullu hook yazılamayacağı
  // için yükleme dalları bu bileşenin dışında kalmak zorunda.
  //
  // `key`: başka bir kabine geçildiğinde editör state'i SIFIRLANMALI. Aynı
  // bileşen örneği kalsaydı, önceki kabinin node'ları ve değişiklik günlüğü
  // yenisinin üstüne taşınırdı.
  return <DiagramEditorShell key={cabinetId} cabinetId={cabinetId!} graph={data} />;
}

function DiagramEditorShell({ cabinetId, graph }: { cabinetId: string; graph: DiagramDto }) {
  const editor = useDiagramEditor(cabinetId, graph);

  // Canlı yayın. Hook telemetriyi harici store'a yazar ve BURAYA HİÇBİR ŞEY
  // döndürmez (durum dışında) — değerleri prop olarak aşağı taşımak, her tick'te
  // tüm ağacı yeniden çizdirirdi. `TemplateNode` onları kendi kimliğiyle okur.
  const hubStatus = useDiagramLive(cabinetId);

  // Node ve edge bileşenlerinin ihtiyaç duyduğu, `data`'ya sığmayan her şey.
  // React Flow onlara yalnızca `data` ulaştırdığı için context kullanılıyor.
  //
  // Geri çağrılar `editor`'dan AYRIŞTIRILIYOR: bağımlılık listesine `editor`'ı
  // yazmak her düzenlemede yeni bir context değeri üretir ve grafın tamamını
  // yeniden çizdirirdi. Bu üç fonksiyonun kimliği `useCallback` ile sabit —
  // aksi halde `useMemo` hiçbir işe yaramazdı.
  const { deleteElements, duplicateDevice, updateConnection } = editor;
  const canvasContext = useMemo(
    () => ({
      cabinetId,
      scadaIsEnabled: graph.cabinet.scadaIsEnabled,
      onDelete: (deviceId: string) => deleteElements([deviceId], []),
      onDuplicate: duplicateDevice,
      onWaypointsChange: (connectionId: string, waypoints: PointDto[]) => updateConnection(connectionId, { waypoints })
    }),
    [cabinetId, graph.cabinet.scadaIsEnabled, deleteElements, duplicateDevice, updateConnection]
  );

  return (
    // ReactFlowProvider en dışta: `DiagramCanvas` bırakma noktasını akış
    // koordinatına çevirmek için `useReactFlow` çağırıyor, provider onun
    // ÜSTÜNDE olmak zorunda.
    <ReactFlowProvider>
      <DiagramCanvasProvider value={canvasContext}>
        <div className={shellClass}>
          <DiagramToolbar cabinet={graph.cabinet} settings={graph.canvasSettings} save={editor.save} isDirty={editor.isDirty} hubStatus={hubStatus} />

          {graph.cabinet.scadaIsEnabled && <LiveDisconnectedBanner status={hubStatus} />}

          <EditorBody editor={editor} graph={graph} />
        </div>

        <UnsavedChangesBlocker isDirty={editor.isDirty} saveNow={editor.save.saveNow} />
      </DiagramCanvasProvider>
    </ReactFlowProvider>
  );
}

/**
 * Palet + canvas + özellikler paneli.
 *
 * AYRI bir bileşen olmasının tek sebebi `useReactFlow`: hook yalnızca
 * `ReactFlowProvider`'ın İÇİNDEN çağrılabilir ve provider'ı render eden
 * `DiagramEditorShell`'in kendi gövdesi hâlâ dışarıdadır. Not ekleme "görünen
 * alanın ortasına" koyduğu için o dönüşüme ihtiyaç duyuyor.
 */
function EditorBody({ editor, graph }: { editor: DiagramEditor; graph: DiagramDto }) {
  const { screenToFlowPosition } = useReactFlow();
  const canvasRef = useRef<HTMLDivElement>(null);
  const { addAnnotation } = editor;

  const addAnnotationAtCenter = useCallback(
    (shape: AnnotationShape) => {
      const rect = canvasRef.current?.getBoundingClientRect();
      // Ölçü henüz yoksa akış başlangıcına düş: notu hiç eklememektense
      // bulunabilir bir yere koymak daha iyi.
      const center = rect
        ? screenToFlowPosition({ x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 })
        : { x: 0, y: 0 };

      addAnnotation(shape, { x: Math.round(center.x), y: Math.round(center.y) });
    },
    [addAnnotation, screenToFlowPosition]
  );

  return (
    <div className='flex min-h-0 flex-1'>
      <PalettePanel onAddAnnotation={addAnnotationAtCenter} />
      <div ref={canvasRef} className='min-w-0 flex-1'>
        <DiagramCanvas editor={editor} settings={graph.canvasSettings} devices={graph.devices} />
      </div>
      <PropertiesPanel editor={editor} />
    </div>
  );
}
