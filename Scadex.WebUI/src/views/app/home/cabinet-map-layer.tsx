import { useEffect, useLayoutEffect, useMemo, useRef, type RefObject } from 'react';
import type { FeatureCollection, Point } from 'geojson';
import type { ExpressionSpecification, FilterSpecification, GeoJSONSource, Map as MapLibreMap, MapLayerMouseEvent, MapMouseEvent, Subscription } from 'maplibre-gl';
import { useMap } from '@/components/ui/map';
import type { CabinetDetailDto } from '@/models/cabinet';
import CabinetIdleIcon from '@/assets/cabinet-idle-2.png';
import CabinetInProcessIcon from '@/assets/cabinet-inproces-2.png';

/** Haritada iğnesi olan kabin: koordinatları `null` olamaz. */
export type LocatedCabinet = CabinetDetailDto & { latitude: number; longitude: number };

/**
 * Feature'a yalnızca çizim ve tıklama çözümlemesi için gerekenler konur. Durum filtresi JS'te
 * (`visibleCabinets`) uygulandığı için `deviceStatusId` / `isActive` worker'a taşınmaz.
 */
type CabinetFeatureProperties = { id: string; name: string; isBusy: boolean; isAlert: boolean };

// Kimlikler sabit: sayfada tek bir kabin katmanı var. İkinci bir örnek gerekirse `useId` önekine geçilmeli.
const SOURCE_ID = 'cabinets';
const CLUSTER_LAYER_ID = 'cabinet-clusters';
const CLUSTER_COUNT_LAYER_ID = 'cabinet-cluster-count';
const POINT_LAYER_ID = 'cabinet-points';
const HOVER_LAYER_ID = 'cabinet-point-hover';
const IDLE_ICON_ID = 'cabinet-idle';
const BUSY_ICON_ID = 'cabinet-busy';
/** Modülün alarm bildirdiği kabin (örn. zorla açma): "işlem" ikonu + kırmızı rozet. Kabin durumundan bağımsızdır. */
const ALERT_ICON_ID = 'cabinet-alert';

/** Kümeleme bu zoom'un ÜSTÜNDE durur; `fitBounds`'un tek kabinde indiği zoom 17 hep tekil kabin gösterir. */
const CLUSTER_MAX_ZOOM = 14;
/** Kümenin içinde işlem yapılan kabin varsa kenarı bu renkte ve kalın çizilir — uzak zoom'da da kaybolmasın. */
const BUSY_CLUSTER_STROKE = '#f59e0b';
/** Kümenin içinde alarm olan kabin varsa kenar bu renkte — "işlem"den ÖNCELİKLİ. */
const ALERT_CLUSTER_STROKE = '#ef4444';

/** Eski DOM işaretçisinin `h-12`'si (CSS piksel). */
const ICON_HEIGHT = 48;
/**
 * İkon atlasına 2x çözünürlükle konur. WebGL ikon atlası mipmap kullanmaz: 493 piksellik PNG'yi çalışma
 * anında ~10 kat küçültmek kenarları tırtıklı gösterirdi — bu yüzden bir kez, canvas'ta küçültülür.
 */
const ICON_PIXEL_RATIO = 2;
/**
 * Tailwind `drop-shadow-lg` (0 4px 4px / %15) canvas'a gömülür — çalışma anında maliyeti yok. Dolgu dikeyde
 * simetrik: `icon-anchor: center` ile ikonun görünen ortası koordinata oturmaya devam eder.
 */
const SHADOW = { offsetY: 4, blur: 4, color: 'rgba(0, 0, 0, 0.15)' };
const SHADOW_PAD_X = SHADOW.blur;
const SHADOW_PAD_Y = SHADOW.blur + SHADOW.offsetY;

/**
 * Carto stilinin glyph sunucusunda bulunan bir font OLMALI (stilin `text-font` listesi: Open Sans
 * Regular/Bold, Montserrat Medium…). mapcn'in küme katmanındaki `Open Sans Semibold` o listede yok.
 */
const LABEL_FONT = ['Open Sans Bold'];
/** Etiket ikonun altında: ikonun yarı yüksekliği + 4 px boşluk, `text-size` cinsinden (em). */
const LABEL_SIZE = 11;
const LABEL_OFFSET_EM = (ICON_HEIGHT / 2 + 4) / LABEL_SIZE;

/** DOM etiketinin `bg-white dark:bg-stone-800` hapının yerini, aynı renklerde bir hale (halo) alır. */
const LABEL_COLORS = {
  light: { text: '#0a0a0a', halo: '#ffffff' },
  dark: { text: '#fafafa', halo: '#292524' }
} as const;

const IS_CLUSTER: ExpressionSpecification = ['has', 'point_count'];
const IS_CABINET: ExpressionSpecification = ['!', ['has', 'point_count']];
// Öncelik: alarm > işlem > boşta.
const ICON_IMAGE: ExpressionSpecification = ['case', ['get', 'isAlert'], ALERT_ICON_ID, ['get', 'isBusy'], BUSY_ICON_ID, IDLE_ICON_ID];

/** Hover katmanının filtresi: yalnızca imlecin altındaki kabin (id `null` iken hiçbir şeyle eşleşmez). */
function hoverFilter(cabinetId: string | null): FilterSpecification {
  return ['all', IS_CABINET, ['==', ['get', 'id'], cabinetId ?? '']];
}

async function rasterizeIcon(url: string, withAlertBadge = false): Promise<ImageData> {
  const image = new Image();
  image.src = url;
  await image.decode();

  const width = Math.round((image.naturalWidth / image.naturalHeight) * ICON_HEIGHT) * ICON_PIXEL_RATIO;
  const height = ICON_HEIGHT * ICON_PIXEL_RATIO;
  const canvas = document.createElement('canvas');
  canvas.width = width + SHADOW_PAD_X * 2 * ICON_PIXEL_RATIO;
  canvas.height = height + SHADOW_PAD_Y * 2 * ICON_PIXEL_RATIO;

  const context = canvas.getContext('2d');
  if (!context) throw new Error('Kabin ikonu için 2D canvas bağlamı alınamadı.');
  context.imageSmoothingQuality = 'high';
  context.shadowColor = SHADOW.color;
  context.shadowBlur = SHADOW.blur * ICON_PIXEL_RATIO;
  context.shadowOffsetY = SHADOW.offsetY * ICON_PIXEL_RATIO;
  context.drawImage(image, SHADOW_PAD_X * ICON_PIXEL_RATIO, SHADOW_PAD_Y * ICON_PIXEL_RATIO, width, height);
  if (withAlertBadge) drawAlertBadge(context, SHADOW_PAD_X * ICON_PIXEL_RATIO + width, SHADOW_PAD_Y * ICON_PIXEL_RATIO);
  return context.getImageData(0, 0, canvas.width, canvas.height);
}

/**
 * İkonun sağ üst köşesine beyaz halkalı kırmızı "!" rozeti. Ayrı bir PNG asset yerine canvas'ta çizilir: ikon
 * görseli değişirse rozet kendiliğinden ona uyar. `right` / `top` ikonun (gölge dolgusu hariç) sağ üst köşesidir.
 */
function drawAlertBadge(context: CanvasRenderingContext2D, right: number, top: number) {
  const radius = ICON_HEIGHT * 0.2 * ICON_PIXEL_RATIO;
  const centerX = right - radius;
  const centerY = top + radius;

  context.shadowColor = 'transparent';
  context.fillStyle = '#ffffff';
  context.beginPath();
  context.arc(centerX, centerY, radius, 0, Math.PI * 2);
  context.fill();

  context.fillStyle = ALERT_CLUSTER_STROKE;
  context.beginPath();
  context.arc(centerX, centerY, radius - 2 * ICON_PIXEL_RATIO, 0, Math.PI * 2);
  context.fill();

  context.fillStyle = '#ffffff';
  context.font = `bold ${Math.round(radius * 1.3)}px sans-serif`;
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  context.fillText('!', centerX, centerY + ICON_PIXEL_RATIO);
}

/**
 * Modül düzeyinde önbellek: tema değişimi stili (ve onunla atlastaki ikonları) sıfırlar, ama PNG yeniden
 * indirilip çözülmez — yalnızca hazır `ImageData` yeniden eklenir. Hata önbelleklenmez, sonraki kurulum dener.
 */
let cabinetIcons: Promise<[string, ImageData][]> | null = null;

function loadCabinetIcons(): Promise<[string, ImageData][]> {
  // Alarm ikonu "işlem" görselinden türer: alarm açık bir oturumda doğar, dolayısıyla o kabin zaten işlemdedir.
  cabinetIcons ??= Promise.all([rasterizeIcon(CabinetIdleIcon), rasterizeIcon(CabinetInProcessIcon), rasterizeIcon(CabinetInProcessIcon, true)]).then(
    ([idle, busy, alert]): [string, ImageData][] => [
      [IDLE_ICON_ID, idle],
      [BUSY_ICON_ID, busy],
      [ALERT_ICON_ID, alert]
    ],
    error => {
      cabinetIcons = null;
      throw error;
    }
  );
  return cabinetIcons;
}

function addCabinetLayers(map: MapLibreMap, labelColors: { text: string; halo: string }) {
  map.addLayer({
    id: CLUSTER_LAYER_ID,
    type: 'circle',
    source: SOURCE_ID,
    filter: IS_CLUSTER,
    paint: {
      'circle-color': ['step', ['get', 'point_count'], '#3b82f6', 25, '#1d4ed8', 250, '#1e3a8a'],
      'circle-radius': ['step', ['get', 'point_count'], 18, 25, 24, 250, 32],
      'circle-opacity': 0.9,
      'circle-stroke-color': [
        'case',
        ['>', ['get', 'alertCount'], 0],
        ALERT_CLUSTER_STROKE,
        ['>', ['get', 'busyCount'], 0],
        BUSY_CLUSTER_STROKE,
        '#ffffff'
      ],
      'circle-stroke-width': ['case', ['any', ['>', ['get', 'alertCount'], 0], ['>', ['get', 'busyCount'], 0]], 3, 1]
    }
  });

  map.addLayer({
    id: CLUSTER_COUNT_LAYER_ID,
    type: 'symbol',
    source: SOURCE_ID,
    filter: IS_CLUSTER,
    layout: {
      'text-field': ['get', 'point_count_abbreviated'],
      'text-font': LABEL_FONT,
      'text-size': 12,
      'text-allow-overlap': true,
      'text-ignore-placement': true
    },
    paint: { 'text-color': '#ffffff' }
  });

  // İkon her zaman çizilir; etiket başka bir sembolle çakışırsa düşer (`text-optional`) ve yakın zoom'da
  // geri gelir. DOM'daki gibi binlerce etiketin üst üste yığılması yerine okunabilir bir seyrelme.
  map.addLayer({
    id: POINT_LAYER_ID,
    type: 'symbol',
    source: SOURCE_ID,
    filter: IS_CABINET,
    layout: {
      'icon-image': ICON_IMAGE,
      'icon-allow-overlap': true,
      // Üst üste binmede alarmlı kabin en üstte, meşgul kabin boştakilerin ÜSTÜNDE kalsın (yüksek anahtar sonra çizilir).
      'symbol-sort-key': ['case', ['get', 'isAlert'], 2, ['get', 'isBusy'], 1, 0],
      'text-field': ['get', 'name'],
      'text-font': LABEL_FONT,
      'text-size': LABEL_SIZE,
      'text-anchor': 'top',
      'text-offset': [0, LABEL_OFFSET_EM],
      // DOM etiketi `whitespace-nowrap`'tı; uzun adlar alt satıra kırılmasın.
      'text-max-width': 30,
      'text-optional': true
    },
    paint: {
      'text-color': labelColors.text,
      'text-halo-color': labelColors.halo,
      'text-halo-width': 1.5
    }
  });

  // Eski `hover:scale-120`. `icon-size` bir layout özelliği olduğu için feature-state ile değişemez;
  // bunun yerine tek kabinlik bir katman büyük çizilir ve filtresi yalnızca hover edilen id değişince güncellenir.
  map.addLayer({
    id: HOVER_LAYER_ID,
    type: 'symbol',
    source: SOURCE_ID,
    filter: hoverFilter(null),
    layout: {
      'icon-image': ICON_IMAGE,
      'icon-size': 1.2,
      'icon-allow-overlap': true,
      'icon-ignore-placement': true
    }
  });
}

type LayerCallbacks = {
  cabinetById: ReadonlyMap<string, LocatedCabinet>;
  onCabinetClick: (cabinet: LocatedCabinet) => void;
  onCabinetDoubleClick: (cabinet: LocatedCabinet) => void;
  onBackgroundClick: () => void;
};

/** Dinleyicileri bağlar ve hepsini çözen fonksiyonu döner. Callback'ler her olayda ref'ten taze okunur. */
function bindCabinetEvents(map: MapLibreMap, callbacks: RefObject<LayerCallbacks>): () => void {
  let hoveredId: string | null = null;
  const setHovered = (cabinetId: string | null) => {
    if (cabinetId === hoveredId) return;
    hoveredId = cabinetId;
    map.setFilter(HOVER_LAYER_ID, hoverFilter(cabinetId));
  };

  const cabinetOf = (properties: Record<string, unknown> | undefined) => {
    const id = properties?.id;
    return typeof id === 'string' ? callbacks.current.cabinetById.get(id) : undefined;
  };

  // Tek tıklama işleyicisi, katmana bağlı değil: popup'ın kapanması da buradan yönetilir. MapLibre popup'ının
  // kendi `closeOnClick`'i KAPALI — o dinleyici bizimkinden önce ya da sonra kaydedilebildiği için (tema
  // değişimi dinleyicileri yeniden bağlar) iki kabin arasında geçişte popup'ı takılı bırakabiliyordu.
  const handleClick = (e: MapMouseEvent) => {
    const [feature] = map.queryRenderedFeatures(e.point, { layers: [POINT_LAYER_ID, CLUSTER_LAYER_ID] });
    const cabinet = feature?.layer.id === POINT_LAYER_ID ? cabinetOf(feature.properties) : undefined;
    if (cabinet) {
      callbacks.current.onCabinetClick(cabinet);
      return;
    }

    callbacks.current.onBackgroundClick();
    const clusterId = feature?.properties.cluster_id;
    if (feature?.layer.id !== CLUSTER_LAYER_ID || feature.geometry.type !== 'Point' || typeof clusterId !== 'number') return;

    const center = feature.geometry.coordinates as [number, number];
    map
      .getSource<GeoJSONSource>(SOURCE_ID)
      ?.getClusterExpansionZoom(clusterId)
      .then(zoom => map.easeTo({ center, zoom }))
      // Yanıt gelmeden `setData` kümeyi dağıttıysa kimlik geçersizdir; tıklama sessizce boşa düşer.
      .catch(() => undefined);
  };

  const handleDoubleClick = (e: MapLayerMouseEvent) => {
    // Haritanın çift tıkla yakınlaştırması tetiklenmesin — burada çift tıklamanın anlamı "detayı aç".
    e.preventDefault();
    const cabinet = cabinetOf(e.features?.[0]?.properties);
    if (cabinet) callbacks.current.onCabinetDoubleClick(cabinet);
  };

  const handleMouseMove = (e: MapLayerMouseEvent) => {
    const feature = e.features?.[0];
    map.getCanvas().style.cursor = 'pointer';
    setHovered(feature?.layer.id === POINT_LAYER_ID ? (cabinetOf(feature.properties)?.id ?? null) : null);
  };

  const handleMouseLeave = () => {
    map.getCanvas().style.cursor = '';
    setHovered(null);
  };

  const subscriptions: Subscription[] = [
    map.on('click', handleClick),
    map.on('dblclick', POINT_LAYER_ID, handleDoubleClick),
    map.on('mousemove', [POINT_LAYER_ID, CLUSTER_LAYER_ID], handleMouseMove),
    map.on('mouseleave', [POINT_LAYER_ID, CLUSTER_LAYER_ID], handleMouseLeave)
  ];

  return () => {
    for (const subscription of subscriptions) subscription.unsubscribe();
    map.getCanvas().style.cursor = '';
  };
}

type CabinetMapLayerProps = {
  /** Haritada gösterilecek (durum filtresinden geçmiş) kabinler. */
  cabinets: LocatedCabinet[];
  /** "İşlem yapılıyor" ikonuyla çizilecek kabinler. Referansı içerik değişmedikçe sabit olmalı — her yeni küme `setData` demektir. */
  busyCabinetIds: ReadonlySet<string>;
  /** Alarm ikonuyla çizilecek kabinler (modülden; kabin durumundan bağımsız). Referans kuralı `busyCabinetIds` ile aynı. */
  alertCabinetIds: ReadonlySet<string>;
  onCabinetClick: (cabinet: LocatedCabinet) => void;
  onCabinetDoubleClick: (cabinet: LocatedCabinet) => void;
  /** Kabin dışında bir yere (boş alan ya da küme) tıklandı. */
  onBackgroundClick: () => void;
};

/**
 * Ana sayfa haritasının kabin katmanı: kabin başına bir DOM işaretçisi yerine tek bir kümelenen GeoJSON
 * kaynağı ve WebGL sembol katmanları. React'e yalnızca veri değişince dokunulur; pan/zoom React'ten geçmez.
 *
 * Kurulum her stil yüklenişinde yeniden yapılır: mapcn tema değişiminde `setStyle(diff:false)` çağırıyor ve bu,
 * kaynak/katman/ikonların hepsini siler (`isLoaded` o arada `false` olur).
 */
export function CabinetMapLayer({ cabinets, busyCabinetIds, alertCabinetIds, onCabinetClick, onCabinetDoubleClick, onBackgroundClick }: CabinetMapLayerProps) {
  const { map, isLoaded, resolvedTheme } = useMap();

  const geojson = useMemo<FeatureCollection<Point, CabinetFeatureProperties>>(
    () => ({
      type: 'FeatureCollection',
      features: cabinets.map(cabinet => ({
        type: 'Feature',
        geometry: { type: 'Point', coordinates: [cabinet.longitude, cabinet.latitude] },
        properties: { id: cabinet.id, name: cabinet.name, isBusy: busyCabinetIds.has(cabinet.id), isAlert: alertCabinetIds.has(cabinet.id) }
      }))
    }),
    [cabinets, busyCabinetIds, alertCabinetIds]
  );

  const cabinetById = useMemo(() => new Map(cabinets.map(cabinet => [cabinet.id, cabinet])), [cabinets]);

  // Kurulum async (ikonlar) ve olay dinleyicileri uzun ömürlü: ikisi de en güncel değerleri ref'ten okur.
  // Tazeleme render sırasında değil `useLayoutEffect`'te — bkz. `use-diagram-save.ts`.
  const callbacksRef = useRef<LayerCallbacks>({ cabinetById, onCabinetClick, onCabinetDoubleClick, onBackgroundClick });
  const geojsonRef = useRef(geojson);
  const labelColorsRef = useRef(LABEL_COLORS[resolvedTheme]);
  useLayoutEffect(() => {
    callbacksRef.current = { cabinetById, onCabinetClick, onCabinetDoubleClick, onBackgroundClick };
    geojsonRef.current = geojson;
    labelColorsRef.current = LABEL_COLORS[resolvedTheme];
  });

  useEffect(() => {
    if (!map || !isLoaded) return;

    let cancelled = false;
    let unbind: (() => void) | undefined;

    const setup = async () => {
      const icons = await loadCabinetIcons().catch((error: unknown) => {
        // İkon yoksa katman yine kurulur: etiketler ve kümeler çizilir, MapLibre eksik ikonu atlar.
        console.error('Kabin ikonları yüklenemedi:', error);
        return [];
      });
      if (cancelled) return;

      for (const [id, data] of icons) {
        if (!map.hasImage(id)) map.addImage(id, data, { pixelRatio: ICON_PIXEL_RATIO });
      }
      if (!map.getSource(SOURCE_ID)) {
        map.addSource(SOURCE_ID, {
          type: 'geojson',
          data: geojsonRef.current,
          cluster: true,
          clusterMaxZoom: CLUSTER_MAX_ZOOM,
          clusterRadius: 50,
          clusterProperties: {
            busyCount: ['+', ['case', ['get', 'isBusy'], 1, 0]],
            alertCount: ['+', ['case', ['get', 'isAlert'], 1, 0]]
          }
        });
      }
      if (!map.getLayer(POINT_LAYER_ID)) addCabinetLayers(map, labelColorsRef.current);
      unbind = bindCabinetEvents(map, callbacksRef);
    };
    void setup();

    return () => {
      cancelled = true;
      unbind?.();
      try {
        for (const layerId of [HOVER_LAYER_ID, POINT_LAYER_ID, CLUSTER_COUNT_LAYER_ID, CLUSTER_LAYER_ID]) {
          if (map.getLayer(layerId)) map.removeLayer(layerId);
        }
        if (map.getSource(SOURCE_ID)) map.removeSource(SOURCE_ID);
      } catch {
        // Stil yeniden yükleniyor olabilir — yeni stilde zaten hiçbiri yok.
      }
    };
  }, [map, isLoaded]);

  // Veri değişince kaynağı yerinde güncelle (kaynak ve katmanlar yeniden kurulmaz). Kurulum henüz bitmediyse
  // kaynak yoktur; o zaman kurulum `geojsonRef`'teki en güncel veriyle başlar.
  useEffect(() => {
    if (!map || !isLoaded) return;
    map.getSource<GeoJSONSource>(SOURCE_ID)?.setData(geojson);
  }, [map, isLoaded, geojson]);

  // Tema, stil değiştirmeden de değişebilir (her iki tema için aynı stil verilirse); etiket renkleri ayrıca izlenir.
  useEffect(() => {
    if (!map || !isLoaded || !map.getLayer(POINT_LAYER_ID)) return;
    map.setPaintProperty(POINT_LAYER_ID, 'text-color', LABEL_COLORS[resolvedTheme].text);
    map.setPaintProperty(POINT_LAYER_ID, 'text-halo-color', LABEL_COLORS[resolvedTheme].halo);
  }, [map, isLoaded, resolvedTheme]);

  return null;
}
