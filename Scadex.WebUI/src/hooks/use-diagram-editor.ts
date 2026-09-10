import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { applyEdgeChanges, applyNodeChanges, type Connection, type Edge, type EdgeChange, type NodeChange, type XYPosition } from '@xyflow/react';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { diagramKeys } from '@/api/query-keys';
import { useAppDispatch } from '@/hooks';
import { ConnectionRejectionMessages, buildConnectionContext, validateConnection } from '@/lib/diagram/connection-rules';
import { buildSaveRequest } from '@/lib/diagram/build-save-request';
import { instantiateFromDevicePins, instantiateFromTemplate } from '@/lib/diagram/instantiate-template-pins';
import { forgetUnsaved, markSaved, markUnsaved, resetUnsaved } from '@/lib/diagram/unsaved-store';
import {
  createJournal,
  journalSize,
  markDeleted,
  markTouched,
  mergeJournal,
  touchedIds,
  type DiagramJournal,
  type JournalFamily
} from '@/lib/diagram/journal';
import { cascadePosition, newAnnotationDraft } from '@/lib/diagram/annotation-defaults';
import { toRfEdge, type DiagramEdge } from '@/lib/diagram/to-rf-edges';
import { toAnnotationNode, toDeviceNode, toRfNodes, type DiagramNode } from '@/lib/diagram/to-rf-nodes';
import { toRfEdges } from '@/lib/diagram/to-rf-edges';
import { AnnotationShape, EdgeRouting, LineStyle, WireType } from '@/models/enums';
import { newId } from '@/lib/sequential-id';
import type { ComponentTemplatePaletteDto } from '@/models/componentTemplate';
import {
  isSaveRequestEmpty,
  type DiagramAnnotationItemDto,
  type DiagramConnectionDto,
  type DiagramDeviceDto,
  type DiagramDto
} from '@/models/diagram';
import { setDirty, setSelection } from '@/store/reducers/diagramSlice';
import { useDiagramSave, type SaveController } from './use-diagram-save';

/**
 * Diyagram editörünün durumu.
 *
 * **İki ayrı kaynak, bilerek.** Sunucudan gelen graf `useDiagramGraph`'ta
 * (TanStack Query) DEĞİŞMEZ olarak durur; düzenlenen hâli burada React Flow
 * state'inde yaşar. Query cache'ini yerinde değiştirmek, kaydedilmemiş bir
 * düzenlemeyi "sunucu gerçeği" gibi göstermek olurdu.
 *
 * **Günlük yalnızca kimlik tutar** (bkz. `lib/diagram/journal.ts`); gönderi
 * gövdesi kaydetme anında RF state'inden kurulur.
 */

/** Yeni çizilen kablonun varsayılan görünümü. Nötr bir ton: tip henüz seçilmedi. */
const DEFAULT_WIRE_COLOR = '#64748B';
const DEFAULT_WIRE_WIDTH = 1.5;

/**
 * Kopya kaynağın tam üstüne değil, hafifçe kaydırılmış doğar — üst üste binen
 * iki kutu, kullanıcıya kopyalamanın işe yarayıp yaramadığını göstermez.
 */
const DUPLICATE_OFFSET = 24;

export interface DiagramEditor {
  nodes: DiagramNode[];
  edges: DiagramEdge[];
  onNodesChange: (changes: NodeChange<DiagramNode>[]) => void;
  onEdgesChange: (changes: EdgeChange<DiagramEdge>[]) => void;
  onNodeDragStop: (event: unknown, node: DiagramNode, nodes: DiagramNode[]) => void;
  onConnect: (connection: Connection) => void;
  isValidConnection: (connection: Connection | Edge) => boolean;
  onSelectionChange: (params: { nodes: DiagramNode[]; edges: DiagramEdge[] }) => void;
  addDeviceFromTemplate: (template: ComponentTemplatePaletteDto, position: XYPosition) => void;
  /** Özellikler panelinin cihaz alanlarını yazdığı yer. */
  updateDevice: (id: string, patch: Partial<DiagramDeviceDto>) => void;
  /** Özellikler panelinin kablo alanlarını yazdığı yer. */
  updateConnection: (id: string, patch: Partial<DiagramConnectionDto>) => void;
  /**
   * Silmenin ARAYÜZ yolu. `Delete` tuşunu React Flow kendi işler; bu, aynı işi
   * bir düğmeden yapılabilir kılıyor — klavye tek yol olmamalı.
   */
  deleteElements: (nodeIds: string[], edgeIds: string[]) => void;
  /** Cihazı kopyalar. Kopya KAYDEDİLMEMİŞ doğar; pinlerini sunucu üretir. */
  duplicateDevice: (id: string) => void;
  /** Hizalama, dağıtma ve konum alanlarının ortak yazma yolu. */
  moveNodes: (positions: Record<string, XYPosition>) => void;
  /** Palet'teki not araçlarının çağırdığı yer. */
  addAnnotation: (shape: AnnotationShape, position: XYPosition) => void;
  /** Özellikler panelinin not alanlarını yazdığı yer. */
  updateAnnotation: (id: string, patch: Partial<DiagramAnnotationItemDto>) => void;
  isDirty: boolean;
  save: SaveController;
}

export function useDiagramEditor(cabinetId: string, graph: DiagramDto): DiagramEditor {
  const dispatch = useAppDispatch();
  const queryClient = useQueryClient();

  const [nodes, setNodes] = useState<DiagramNode[]>(() => toRfNodes(graph));
  const [edges, setEdges] = useState<DiagramEdge[]>(() => toRfEdges(graph));
  const [dirtyCount, setDirtyCount] = useState(0);

  const journalRef = useRef<DiagramJournal>(createJournal());

  // "Kaydedilmemiş" defteri modül düzeyinde yaşıyor (bileşen ağacına bağlanmayan
  // yerlerden de sorulabilmesi için); dolayısıyla editör kapanırken TEMİZLENMELİ,
  // yoksa başka bir kabin açıldığında eski kimlikler orada durur.
  useEffect(() => resetUnsaved, []);
  // Kaydetme geri çağrıları render dışında çalışır; state'i doğrudan okuyamazlar.
  const nodesRef = useRef(nodes);
  const edgesRef = useRef(edges);

  /**
   * Aynalama RENDER SIRASINDA değil, `useLayoutEffect`'te — bkz. `use-diagram-save.ts`.
   *
   * Burada `useEffect` yetmez: `touch()` kaydetmeyi 0 ms'lik bir zamanlayıcıyla
   * kurabiliyor ve pasif effect'lerle zamanlayıcıların sırası garanti değil. Ref
   * bir tur geride kalsaydı, gönderi gövdesi günlükteki en son değişikliği
   * bulamaz ve o değişiklik günlükten silinerek SESSİZCE kaybolurdu.
   */
  useLayoutEffect(() => {
    nodesRef.current = nodes;
    edgesRef.current = edges;
  });

  const syncDirty = useCallback(() => {
    const size = journalSize(journalRef.current);
    setDirtyCount(size);
    dispatch(setDirty(size > 0));
  }, [dispatch]);

  // ---------------------------------------------------------------- kaydetme

  const buildRequest = useCallback(() => {
    const sent = journalRef.current;
    const request = buildSaveRequest(nodesRef.current, edgesRef.current, sent);
    if (isSaveRequestEmpty(request)) return null;

    // TAKAS: uçuş sırasında yapılan düzenlemeler taze bir günlüğe birikir.
    // Aynı günlüğe yazmaya devam etseydik, başarı sonrası temizlik uçuş
    // sırasındaki değişiklikleri de silerdi.
    journalRef.current = createJournal();
    setDirtyCount(0);
    return { request, sent };
  }, []);

  const handleSaved = useCallback(
    (sent: DiagramJournal) => {
      // Kimlik yeniden yazma YOK: Id'leri istemci üretti, sunucu onları aynen
      // kullandı. Gövdede giden her kayıt artık kalıcı.
      markSaved(touchedIds(sent));
      syncDirty();

      // Graf TAZELENMEZ. Pin ve kanal Id'lerini de istemci ürettiği için sunucudan
      // öğrenilecek bir şey kalmadı; eskiden buradaki tek `invalidateQueries`
      // istisnası tam da onları almak içindi.
      //
      // Yine de cache artık bayat: gönderilen düzenlemeler ona yazılmadı.
      // `refetchType: 'none'` yalnızca BAYAT İŞARETLER, istek atmaz — böylece
      // uçuştaki bir düzenleme ezilmez ama editöre bir sonraki girişte taze veri
      // gelir.
      void queryClient.invalidateQueries({ queryKey: diagramKeys.cabinet(cabinetId), refetchType: 'none' });
    },
    [cabinetId, queryClient, syncDirty]
  );

  const handleFailed = useCallback(
    (sent: DiagramJournal, error: unknown) => {
      // Sunucu transaction'ı geri aldı: hiçbir satır değişmedi. Gönderilen günlük
      // geri katılır ki bir sonraki deneme aynı değişiklikleri taşısın.
      mergeJournal(journalRef.current, sent);
      syncDirty();
      toast.error(error instanceof Error ? error.message : 'Diyagram kaydedilemedi');
    },
    [syncDirty]
  );

  const save = useDiagramSave({ cabinetId, buildRequest, onSuccess: handleSaved, onFailure: handleFailed });

  /**
   * Bir düzenleme oldu: "kaydedilmemiş değişiklik" göstergesini tazeler.
   *
   * Kaydetmeyi TETİKLEMEZ. Gönderinin tek tetikleyicisi Kaydet düğmesi
   * (`save.saveNow`); otomatik kaydetme ve klavye kısayolu bilerek yok.
   */
  const touch = useCallback(() => {
    syncDirty();
  }, [syncDirty]);

  // ------------------------------------------------- sunucu grafıyla eşitleme

  const [syncedGraph, setSyncedGraph] = useState(graph);
  // Render sırasında state ayarlamak, prop değişimini yansıtmanın React'in
  // önerdiği yolu (effect'te yapmak kademeli bir render turu daha üretirdi).
  //
  // KOŞUL kritik: kaydedilmemiş iş varken ya da gönderi uçuştayken sunucudan
  // gelen graf kullanıcının düzenlemesini EZERDİ.
  //
  // `dirtyCount` günlüğün boyutunun aynasıdır: günlüğe dokunan her yol
  // `syncDirty`'den geçer (bkz. `touch`, `handleSaved`, `handleFailed`,
  // `buildRequest`). Bu yüzden ayrıca `journalRef`'e bakmaya gerek yok — ki
  // render sırasında bir ref okumak zaten yapılmaması gerekendir.
  if (syncedGraph !== graph && dirtyCount === 0 && save.status !== 'saving') {
    setSyncedGraph(graph);
    setNodes(toRfNodes(graph));
    setEdges(toRfEdges(graph));
  }

  // ------------------------------------------------------------- düzenlemeler

  const familyOf = useCallback((node: DiagramNode | undefined): JournalFamily => (node?.type === 'annotation' ? 'annotations' : 'devices'), []);

  const onNodesChange = useCallback(
    (changes: NodeChange<DiagramNode>[]) => {
      let changed = false;
      for (const change of changes) {
        // KONUM değişiklikleri günlüğe GİRMEZ: sürükleme sırasında saniyede
        // onlarca kez gelirler. Konum yalnızca onNodeDragStop'ta kaydedilir —
        // tek başına bu kural trafiğin neredeyse tamamını siler.
        if (change.type !== 'remove') continue;
        markDeleted(journalRef.current, familyOf(nodesRef.current.find(n => n.id === change.id)), change.id);
        // Hiç kaydedilmemiş bir kayıt silindiyse defterden de düşer; kalsaydı
        // oturum boyunca büyüyen ölü bir küme olurdu.
        forgetUnsaved(change.id);
        changed = true;
      }

      setNodes(current => applyNodeChanges(changes, current));
      if (changed) touch();
    },
    [familyOf, touch]
  );

  const onEdgesChange = useCallback(
    (changes: EdgeChange<DiagramEdge>[]) => {
      let changed = false;
      for (const change of changes) {
        if (change.type !== 'remove') continue;
        markDeleted(journalRef.current, 'connections', change.id);
        forgetUnsaved(change.id);
        changed = true;
      }

      setEdges(current => applyEdgeChanges(changes, current));
      if (changed) touch();
    },
    [touch]
  );

  const onNodeDragStop = useCallback(
    (_event: unknown, node: DiagramNode, draggedNodes: DiagramNode[]) => {
      // Çoklu seçim sürüklendiğinde React Flow üçüncü parametrede hepsini verir;
      // yalnızca `node`'u işlemek diğerlerinin yeni konumunu kaybettirirdi.
      const moved = draggedNodes.length > 0 ? draggedNodes : [node];
      const movedIds = new Set(moved.map(n => n.id));

      for (const item of moved) markTouched(journalRef.current, familyOf(item), item.id);

      // Taşınan node'un DTO'sundaki koordinatı da tazele: gönderi `node.position`
      // okuyor ama özellikler paneli DTO'ya bakıyor, ikisi ayrışmamalı.
      setNodes(current => current.map(item => (movedIds.has(item.id) ? repositioned(item, item.position) : item)));

      touch();
    },
    [familyOf, touch]
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

      const id = newId();
      const draft: DiagramConnectionDto = {
        id,
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
      markTouched(journalRef.current, 'connections', id);
      markUnsaved(id);
      touch();
    },
    [cabinetId, connectionContext, touch]
  );

  const addDeviceFromTemplate = useCallback(
    (template: ComponentTemplatePaletteDto, position: XYPosition) => {
      const id = newId();
      const { pins, ioChannels } = instantiateFromTemplate(template);
      const device: DiagramDeviceDto = {
        id,
        name: nextDeviceName(template.name, nodesRef.current),
        coordinateX: position.x,
        coordinateY: position.y,
        rotation: 0,
        zIndex: nextZIndex(nodesRef.current),
        isLocked: false,
        isVisible: true,
        isActive: true,
        componentTemplateId: template.id,
        externalCode: null,
        deviceStatusId: null,
        deviceStatusName: null,
        lastSeen: null,
        template: {
          id: template.id,
          name: template.name,
          deviceTypeId: template.deviceTypeId,
          width: template.width,
          height: template.height,
          backgroundColor: template.backgroundColor,
          backgroundImageUrl: template.backgroundImageUrl
        },
        // Pinler ve kanallar HEMEN doğar: kimliklerini istemci ürettiği için
        // kaydetmeyi beklemeye gerek yok. Cihaz canvas'a bırakıldığı anda
        // kablolanabilir; sunucu aynı pinleri aynı Id'lerle yazacak.
        pins,
        ioChannels
      };

      setNodes(current => [...current, toDeviceNode(device)]);
      markTouched(journalRef.current, 'devices', id);
      markUnsaved(id);
      touch();
    },
    [touch]
  );

  const updateDevice = useCallback(
    (id: string, patch: Partial<DiagramDeviceDto>) => {
      setNodes(current =>
        current.map(node => {
          if (node.id !== id || node.type !== 'template') return node;
          const device = { ...node.data.device, ...patch };
          // Node'un KENDİ alanları da tazeleniyor. `zIndex`, `hidden` ve
          // `draggable` React Flow'un okuduğu yerlerdir; yalnızca `data`'yı
          // yazmak, "kilitle" düğmesine basıldığında cihazın sürüklenmeye devam
          // etmesi demek olurdu — kaydedip sayfayı yenileyene kadar.
          return {
            ...node,
            data: { device },
            zIndex: device.zIndex,
            hidden: !device.isVisible,
            draggable: !device.isLocked
          };
        })
      );
      markTouched(journalRef.current, 'devices', id);
      touch();
    },
    [touch]
  );

  const addAnnotation = useCallback(
    (shape: AnnotationShape, position: XYPosition) => {
      const id = newId();
      const takenNames = nodesRef.current.filter(node => node.type === 'annotation').map(node => node.data.annotation.name);
      // Kaydırma TÜM node'lara bakıyor, yalnızca notlara değil: yeni notun bir
      // cihazın tam altında doğması da onu görünmez yapardı.
      const spot = cascadePosition(position, nodesRef.current.map(node => node.position));
      const annotation = newAnnotationDraft(id, shape, spot, takenNames);

      setNodes(current => [...current, toAnnotationNode(annotation)]);
      markTouched(journalRef.current, 'annotations', id);
      markUnsaved(id);
      touch();
    },
    [touch]
  );

  const updateAnnotation = useCallback(
    (id: string, patch: Partial<DiagramAnnotationItemDto>) => {
      setNodes(current =>
        current.map(node => {
          if (node.id !== id || node.type !== 'annotation') return node;
          const annotation = { ...node.data.annotation, ...patch };
          // Node'un KENDİ alanları da tazeleniyor: `width`/`height` React Flow'un
          // kutu ölçüsü, `hidden`/`draggable` ise etkileşim kuralları. Yalnızca
          // `data`'yı yazmak, panelde 200'e çıkardığınız genişliğin canvas'ta
          // hiç değişmemesi demek olurdu.
          return {
            ...node,
            data: { annotation },
            width: annotation.width,
            height: annotation.height,
            zIndex: annotation.zIndex,
            hidden: !annotation.isVisible,
            draggable: !annotation.isLocked,
            selectable: !annotation.isLocked
          };
        })
      );
      markTouched(journalRef.current, 'annotations', id);
      touch();
    },
    [touch]
  );

  const updateConnection = useCallback(
    (id: string, patch: Partial<DiagramConnectionDto>) => {
      setEdges(current =>
        current.map(edge => {
          if (edge.id !== id || !edge.data) return edge;
          // DTO güncellenip edge ONDAN yeniden kuruluyor. Renk / stil / routing
          // hem DTO'da hem RF edge'inin kendi alanlarında yaşıyor; ikisini elle
          // ayrı ayrı yazmak, birinin unutulduğu anda gönderi ile ekrandaki
          // görüntünün ayrışması demek olurdu.
          const next = toRfEdge({ ...edge.data.connection, ...patch });
          // `selected` RF'in kendi alanı; yeniden kurarken korunmazsa özellik
          // paneli, düzenlediği kablonun seçimini düşürüp kendini kapatırdı.
          return { ...next, selected: edge.selected };
        })
      );
      markTouched(journalRef.current, 'connections', id);
      touch();
    },
    [touch]
  );

  /**
   * Silmenin arayüz yolu.
   *
   * Değişiklikler mevcut `onNodesChange` / `onEdgesChange`'den GEÇİRİLİYOR,
   * state'e doğrudan yazılmıyor: günlüğe düşürme kuralı tek yerde kalsın diye.
   * Ayrı bir yol açmak, iki silme biçiminden birinin günlüğü güncellemeyi
   * unuttuğu bir gelecek üretirdi.
   */
  const deleteElements = useCallback(
    (nodeIds: string[], edgeIds: string[]) => {
      const doomedNodes = new Set(nodeIds);
      const doomedEdges = new Set(edgeIds);

      // Silinen cihazın kabloları da gitmeli. RF'in `remove` değişikliği bunu
      // KENDİ yapmaz; ucu kalmayan kablo canvas'ta asılı kalır ve kaydedilirken
      // sunucuya artık var olmayan bir pine referans giderdi. `Delete` tuşu
      // yolunda aynı hesabı RF kendisi yapıyor — arayüz yolu onunla aynı
      // davranmak zorunda, yoksa "nasıl sildiğine göre değişen" bir sonuç olur.
      if (doomedNodes.size > 0) {
        for (const edge of edgesRef.current) {
          if (doomedNodes.has(edge.source) || doomedNodes.has(edge.target)) doomedEdges.add(edge.id);
        }
      }

      // Kablolar ÖNCE: node gittikten sonra kalan kablo bir an için uçsuz kalır
      // ve RF o arada onu çizmeye çalışır.
      if (doomedEdges.size > 0) onEdgesChange([...doomedEdges].map(id => ({ type: 'remove' as const, id })));
      if (doomedNodes.size > 0) onNodesChange([...doomedNodes].map(id => ({ type: 'remove' as const, id })));
    },
    [onEdgesChange, onNodesChange]
  );

  const duplicateDevice = useCallback(
    (id: string) => {
      const node = nodesRef.current.find(n => n.id === id);
      if (node?.type !== 'template') return;

      const source = node.data.device;
      const copyId = newId();
      const { pins, ioChannels } = instantiateFromDevicePins(source.pins);
      const device: DiagramDeviceDto = {
        ...source,
        id: copyId,
        // Ad KAYNAKTAN türetiliyor, şablondan değil: kullanıcı cihaza "PSU-Ana"
        // dediyse kopyanın "Güç Kaynağı 4" olması bağlamı kaybettirir.
        name: nextDeviceName(source.name, nodesRef.current),
        coordinateX: node.position.x + DUPLICATE_OFFSET,
        coordinateY: node.position.y + DUPLICATE_OFFSET,
        zIndex: nextZIndex(nodesRef.current),
        // Dış kod KOPYALANMAZ: `IX_Device_CabinetId_ExternalCode` benzersiz ve
        // kopya kaydedilirken çakışırdı. Yeni kodu kullanıcı verir.
        externalCode: null,
        // Canlı durum da kopyalanmaz: yeni cihaz henüz hiç telemetri görmedi.
        // `null`, `Offline` ile AYNI ŞEY DEĞİL.
        deviceStatusId: null,
        deviceStatusName: null,
        lastSeen: null,
        // Pin ŞEMASI kaynaktan gelir, KİMLİKLERİ tazelenir: taşınsalardı kopyaya
        // çizilen kablo kaynağa bağlanırdı. Kablolar kopyalanmıyor, yalnızca cihaz.
        pins,
        ioChannels
      };

      setNodes(current => [...current, toDeviceNode(device)]);
      markTouched(journalRef.current, 'devices', copyId);
      markUnsaved(copyId);
      touch();
    },
    [touch]
  );

  const moveNodes = useCallback(
    (positions: Record<string, XYPosition>) => {
      const ids = Object.keys(positions);
      if (ids.length === 0) return;

      setNodes(current =>
        current.map(node => {
          const position = positions[node.id];
          return position ? repositioned(node, position) : node;
        })
      );

      for (const id of ids) markTouched(journalRef.current, familyOf(nodesRef.current.find(n => n.id === id)), id);
      touch();
    },
    [familyOf, touch]
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
    addDeviceFromTemplate,
    updateDevice,
    updateConnection,
    deleteElements,
    duplicateDevice,
    moveNodes,
    addAnnotation,
    updateAnnotation,
    isDirty: dirtyCount > 0,
    save
  };
}

/**
 * Node'u yeni konuma taşır ve DTO'daki koordinatı da tazeler.
 *
 * Konum İKİ yerde yaşıyor: React Flow `node.position`'ı, özellikler paneli
 * `data.*.coordinateX/Y`'yi okuyor. Birini yazıp diğerini unutmak, kutu ekranda
 * taşınmışken panelin eski koordinatı gösterdiği bir ayrışma üretirdi.
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
