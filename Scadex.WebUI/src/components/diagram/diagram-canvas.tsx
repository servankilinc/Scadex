import { useCallback, type DragEvent } from 'react';
import {
  Background,
  BackgroundVariant as RfBackgroundVariant,
  ConnectionMode,
  Controls,
  MiniMap,
  ReactFlow,
  SelectionMode,
  useReactFlow,
  type EdgeTypes,
  type NodeTypes
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { BackgroundVariant } from '@/models/enums';
import type { DiagramCanvasSettingsDto, DiagramDto } from '@/models/diagram';
import { useAppSelector } from '@/hooks';
import type { DiagramEditor } from '@/hooks/use-diagram-editor';
import { safeCssColor } from '@/lib/diagram/colors';
import { readTemplateDragData } from '@/lib/diagram/dnd';
import { AnnotationNode } from './annotation-node';
import { OrthogonalEdge } from './orthogonal-edge';
import { TemplateNode } from './template-node';

/**
 * MODÜL SEVİYESİNDE tanımlanmak ZORUNDA. Her render'da yeni bir nesne verilirse
 * React Flow tüm node'ları remount eder: seçim kaybolur, animasyonlar sıfırlanır
 * ve büyük graflarda gözle görülür bir donma olur.
 */
const nodeTypes: NodeTypes = {
  template: TemplateNode,
  annotation: AnnotationNode
};

const edgeTypes: EdgeTypes = {
  orthogonal: OrthogonalEdge
};

/** Bizim `BackgroundVariant` → React Flow'unki. `None` desen çizdirmez. */
const VARIANT_MAP: Record<BackgroundVariant, RfBackgroundVariant | null> = {
  [BackgroundVariant.None]: null,
  [BackgroundVariant.Dots]: RfBackgroundVariant.Dots,
  [BackgroundVariant.Lines]: RfBackgroundVariant.Lines,
  [BackgroundVariant.Cross]: RfBackgroundVariant.Cross
};

interface DiagramCanvasProps {
  editor: DiagramEditor;
  settings: DiagramCanvasSettingsDto;
  /** Yalnızca minimap renkleri için: cihazlar kendi şablon renginde görünsün. */
  devices: DiagramDto['devices'];
}

export function DiagramCanvas({ editor, settings, devices }: DiagramCanvasProps) {
  // RF kendi tema sinifini kendi yonetir; uygulamanin temasiyla ayni kaynaktan
  // beslenmezse kontrol butonlari acik temada beyaz uzerine beyaz kalir.
  const theme = useAppSelector(s => s.theme.activeTheme);
  const mode = useAppSelector(s => s.diagram.mode);
  const { screenToFlowPosition } = useReactFlow();

  const variant = VARIANT_MAP[settings.backgroundVariant];

  const onDragOver = useCallback((event: DragEvent) => {
    // preventDefault ÇAĞRILMAZSA tarayıcı drop'u hiç tetiklemez.
    event.preventDefault();
    event.dataTransfer.dropEffect = 'copy';
  }, []);

  const onDrop = useCallback(
    (event: DragEvent) => {
      event.preventDefault();
      const template = readTemplateDragData(event.dataTransfer);
      if (!template) return;

      // Ekran koordinatı akış koordinatına çevrilir; yoksa zoom/pan uygulanmış
      // canvas'ta cihaz imlecin çok uzağına düşer.
      const position = screenToFlowPosition({ x: event.clientX, y: event.clientY });

      // Bırakma noktası kutunun ORTASI olsun: kullanıcı imleci nereye getirdiyse
      // cihazın oraya oturmasını bekler, sol üst köşesinin değil.
      editor.addDeviceFromTemplate(template, {
        x: position.x - template.width / 2,
        y: position.y - template.height / 2
      });
    },
    [editor, screenToFlowPosition]
  );

  return (
    <ReactFlow
      nodes={editor.nodes}
      edges={editor.edges}
      nodeTypes={nodeTypes}
      edgeTypes={edgeTypes}
      onNodesChange={editor.onNodesChange}
      onEdgesChange={editor.onEdgesChange}
      onNodeDragStop={editor.onNodeDragStop}
      onConnect={editor.onConnect}
      isValidConnection={editor.isValidConnection}
      onSelectionChange={editor.onSelectionChange}
      onDragOver={onDragOver}
      onDrop={onDrop}
      // Loose: baglanti herhangi bir handle'da baslayip bitebilir. Her pin
      // type="source" render edildigi icin sart — bkz. template-node.tsx.
      connectionMode={ConnectionMode.Loose}
      colorMode={theme}
      nodesDraggable
      nodesConnectable
      elementsSelectable
      // "Seç" modunda boş alanı sürüklemek KUTU SEÇİMİ yapar. RF'in varsayılanı
      // kaydırmaktır ve çoklu seçim ancak Shift ile olur; bu, çoklu seçimi (ve
      // ona bağlı hizalamayı) klavyeye mahkûm ederdi.
      selectionOnDrag={mode === 'select'}
      // Seç modunda ORTA tuş kaydırır: mod değiştirmeden hızlıca konumlanmanın
      // yolu açık kalsın. Sağ tuş burada YOK — o, node menüsünün.
      panOnDrag={mode === 'pan' ? true : [1]}
      // `Partial`: kutuya DOKUNAN öğe seçilir, tamamen içine almak gerekmez.
      // Tam kapsama, kablosu uzun bir cihazı seçmek için ekranın yarısını
      // taramayı gerektirirdi.
      selectionMode={SelectionMode.Partial}
      // RF, input/textarea içindeyken tuş olaylarını yok sayar; bu yüzden
      // Backspace'i de kabul etmek metin yazarken node silmez. Bu kısayol tek
      // silme yolu DEĞİL — aynı iş özellikler panelinden ve sağ tık menüsünden
      // de yapılabiliyor (bkz. `confirm-delete.tsx`).
      deleteKeyCode={['Delete', 'Backspace']}
      minZoom={settings.minZoom}
      maxZoom={settings.maxZoom}
      snapToGrid={settings.snapToGrid}
      snapGrid={[settings.gridSize, settings.gridSize]}
      fitView
      proOptions={{ hideAttribution: false }}
      style={{ backgroundColor: settings.backgroundColor }}>
      {variant && <Background variant={variant} gap={settings.gridSize} color={settings.gridColor} />}
      <Controls showInteractive={false} />
      <MiniMap pannable zoomable nodeColor={node => (node.type === 'annotation' ? '#cbd5e1' : miniMapColor(node.id, devices))} />
    </ReactFlow>
  );
}

/** Minimap'te cihazlar kendi şablon renkleriyle görünsün — konumu tanımayı kolaylaştırır. */
function miniMapColor(nodeId: string, devices: DiagramDto['devices']): string {
  const device = devices.find(d => d.id === nodeId);
  return device ? safeCssColor(device.template.backgroundColor) : '#94a3b8';
}
