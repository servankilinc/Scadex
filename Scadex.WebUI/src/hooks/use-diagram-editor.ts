import { useCallback, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { applyEdgeChanges, applyNodeChanges, type Connection, type Edge, type EdgeChange, type NodeChange, type XYPosition } from '@xyflow/react';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import * as diagramApi from '@/api/diagram';
import { diagramKeys } from '@/api/query-keys';
import { useAppDispatch } from '@/hooks';
import { toApiError } from '@/lib/axios-helper';
import { ConnectionRejectionMessages, buildConnectionContext, validateConnection } from '@/lib/diagram/connection-rules';
import {
  savedDeviceProperties,
  toAnnotationCreateRequest,
  type DeviceProperties,
  toAnnotationLayoutItem,
  toAnnotationUpdateRequest,
  toConnectionCreateRequest,
  toConnectionUpdateRequest,
  toDeviceLayoutItem,
  toDeviceUpdateRequest,
  touchesContent,
  touchesLayout
} from '@/lib/diagram/diagram-requests';
import { cascadePosition, newAnnotationDraft } from '@/lib/diagram/annotation-defaults';
import { toRfEdge, type DiagramEdge } from '@/lib/diagram/to-rf-edges';
import { deviceSize, toAnnotationNode, toDeviceNode, toRfNodes, type AnnotationNode, type DeviceNode, type DiagramNode } from '@/lib/diagram/to-rf-nodes';
import { toRfEdges } from '@/lib/diagram/to-rf-edges';
import { AnnotationShape, CameraBrand, DeviceType, EdgeRouting, LineStyle, WireType } from '@/models/enums';
import { newId } from '@/lib/sequential-id';
import type { ComponentTemplatePaletteDto } from '@/models/componentTemplate';
import type {
  DeviceCreateRequest,
  DiagramAnnotationItemDto,
  DiagramConnectionDto,
  DiagramDeviceDto,
  DiagramDto
} from '@/models/diagram';
import { setSelection } from '@/store/reducers/diagramSlice';

/**
 * Diyagram editörünün durumu.
 *
 * **Her eylem anında kendi ucuna gider (2026-10-27).** "Kaydet" düğmesi, değişiklik günlüğü ve "kaydedilmemiş kayıt"
 * defteri yok: cihaz bırakılınca sunucu onu pinleriyle oluşturup döndürür, kablo yalnızca kalıcı pinlere çizilir,
 * sürükleme bitince yerleşim yazılır. Tek istisna cihazın özellik formudur (alanlar arası kurallar — izleme için IP,
 * kamera IP'si — yüzünden kendi Kaydet'i var, `device-form.tsx`).
 *
 * **İki ayrı kaynak, bilerek.** Sunucudan gelen graf `useDiagramGraph`'ta DEĞİŞMEZ olarak durur; düzenlenen hâli burada
 * React Flow state'inde yaşar. Tuval iyimser güncellenir; bir istek başarısız olursa toast + graf yeniden çekilir ve
 * tuval sunucu gerçeğine döner — elle geri alma kodu yazılmaz.
 *
 * **İstekler SIRAYLA gider** (tek kuyruk). Paralel gitselerdi "notu oluştur" ile hemen ardından gelen "notu taşı" ters
 * sırayla varabilir ve ikincisi 400 alırdı.
 */

/** Yeni çizilen kablonun varsayılan görünümü. Nötr bir ton: tip henüz seçilmedi. */
const DEFAULT_WIRE_COLOR = '#64748B';
const DEFAULT_WIRE_WIDTH = 1.5;

/**
 * Kopya kaynağın tam üstüne değil, hafifçe kaydırılmış doğar — üst üste binen
 * iki kutu, kullanıcıya kopyalamanın işe yarayıp yaramadığını göstermez.
 */
const DUPLICATE_OFFSET = 24;

/** Tuvalde/panelde anında yazılan cihaz alanları (`PUT …/layout`). */
export type DeviceLayoutPatch = Partial<Pick<DiagramDeviceDto, 'width' | 'height' | 'rotation' | 'zIndex' | 'isLocked' | 'isVisible'>>;

/** Araç çubuğundaki istek durumu rozeti için. */
export interface DiagramActivity {
  /** Kuyruktaki ya da uçuştaki istek sayısı. */
  pending: number;
  /** Son başarılı isteğin anı — istemcinin kendi saatinden (rozet "ben bunu saat kaçta yaptım"ı gösterir). */
  lastSavedAt: string | null;
  error: string | null;
}

export interface DiagramEditor {
  nodes: DiagramNode[];
  edges: DiagramEdge[];
  onNodesChange: (changes: NodeChange<DiagramNode>[]) => void;
  onEdgesChange: (changes: EdgeChange<DiagramEdge>[]) => void;
  onNodeDragStop: (event: unknown, node: DiagramNode, nodes: DiagramNode[]) => void;
  onConnect: (connection: Connection) => void;
  isValidConnection: (connection: Connection | Edge) => boolean;
  onSelectionChange: (params: { nodes: DiagramNode[]; edges: DiagramEdge[] }) => void;
  /** React Flow'un klavye silmesi (`Delete` / `Backspace`): tuvalden kaldırmayı RF yapar, burada sunucuya gider. */
  onDelete: (params: { nodes: DiagramNode[]; edges: DiagramEdge[] }) => void;
  addDeviceFromTemplate: (template: ComponentTemplatePaletteDto, position: XYPosition) => void;
  /** Cihazı kopyalar: kaynaktan bir oluşturma isteği kurulur (IP'siz). */
  duplicateDevice: (id: string) => void;
  /**
   * Oluşturulmayı bekleyen cihaz — zorunlu alanları (kamerada IP + marka) olduğu için önce pencerede tamamlanır
   * (`create-device-dialog.tsx`). Diğer tipler beklemeden oluşturulur.
   */
  pendingCreate: DeviceCreateRequest | null;
  submitPendingCreate: (request: DeviceCreateRequest) => Promise<boolean>;
  cancelPendingCreate: () => void;
  /** Cihaz formunun Kaydet'i. Başarıda tuvaldeki cihaz güncellenir; başarısızlıkta form taslağı korunur. */
  saveDevice: (id: string, properties: DeviceProperties) => Promise<boolean>;
  updateDeviceLayout: (id: string, patch: DeviceLayoutPatch) => void;
  /** Kablonun görünümü ve kırılma noktaları — anında yazılır. */
  updateConnection: (id: string, patch: Partial<DiagramConnectionDto>) => void;
  /** Silmenin ARAYÜZ yolu (panel, sağ tık menüsü). Cihaza bağlı kablolar da kalkar. */
  deleteElements: (nodeIds: string[], edgeIds: string[]) => void;
  /** Hizalama, dağıtma ve konum alanlarının ortak yazma yolu. */
  moveNodes: (positions: Record<string, XYPosition>) => void;
  /** Palet'teki not araçlarının çağırdığı yer. */
  addAnnotation: (shape: AnnotationShape, position: XYPosition) => void;
  /** Notun içerik ve yerleşim alanları — dokunulan alana göre ilgili uca anında yazılır. */
  updateAnnotation: (id: string, patch: Partial<DiagramAnnotationItemDto>) => void;
  activity: DiagramActivity;
}

export function useDiagramEditor(cabinetId: string, graph: DiagramDto): DiagramEditor {
  const dispatch = useAppDispatch();
  const queryClient = useQueryClient();

  const [nodes, setNodes] = useState<DiagramNode[]>(() => toRfNodes(graph));
  const [edges, setEdges] = useState<DiagramEdge[]>(() => toRfEdges(graph));
  const [pendingCreate, setPendingCreate] = useState<DeviceCreateRequest | null>(null);
  const [activity, setActivity] = useState<DiagramActivity>({ pending: 0, lastSavedAt: null, error: null });

  // Geri çağrılar render dışında çalışır; state'i doğrudan okuyamazlar. Aynalama render sırasında değil
  // `useLayoutEffect`'te: render sırasında ref yazmak React'in kurallarına aykırı.
  const nodesRef = useRef(nodes);
  const edgesRef = useRef(edges);
  useLayoutEffect(() => {
    nodesRef.current = nodes;
    edgesRef.current = edges;
  });

  // ------------------------------------------------------------- istek kuyruğu

  const queueRef = useRef<Promise<unknown>>(Promise.resolve());

  /**
   * İsteği kuyruğa koyar. Başarısızlıkta toast gösterir ve `null` döner; `optimistic` istekte tuval zaten değiştiği için
   * graf yeniden çekilir (sunucu gerçeğine dönüş). Başarıda cache yalnızca BAYAT işaretlenir (`refetchType: 'none'`):
   * tuval zaten güncel, editöre bir sonraki girişte taze veri gelir.
   */
  const send = useCallback(
    <T>(request: () => Promise<T>, { optimistic }: { optimistic: boolean }): Promise<T | null> => {
      const queryKey = diagramKeys.cabinet(cabinetId);
      setActivity(current => ({ ...current, pending: current.pending + 1 }));

      const task = queueRef.current.then(request).then(
        result => {
          setActivity(current => ({ pending: current.pending - 1, lastSavedAt: new Date().toISOString(), error: null }));
          void queryClient.invalidateQueries({ queryKey, refetchType: 'none' });
          return result;
        },
        (error: unknown) => {
          const message = toApiError(error).message;
          toast.error(message);
          setActivity(current => ({ ...current, pending: current.pending - 1, error: message }));
          if (optimistic) void queryClient.invalidateQueries({ queryKey });
          return null;
        }
      );
      queueRef.current = task;
      return task;
    },
    [cabinetId, queryClient]
  );

  const sendLayout = useCallback(
    (changed: DiagramNode[]) => {
      const devices = changed.filter(isDeviceNode).map(node => toDeviceLayoutItem(node.data.device));
      const annotations = changed.filter(isAnnotationNode).map(node => toAnnotationLayoutItem(node.data.annotation));
      if (devices.length === 0 && annotations.length === 0) return;
      void send(() => diagramApi.updateLayout(cabinetId, { devices, annotations }), { optimistic: true });
    },
    [cabinetId, send]
  );

  const sendDelete = useCallback(
    (doomedNodes: DiagramNode[], connectionIds: string[]) => {
      const deviceIds = doomedNodes.filter(isDeviceNode).map(node => node.id);
      const annotationIds = doomedNodes.filter(isAnnotationNode).map(node => node.id);
      if (deviceIds.length === 0 && annotationIds.length === 0 && connectionIds.length === 0) return;
      void send(() => diagramApi.deleteElements(cabinetId, { deviceIds, connectionIds, annotationIds }), { optimistic: true });
    },
    [cabinetId, send]
  );

  // ------------------------------------------------- sunucu grafıyla eşitleme

  const [syncedGraph, setSyncedGraph] = useState(graph);
  // Render sırasında state ayarlamak, prop değişimini yansıtmanın React'in önerdiği yolu. Uçuşta istek varken gelen graf
  // o isteğin sonucunu henüz içermeyebilir; kuyruk boşalınca bir sonraki çekimde eşitlenir.
  if (syncedGraph !== graph && activity.pending === 0) {
    setSyncedGraph(graph);
    setNodes(toRfNodes(graph));
    setEdges(toRfEdges(graph));
  }

  // ------------------------------------------------------------- düzenlemeler

  // Konum değişiklikleri sürükleme boyunca saniyede onlarca kez gelir; yalnızca yerelde uygulanır, sunucuya
  // `onNodeDragStop`'ta gider. Klavye silmesinin `remove`'u da burada yalnızca tuvalden kaldırır — istek `onDelete`'tedir.
  const onNodesChange = useCallback((changes: NodeChange<DiagramNode>[]) => {
    setNodes(current => applyNodeChanges(changes, current));
  }, []);

  const onEdgesChange = useCallback((changes: EdgeChange<DiagramEdge>[]) => {
    setEdges(current => applyEdgeChanges(changes, current));
  }, []);

  const onDelete = useCallback(
    ({ nodes: doomedNodes, edges: doomedEdges }: { nodes: DiagramNode[]; edges: DiagramEdge[] }) => {
      sendDelete(
        doomedNodes,
        doomedEdges.map(edge => edge.id)
      );
    },
    [sendDelete]
  );

  const onNodeDragStop = useCallback(
    (_event: unknown, node: DiagramNode, draggedNodes: DiagramNode[]) => {
      // Çoklu seçim sürüklendiğinde React Flow üçüncü parametrede hepsini verir;
      // yalnızca `node`'u işlemek diğerlerinin yeni konumunu kaybettirirdi.
      const moved = (draggedNodes.length > 0 ? draggedNodes : [node]).map(item => repositioned(item, item.position));
      const movedById = new Map(moved.map(item => [item.id, item]));

      // Taşınan node'un DTO'sundaki koordinatı da tazele: istek DTO'yu okuyor, özellikler paneli de.
      setNodes(current => current.map(item => (movedById.has(item.id) ? repositioned(item, item.position) : item)));
      sendLayout(moved);
    },
    [sendLayout]
  );

  const connectionContext = useMemo(() => buildConnectionContext(nodes, edges), [nodes, edges]);

  const isValidConnection = useCallback(
    (connection: Connection | Edge) => validateConnection(connection.sourceHandle, connection.targetHandle, connectionContext) === null,
    [connectionContext]
  );

  const onConnect = useCallback(
    (connection: Connection) => {
      const rejection = validateConnection(connection.sourceHandle, connection.targetHandle, connectionContext);
      if (rejection) {
        toast.error(ConnectionRejectionMessages[rejection]);
        return;
      }

      const draft: DiagramConnectionDto = {
        id: newId(),
        cabinetId,
        sourcePinId: connection.sourceHandle!,
        targetPinId: connection.targetHandle!,
        sourceDeviceId: connection.source,
        targetDeviceId: connection.target,
        label: null,
        wireType: WireType.Signal,
        color: DEFAULT_WIRE_COLOR,
        lineStyle: LineStyle.Solid,
        strokeWidth: DEFAULT_WIRE_WIDTH,
        routing: EdgeRouting.Orthogonal,
        waypoints: [],
        zIndex: 0
      };

      setEdges(current => [...current, toRfEdge(draft)]);
      void send(() => diagramApi.createConnection(cabinetId, toConnectionCreateRequest(draft)), { optimistic: true });
    },
    [cabinetId, connectionContext, send]
  );

  /** Cihazı oluşturur ve sunucunun döndürdüğü hâliyle (pinler, kanallar) tuvale ekler. İyimser DEĞİL: pinler yanıtta gelir. */
  const createDevice = useCallback(
    async (request: DeviceCreateRequest): Promise<boolean> => {
      const created = await send(() => diagramApi.createDevice(cabinetId, request), { optimistic: false });
      if (!created) return false;
      setNodes(current => [...current, toDeviceNode(created)]);
      return true;
    },
    [cabinetId, send]
  );

  /** Kamera zorunlu alanları (IP + marka) pencereden ister; diğer tipler beklemeden oluşturulur. */
  const requestCreate = useCallback(
    (request: DeviceCreateRequest, deviceTypeId: DeviceType) => {
      if (deviceTypeId === DeviceType.Camera) setPendingCreate(request);
      else void createDevice(request);
    },
    [createDevice]
  );

  const addDeviceFromTemplate = useCallback(
    (template: ComponentTemplatePaletteDto, position: XYPosition) => {
      requestCreate(
        {
          id: newId(),
          componentTemplateId: template.id,
          name: nextDeviceName(template.name, nodesRef.current),
          coordinateX: position.x,
          coordinateY: position.y,
          // Şablon ölçüsü BURAYA KOPYALANMAZ: null = "şablonu takip et"; kopyalamak override'ı hemen materyalize ederdi.
          width: null,
          height: null,
          rotation: 0,
          zIndex: nextZIndex(nodesRef.current),
          isLocked: false,
          isVisible: true,
          ipAddress: null,
          // İzleme varsayılan KAPALI: hangi cihazın yoklanacağına operatör karar verir.
          monitoringPort: null,
          pingIntervalSec: 300,
          isMonitoringEnabled: false,
          // Tipine göre boş ek nesne: sunucu tipiyle eşleşen nesneyi zorunlu tutar. Kart MAC'siz, PC kimliksiz doğar (panelden girilir).
          camera:
            template.deviceTypeId === DeviceType.Camera
              ? { brand: CameraBrand.Hikvision, model: null, description: null, username: null, password: null }
              : null,
          controlCard: template.deviceTypeId === DeviceType.ControlModule ? { macAddress: null } : null,
          pc: template.deviceTypeId === DeviceType.Pc ? { secret: null } : null
        },
        template.deviceTypeId
      );
    },
    [requestCreate]
  );

  const duplicateDevice = useCallback(
    (id: string) => {
      const node = nodesRef.current.find(n => n.id === id);
      if (node?.type !== 'template') return;

      const source = node.data.device;
      requestCreate(
        {
          id: newId(),
          componentTemplateId: source.componentTemplateId,
          // Ad KAYNAKTAN türetiliyor: kullanıcı cihaza "PSU-Ana" dediyse kopyanın "Güç Kaynağı 4" olması bağlamı kaybettirir.
          name: nextDeviceName(source.name, nodesRef.current),
          coordinateX: node.position.x + DUPLICATE_OFFSET,
          coordinateY: node.position.y + DUPLICATE_OFFSET,
          // Boyut override'ı KOPYALANIR: 300×80'e getirilmiş bir cihazın kopyasının 120×60 doğmasını kimse beklemez.
          width: source.width,
          height: source.height,
          rotation: source.rotation,
          zIndex: nextZIndex(nodesRef.current),
          isLocked: false,
          isVisible: true,
          // IP KOPYALANMAZ (aynı adreste iki cihaz olmaz); bu yüzden izleme de kapalı doğar — açık kalsaydı sunucu
          // "izleme açıkken IP zorunlu" ile reddederdi. Port ve periyot gelir: IP girilip izleme açılınca hazırdır.
          ipAddress: null,
          monitoringPort: source.monitoringPort,
          pingIntervalSec: source.pingIntervalSec,
          isMonitoringEnabled: false,
          // Parola ve sır KOPYALANAMAZ (sunucu onları göndermez; PC kimliği cihazın kendi Id'sine bağlı). Kartın MAC'i de IP gibi kopyalanmaz.
          camera: source.camera
            ? { brand: source.camera.brand, model: source.camera.model, description: source.camera.description, username: source.camera.username, password: null }
            : null,
          controlCard: source.controlCard ? { macAddress: null } : null,
          pc: source.pc ? { secret: null } : null
        },
        source.template.deviceTypeId
      );
    },
    [requestCreate]
  );

  const submitPendingCreate = useCallback(
    async (request: DeviceCreateRequest) => {
      const ok = await createDevice(request);
      if (ok) setPendingCreate(null);
      return ok;
    },
    [createDevice]
  );

  const cancelPendingCreate = useCallback(() => setPendingCreate(null), []);

  const saveDevice = useCallback(
    async (id: string, properties: DeviceProperties): Promise<boolean> => {
      const node = nodesRef.current.find(n => n.id === id);
      if (node?.type !== 'template') return false;

      const request = toDeviceUpdateRequest({ ...node.data.device, ...properties });
      const ok = (await send(() => diagramApi.updateDevice(cabinetId, id, request), { optimistic: false })) !== null;
      if (!ok) return false;

      // Yerleşim istek uçarken değişmiş olabilir: özellikler GÜNCEL node'un üstüne yazılır.
      const saved = savedDeviceProperties(properties);
      setNodes(current => current.map(item => (item.id === id && item.type === 'template' ? withDevice(item, { ...item.data.device, ...saved }) : item)));
      return true;
    },
    [cabinetId, send]
  );

  const updateDeviceLayout = useCallback(
    (id: string, patch: DeviceLayoutPatch) => {
      const node = nodesRef.current.find(n => n.id === id);
      if (node?.type !== 'template') return;

      const updated = withDevice(node, { ...node.data.device, ...patch });
      setNodes(current => current.map(item => (item.id === id && item.type === 'template' ? withDevice(item, { ...item.data.device, ...patch }) : item)));
      sendLayout([updated]);
    },
    [sendLayout]
  );

  const addAnnotation = useCallback(
    (shape: AnnotationShape, position: XYPosition) => {
      const takenNames = nodesRef.current.filter(node => node.type === 'annotation').map(node => node.data.annotation.name);
      // Kaydırma TÜM node'lara bakıyor, yalnızca notlara değil: yeni notun bir
      // cihazın tam altında doğması da onu görünmez yapardı.
      const spot = cascadePosition(position, nodesRef.current.map(node => node.position));
      const annotation = newAnnotationDraft(newId(), shape, spot, takenNames);

      setNodes(current => [...current, toAnnotationNode(annotation)]);
      void send(() => diagramApi.createAnnotation(cabinetId, toAnnotationCreateRequest(annotation)), { optimistic: true });
    },
    [cabinetId, send]
  );

  const updateAnnotation = useCallback(
    (id: string, patch: Partial<DiagramAnnotationItemDto>) => {
      const node = nodesRef.current.find(n => n.id === id);
      if (node?.type !== 'annotation') return;

      const updated = withAnnotation(node, { ...node.data.annotation, ...patch });
      setNodes(current => current.map(item => (item.id === id && item.type === 'annotation' ? withAnnotation(item, { ...item.data.annotation, ...patch }) : item)));

      // İçerik ve yerleşim ayrı uçlardadır; panel tek seferde yalnızca birine dokunur ama ikisi birden de gelebilir.
      if (touchesContent(patch)) {
        const request = toAnnotationUpdateRequest(updated.data.annotation);
        void send(() => diagramApi.updateAnnotation(cabinetId, id, request), { optimistic: true });
      }
      if (touchesLayout(patch)) sendLayout([updated]);
    },
    [cabinetId, send, sendLayout]
  );

  const updateConnection = useCallback(
    (id: string, patch: Partial<DiagramConnectionDto>) => {
      const edge = edgesRef.current.find(e => e.id === id);
      if (!edge?.data) return;

      const connection = { ...edge.data.connection, ...patch };
      setEdges(current =>
        current.map(item => {
          if (item.id !== id || !item.data) return item;
          // DTO güncellenip edge ONDAN yeniden kuruluyor: renk / stil / routing hem DTO'da hem RF edge'inin kendi
          // alanlarında yaşıyor. `selected` korunmazsa özellik paneli düzenlediği kablonun seçimini düşürürdü.
          return { ...toRfEdge({ ...item.data.connection, ...patch }), selected: item.selected };
        })
      );
      void send(() => diagramApi.updateConnection(cabinetId, id, toConnectionUpdateRequest(connection)), { optimistic: true });
    },
    [cabinetId, send]
  );

  const deleteElements = useCallback(
    (nodeIds: string[], edgeIds: string[]) => {
      const doomedNodeIds = new Set(nodeIds);
      const doomedEdgeIds = new Set(edgeIds);

      // Silinen cihazın kabloları tuvalden de gitmeli (sunucu onları zaten birlikte siler). RF'in `remove` değişikliği
      // bunu kendi yapmaz; ucu kalmayan kablo canvas'ta asılı kalırdı. `Delete` tuşu yolunda aynı hesabı RF yapıyor.
      for (const edge of edgesRef.current) {
        if (doomedNodeIds.has(edge.source) || doomedNodeIds.has(edge.target)) doomedEdgeIds.add(edge.id);
      }

      const doomedNodes = nodesRef.current.filter(node => doomedNodeIds.has(node.id));
      setEdges(current => current.filter(edge => !doomedEdgeIds.has(edge.id)));
      setNodes(current => current.filter(node => !doomedNodeIds.has(node.id)));
      sendDelete(doomedNodes, [...doomedEdgeIds]);
    },
    [sendDelete]
  );

  const moveNodes = useCallback(
    (positions: Record<string, XYPosition>) => {
      const moveTo = (node: DiagramNode) => {
        const position = positions[node.id];
        return position ? repositioned(node, position) : node;
      };
      const moved = nodesRef.current.filter(node => positions[node.id]).map(moveTo);
      if (moved.length === 0) return;

      setNodes(current => current.map(moveTo));
      sendLayout(moved);
    },
    [sendLayout]
  );

  const onSelectionChange = useCallback(
    ({ nodes: selectedNodes, edges: selectedEdges }: { nodes: DiagramNode[]; edges: DiagramEdge[] }) => {
      dispatch(setSelection({ nodeIds: selectedNodes.map(n => n.id), edgeIds: selectedEdges.map(e => e.id) }));
    },
    [dispatch]
  );

  return {
    nodes,
    edges,
    onNodesChange,
    onEdgesChange,
    onNodeDragStop,
    onConnect,
    isValidConnection,
    onSelectionChange,
    onDelete,
    addDeviceFromTemplate,
    duplicateDevice,
    pendingCreate,
    submitPendingCreate,
    cancelPendingCreate,
    saveDevice,
    updateDeviceLayout,
    updateConnection,
    deleteElements,
    moveNodes,
    addAnnotation,
    updateAnnotation,
    activity
  };
}

function isDeviceNode(node: DiagramNode): node is DeviceNode {
  return node.type === 'template';
}

function isAnnotationNode(node: DiagramNode): node is AnnotationNode {
  return node.type === 'annotation';
}

/**
 * Cihaz DTO'sunu node'a yazar. Node'un KENDİ alanları da tazelenir: `zIndex`, `hidden`, `draggable` ve kutu ölçüsü React
 * Flow'un okuduğu yerlerdir; yalnızca `data`'yı yazmak "kilitle"ye basıldığında cihazın sürüklenmeye devam etmesi, panelde
 * girilen genişliğin canvas'ta hiç değişmemesi demek olurdu (`align-toolbar.tsx > toBox` da `node.width`'i okuyor).
 */
function withDevice(node: DeviceNode, device: DiagramDeviceDto): DeviceNode {
  return { ...node, data: { device }, ...deviceSize(device), zIndex: device.zIndex, hidden: !device.isVisible, draggable: !device.isLocked };
}

function withAnnotation(node: AnnotationNode, annotation: DiagramAnnotationItemDto): AnnotationNode {
  return {
    ...node,
    data: { annotation },
    position: { x: annotation.coordinateX, y: annotation.coordinateY },
    width: annotation.width,
    height: annotation.height,
    zIndex: annotation.zIndex,
    hidden: !annotation.isVisible,
    draggable: !annotation.isLocked,
    selectable: !annotation.isLocked
  };
}

/**
 * Node'u yeni konuma taşır ve DTO'daki koordinatı da tazeler.
 *
 * Konum İKİ yerde yaşıyor: React Flow `node.position`'ı, özellikler paneli ve istekler `data.*.coordinateX/Y`'yi okuyor.
 */
function repositioned(node: DiagramNode, position: XYPosition): DiagramNode {
  if (node.type === 'annotation') {
    return { ...node, position, data: { annotation: { ...node.data.annotation, coordinateX: position.x, coordinateY: position.y } } };
  }
  return { ...node, position, data: { device: { ...node.data.device, coordinateX: position.x, coordinateY: position.y } } };
}

/** Yeni cihaz en üstte doğar; altta kalıp görünmez olması kafa karıştırıcı olurdu. */
function nextZIndex(nodes: DiagramNode[]): number {
  return nodes.reduce((max, node) => Math.max(max, node.zIndex ?? 0), 0) + 1;
}

/**
 * "Kontrol Modülü", "Kontrol Modülü 2", … — `Device.Name` üzerinde benzersizlik
 * kısıtı YOK, ama aynı adlı üç kutu kullanıcı için okunmaz olurdu.
 */
function nextDeviceName(templateName: string, nodes: DiagramNode[]): string {
  const taken = new Set(nodes.filter(n => n.type === 'template').map(n => n.data.device.name));
  if (!taken.has(templateName)) return templateName;

  for (let index = 2; ; index++) {
    const candidate = `${templateName} ${index}`;
    if (!taken.has(candidate)) return candidate;
  }
}
