import { useEffect, useLayoutEffect, useMemo, useRef, type RefObject } from 'react';
import type { FeatureCollection, Point } from 'geojson';
import type { ExpressionSpecification, FilterSpecification, GeoJSONSource, Map as MapLibreMap, MapLayerMouseEvent, MapMouseEvent, Subscription } from 'maplibre-gl';
import { useMap } from '@/components/ui/map';
import type { CabinetDetailDto } from '@/models/cabinet';
import { DeviceStatus } from '@/models/enums';
import CabinetIdleIcon from '@/assets/cabinet-idle-2.png';
import CabinetInProcessIcon from '@/assets/cabinet-inproces-2.png';

/** Haritada iğnesi olan kabin: koordinatları `null` olamaz. */
export type LocatedCabinet = CabinetDetailDto & { latitude: number; longitude: number };

/**
 * Feature'a yalnızca çizim ve tıklama çözümlemesi için gerekenler konur. Durum FİLTRESİ JS'te (`visibleCabinets`)
 * uygulandığı için `deviceStatusId` / `isActive` worker'a taşınmaz; isim kutusundaki durum noktası için yalnızca
 * türetilmiş `statusKey` (görsel seçimi) ve `statusRank` (kümenin en kötü durumu) taşınır.
 */
type CabinetFeatureProperties = { id: string; name: string; isBusy: boolean; isAlert: boolean; statusKey: StatusKey; statusRank: number };

// Kimlikler sabit: sayfada tek bir kabin katmanı var. İkinci bir örnek gerekirse `useId` önekine geçilmeli.
const SOURCE_ID = 'cabinets';
const CLUSTER_LAYER_ID = 'cabinet-clusters';
const CLUSTER_COUNT_LAYER_ID = 'cabinet-cluster-count';
const CLUSTER_STATUS_LAYER_ID = 'cabinet-cluster-status';
const POINT_LAYER_ID = 'cabinet-points';
const HOVER_LAYER_ID = 'cabinet-point-hover';
const LABEL_LAYER_ID = 'cabinet-labels';
/** Tıklama/hover'da kabini temsil eden katmanlar: ikon ve isim kutusu. */
const CABINET_LAYER_IDS = [POINT_LAYER_ID, LABEL_LAYER_ID];

/**
 * İkonun tabanı. `alert`: modülün alarm bildirdiği kabin (örn. zorla açma) — "işlem" ikonu + kırmızı rozet; kabin
 * durumundan bağımsızdır. Öncelik: alarm > işlem > boşta.
 */
const ICON_BASES = ['idle', 'busy', 'alert'] as const;
type IconBase = (typeof ICON_BASES)[number];

type StatusKey = 'unknown' | 'online' | 'maintenance' | 'warning' | 'offline' | 'critical';

/**
 * İsim kutusunun sol üstündeki kabin DURUMU noktası (kümede dairenin sağ üst kenarı). Renkler `StatusDot` (template-node.tsx) ve ana sayfa rozetlerinin
 * (`STATUS_CHIP_DEFS`) Tailwind renklerinin hex kopyasıdır — canvas sınıf okuyamaz; birini değiştirirseniz ötekileri de.
 * `rank` yalnızca kümenin "en kötü" noktası içindir ve sunucudaki `DeviceStatusSeverityRank` sırasını izler, tek farkla:
 * Online, Bilinmiyor'dan yüksektir — "hepsi çevrimiçi" küme yeşil görünsün. `rank` 0 olan kümede nokta çizilmez.
 */
const STATUS_BADGES: Record<StatusKey, { color: string; rank: number }> = {
  unknown: { color: '#9ca3af', rank: 0 },
  online: { color: '#10b981', rank: 1 },
  maintenance: { color: '#0ea5e9', rank: 2 },
  warning: { color: '#f59e0b', rank: 3 },
  offline: { color: '#64748b', rank: 4 },
  critical: { color: '#ef4444', rank: 5 }
};

/** `null` "Bilinmiyor"dur (canlılık kanıtı yok) — `Offline` (0) ile AYNI ŞEY DEĞİL; ayrı gri nokta alır. */
function statusKeyOf(statusId: DeviceStatus | null): StatusKey {
  switch (statusId) {
    case DeviceStatus.Online:
      return 'online';
    case DeviceStatus.Warning:
      return 'warning';
    case DeviceStatus.Critical:
      return 'critical';
    case DeviceStatus.Maintenance:
      return 'maintenance';
    case DeviceStatus.Offline:
      return 'offline';
    default:
      return 'unknown';
  }
}

const cabinetIconId = (base: IconBase) => `cabinet-${base}`;
const CLUSTER_DOT_ICON_PREFIX = 'cabinet-status-dot-';
const clusterDotIconId = (rank: number) => `${CLUSTER_DOT_ICON_PREFIX}${rank}`;

/** Kümeleme bu zoom'un ÜSTÜNDE durur; `fitBounds`'un tek kabinde indiği zoom 17 hep tekil kabin gösterir. */
const CLUSTER_MAX_ZOOM = 14;
/** Kümenin içinde işlem yapılan kabin varsa kenarı bu renkte ve kalın çizilir — uzak zoom'da da kaybolmasın. */
const BUSY_CLUSTER_STROKE = '#f59e0b';
/** Kümenin içinde alarm olan kabin varsa kenar bu renkte — "işlem"den ÖNCELİKLİ. */
const ALERT_CLUSTER_STROKE = '#ef4444';

/** Eski DOM işaretçisinin `h-12`'si (CSS piksel). */
const ICON_HEIGHT = 64;
/**
 * İkon atlasına 2x çözünürlükle konur. WebGL ikon atlası mipmap kullanmaz: 493 piksellik PNG'yi çalışma
 * anında ~10 kat küçültmek kenarları tırtıklı gösterirdi — bu yüzden bir kez, canvas'ta küçültülür.
 */
const ICON_PIXEL_RATIO = 4;
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
/**
 * İkonun üstündeki isim kutusu (CSS piksel): beyaz kutu, alt ortasında ikonu gösteren sivri uç, sol üst köşesinde
 * durum noktası. Metin glyph fontuyla değil canvas'ta, uygulamanın fontuyla (Geist) çizilir; kutu her iki temada
 * beyazdır. `margin` görselin her kenarındaki boşluktur: gölge ve kutunun köşesinden taşan nokta buraya sığar.
 */
const LABEL = {
  font: '600 12px "Geist Variable", sans-serif',
  textColor: '#0a0a0a',
  padX: 8,
  height: 22,
  radius: 6,
  pointerWidth: 10,
  pointerHeight: 6,
  /** Daha uzun adlar `…` ile kısalır — kutu haritayı kapatmasın. */
  maxTextWidth: 180,
  /** Sivri ucun ikonun üst kenarına uzaklığı. */
  gap: 2,
  /** `dotRadius`'tan küçük olmamalı: kutunun köşesine oturan noktanın taşan yarısı buraya sığar. */
  margin: 8,
  dotRadius: 7,
  dotRing: 2
} as const;
/** Görselin altı (`icon-anchor: bottom`) sivri ucun `margin` kadar altıdır; uç ikonun üst kenarına `gap` kadar yaklaşır. */
const LABEL_ICON_OFFSET: [number, number] = [0, -(ICON_HEIGHT / 2 + LABEL.gap) + LABEL.margin];
const LABEL_SHADOW = { offsetY: 2, blur: 4, color: 'rgba(0, 0, 0, 0.25)' };
/** Görsel kimliği `cabinet-label:<statusKey>:<ad>` — durum noktası kutuya gömülüdür. */
const LABEL_IMAGE_PREFIX = 'cabinet-label:';

const IS_CLUSTER: ExpressionSpecification = ['has', 'point_count'];
const IS_CABINET: ExpressionSpecification = ['!', ['has', 'point_count']];
// Taban önceliği alarm > işlem > boşta. Durum noktası ikonda değil, isim kutusunda (`rasterizeLabel`).
const ICON_BASE: ExpressionSpecification = ['case', ['get', 'isAlert'], 'alert', ['get', 'isBusy'], 'busy', 'idle'];
const ICON_IMAGE: ExpressionSpecification = ['concat', 'cabinet-', ICON_BASE];
// Üst üste binmede alarmlı kabin en üstte, meşgul kabin boştakilerin ÜSTÜNDE kalsın (yüksek anahtar sonra çizilir).
const CABINET_SORT_KEY: ExpressionSpecification = ['case', ['get', 'isAlert'], 2, ['get', 'isBusy'], 1, 0];

/** Hover katmanının filtresi: yalnızca imlecin altındaki kabin (id `null` iken hiçbir şeyle eşleşmez). */
function hoverFilter(cabinetId: string | null): FilterSpecification {
  return ['all', IS_CABINET, ['==', ['get', 'id'], cabinetId ?? '']];
}

/** Durum noktasının yarıçapı (atlas pikseli). Alarm rozetinden küçük: ikincil bilgi, "!"in önüne geçmesin. */
const STATUS_DOT_RADIUS = ICON_HEIGHT * 0.13 * ICON_PIXEL_RATIO;
/**
 * Küme dairesinin (yarıçap 18/24/32, bkz. `circle-radius`) sağ üst kenarına oturan nokta ofseti, CSS pikseli.
 * `circle-radius` basamaklarıyla aynı eşikler; yarıçap × ~0,7 = 45°'deki kenar.
 */
const CLUSTER_DOT_OFFSET: ExpressionSpecification = [
  'step',
  ['get', 'point_count'],
  ['literal', [13, -13]],
  25,
  ['literal', [17, -17]],
  250,
  ['literal', [22, -22]]
];

async function decodeImage(url: string): Promise<HTMLImageElement> {
  const image = new Image();
  image.src = url;
  await image.decode();
  return image;
}

function createContext(width: number, height: number): CanvasRenderingContext2D {
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('Kabin ikonu için 2D canvas bağlamı alınamadı.');
  return context;
}

function rasterizeIcon(image: HTMLImageElement, options: { alertBadge: boolean }): ImageData {
  const width = Math.round((image.naturalWidth / image.naturalHeight) * ICON_HEIGHT) * ICON_PIXEL_RATIO;
  const height = ICON_HEIGHT * ICON_PIXEL_RATIO;
  const left = SHADOW_PAD_X * ICON_PIXEL_RATIO;
  const top = SHADOW_PAD_Y * ICON_PIXEL_RATIO;
  const context = createContext(width + left * 2, height + top * 2);

  context.imageSmoothingQuality = 'high';
  context.shadowColor = SHADOW.color;
  context.shadowBlur = SHADOW.blur * ICON_PIXEL_RATIO;
  context.shadowOffsetY = SHADOW.offsetY * ICON_PIXEL_RATIO;
  context.drawImage(image, left, top, width, height);
  // Alarm rozeti sağ üstte. Durum noktası ikonda değil, isim kutusunda (`rasterizeLabel`).
  if (options.alertBadge) drawAlertBadge(context, left + width, top);
  return context.getImageData(0, 0, context.canvas.width, context.canvas.height);
}

/** Kümenin durum noktası, tek başına. */
function rasterizeDot(color: string): ImageData {
  const size = Math.ceil(STATUS_DOT_RADIUS * 2);
  const context = createContext(size, size);
  drawStatusDot(context, size / 2, size / 2, { color, radius: STATUS_DOT_RADIUS, ring: 1.5 * ICON_PIXEL_RATIO });
  return context.getImageData(0, 0, size, size);
}

/** Beyaz halkalı renkli nokta — haritanın her iki temasında da seçilsin. `radius` / `ring` bağlamın pikseliyle. */
function drawStatusDot(context: CanvasRenderingContext2D, centerX: number, centerY: number, dot: { color: string; radius: number; ring: number }) {
  context.shadowColor = 'transparent';
  context.fillStyle = '#ffffff';
  context.beginPath();
  context.arc(centerX, centerY, dot.radius, 0, Math.PI * 2);
  context.fill();

  context.fillStyle = dot.color;
  context.beginPath();
  context.arc(centerX, centerY, dot.radius - dot.ring, 0, Math.PI * 2);
  context.fill();
}

/** İsim kutusu görselleri bu çözünürlükte: yüksek DPI ekranda metin bulanık kalmasın. */
const labelPixelRatio = () => Math.max(2, Math.ceil(window.devicePixelRatio || 1));

/** Kutuya sığan metin ve kutu genişliği, isim başına bir kez ölçülür (her durum rengi aynı ölçüyü kullanır). */
const labelLayouts = new Map<string, { text: string; boxWidth: number }>();

function labelLayout(name: string): { text: string; boxWidth: number } {
  let layout = labelLayouts.get(name);
  if (layout) return layout;

  const context = createContext(1, 1);
  context.font = LABEL.font;
  let text = name;
  if (context.measureText(text).width > LABEL.maxTextWidth) {
    while (text.length > 1 && context.measureText(`${text}…`).width > LABEL.maxTextWidth) text = text.slice(0, -1);
    text = `${text.trimEnd()}…`;
  }
  layout = { text, boxWidth: Math.ceil(context.measureText(text).width) + LABEL.padX * 2 };
  labelLayouts.set(name, layout);
  return layout;
}

/**
 * İsim kutusu: kutu `margin` kadar içeride, altında sivri uç, sol üst köşesinde durum noktası. Çizim CSS pikseliyle.
 * Nokta kutuyla aynı görselde: üst üste binen kabinlerde her nokta kendi kutusuyla birlikte önde ya da arkada kalır.
 */
function rasterizeLabel(name: string, statusColor: string): ImageData {
  const { text, boxWidth } = labelLayout(name);
  const ratio = labelPixelRatio();
  const context = createContext(
    Math.ceil((boxWidth + LABEL.margin * 2) * ratio),
    Math.ceil((LABEL.height + LABEL.pointerHeight + LABEL.margin * 2) * ratio)
  );
  context.scale(ratio, ratio);
  const { margin: x, margin: y, height, radius, pointerWidth, pointerHeight } = LABEL;
  const right = x + boxWidth;
  const bottom = y + height;
  const centerX = x + boxWidth / 2;

  // Yuvarlak köşeli kutu + alt ortada aşağı bakan uç, tek yol: gölge ve kenar ikisini birlikte sarar.
  context.beginPath();
  context.moveTo(x + radius, y);
  context.arcTo(right, y, right, bottom, radius);
  context.arcTo(right, bottom, centerX, bottom, radius);
  context.lineTo(centerX + pointerWidth / 2, bottom);
  context.lineTo(centerX, bottom + pointerHeight);
  context.lineTo(centerX - pointerWidth / 2, bottom);
  context.arcTo(x, bottom, x, y, radius);
  context.arcTo(x, y, right, y, radius);
  context.closePath();

  context.shadowColor = LABEL_SHADOW.color;
  context.shadowBlur = LABEL_SHADOW.blur;
  context.shadowOffsetY = LABEL_SHADOW.offsetY;
  context.fillStyle = '#ffffff';
  context.fill();
  context.shadowColor = 'transparent';
  context.strokeStyle = 'rgba(0, 0, 0, 0.08)';
  context.lineWidth = 1;
  context.stroke();

  context.font = LABEL.font;
  context.fillStyle = LABEL.textColor;
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  context.fillText(text, centerX, y + height / 2 + 0.5);

  drawStatusDot(context, x, y, { color: statusColor, radius: LABEL.dotRadius, ring: LABEL.dotRing });
  return context.getImageData(0, 0, context.canvas.width, context.canvas.height);
}

/**
 * İsim kutusu (ad × durum) ilk istendiğinde üretilir (`setMissingStyleImageResolver`): yeni kabin, ad ya da durum
 * değişikliğinde ve tema değişiminin stil yenilemesinden sonra ayrıca bir şey yapılmaz. `styleimagemissing` olayı
 * DEĞİL: MapLibre 6'da olay, görsel eksik sayıldıktan sonra gelir ve sembol o görselsiz yerleşir.
 */
function provideLabelImage(map: MapLibreMap, id: string) {
  if (!id.startsWith(LABEL_IMAGE_PREFIX)) return;
  const rest = id.slice(LABEL_IMAGE_PREFIX.length);
  const separator = rest.indexOf(':');
  const badge = STATUS_BADGES[rest.slice(0, separator) as StatusKey];
  if (separator < 0 || !badge) return;
  map.addImage(id, rasterizeLabel(rest.slice(separator + 1), badge.color), { pixelRatio: labelPixelRatio() });
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
  cabinetIcons ??= Promise.all([decodeImage(CabinetIdleIcon), decodeImage(CabinetInProcessIcon)])
    .then(([idle, busy]) => {
      // Alarm ikonu "işlem" görselinden türer: alarm açık bir oturumda doğar, dolayısıyla o kabin zaten işlemdedir.
      const sources: Record<IconBase, { image: HTMLImageElement; alertBadge: boolean }> = {
        idle: { image: idle, alertBadge: false },
        busy: { image: busy, alertBadge: false },
        alert: { image: busy, alertBadge: true }
      };

      // Taban başına bir görsel. Durum noktası ikonda değil, isim kutusunun görselinde (`rasterizeLabel`).
      const icons: [string, ImageData][] = ICON_BASES.map(base => [cabinetIconId(base), rasterizeIcon(sources[base].image, { alertBadge: sources[base].alertBadge })]);
      for (const { color, rank } of Object.values(STATUS_BADGES)) {
        if (rank > 0) icons.push([clusterDotIconId(rank), rasterizeDot(color)]);
      }
      return icons;
    })
    .catch((error: unknown) => {
      cabinetIcons = null;
      throw error;
    });
  return cabinetIcons;
}

function addCabinetLayers(map: MapLibreMap) {
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

  // Kümedeki en kötü kabin durumu, dairenin sağ üst kenarında nokta. Kenar rengi alarm/işlem için ayrılmış
  // (Uyarı'nın amber'i "işlem" kenarıyla aynı renk) — durum oraya karıştırılmaz.
  map.addLayer({
    id: CLUSTER_STATUS_LAYER_ID,
    type: 'symbol',
    source: SOURCE_ID,
    filter: ['all', IS_CLUSTER, ['>', ['get', 'statusRank'], 0]],
    layout: {
      'icon-image': ['concat', CLUSTER_DOT_ICON_PREFIX, ['to-string', ['get', 'statusRank']]],
      'icon-offset': CLUSTER_DOT_OFFSET,
      'icon-allow-overlap': true,
      'icon-ignore-placement': true
    }
  });

  map.addLayer({
    id: POINT_LAYER_ID,
    type: 'symbol',
    source: SOURCE_ID,
    filter: IS_CABINET,
    layout: {
      'icon-image': ICON_IMAGE,
      'icon-allow-overlap': true,
      'symbol-sort-key': CABINET_SORT_KEY
    }
  });

  // Eski `hover:scale-120`. `icon-size` bir layout özelliği olduğu için feature-state ile değişemez;
  // bunun yerine tek kabinlik bir katman büyük çizilir ve filtresi yalnızca hover edilen id değişince güncellenir.
  // İsim kutularının ALTINDA: büyüyen ikon kendi kutusunun sivri ucunu örtmesin.
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

  // İsim kutusu ikonun üstünde, sivri ucu ikonu gösterir. HER ZAMAN çizilir (`allow-overlap`): durum noktası kutuda
  // olduğu için kutu çakışmada düşseydi kabinin durumu da haritadan kaybolurdu. Sık kabinler zoom 14'e kadar
  // kümelendiği için çakışma yalnızca birbirine çok yakın kabinlerde kalır.
  map.addLayer({
    id: LABEL_LAYER_ID,
    type: 'symbol',
    source: SOURCE_ID,
    filter: IS_CABINET,
    layout: {
      'icon-image': ['concat', LABEL_IMAGE_PREFIX, ['get', 'statusKey'], ':', ['get', 'name']],
      'icon-anchor': 'bottom',
      'icon-offset': LABEL_ICON_OFFSET,
      'icon-allow-overlap': true,
      'symbol-sort-key': CABINET_SORT_KEY
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
    const [feature] = map.queryRenderedFeatures(e.point, { layers: [LABEL_LAYER_ID, POINT_LAYER_ID, CLUSTER_LAYER_ID] });
    const cabinet = feature && CABINET_LAYER_IDS.includes(feature.layer.id) ? cabinetOf(feature.properties) : undefined;
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
    setHovered(feature && CABINET_LAYER_IDS.includes(feature.layer.id) ? (cabinetOf(feature.properties)?.id ?? null) : null);
  };

  const handleMouseLeave = () => {
    map.getCanvas().style.cursor = '';
    setHovered(null);
  };

  const subscriptions: Subscription[] = [
    map.on('click', handleClick),
    map.on('dblclick', CABINET_LAYER_IDS, handleDoubleClick),
    map.on('mousemove', [...CABINET_LAYER_IDS, CLUSTER_LAYER_ID], handleMouseMove),
    map.on('mouseleave', [...CABINET_LAYER_IDS, CLUSTER_LAYER_ID], handleMouseLeave)
  ];

  return () => {
    for (const subscription of subscriptions) subscription.unsubscribe();
    map.getCanvas().style.cursor = '';
  };
}

function toFeatureProperties(cabinet: LocatedCabinet, busyCabinetIds: ReadonlySet<string>, alertCabinetIds: ReadonlySet<string>): CabinetFeatureProperties {
  const statusKey = statusKeyOf(cabinet.deviceStatusId);
  return {
    id: cabinet.id,
    name: cabinet.name,
    isBusy: busyCabinetIds.has(cabinet.id),
    isAlert: alertCabinetIds.has(cabinet.id),
    statusKey,
    statusRank: STATUS_BADGES[statusKey].rank
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
  const { map, isLoaded } = useMap();

  const geojson = useMemo<FeatureCollection<Point, CabinetFeatureProperties>>(
    () => ({
      type: 'FeatureCollection',
      features: cabinets.map(cabinet => ({
        type: 'Feature',
        geometry: { type: 'Point', coordinates: [cabinet.longitude, cabinet.latitude] },
        properties: toFeatureProperties(cabinet, busyCabinetIds, alertCabinetIds)
      }))
    }),
    [cabinets, busyCabinetIds, alertCabinetIds]
  );

  const cabinetById = useMemo(() => new Map(cabinets.map(cabinet => [cabinet.id, cabinet])), [cabinets]);

  // Kurulum async (ikonlar) ve olay dinleyicileri uzun ömürlü: ikisi de en güncel değerleri ref'ten okur.
  // Tazeleme render sırasında değil `useLayoutEffect`'te — bkz. `use-diagram-save.ts`.
  const callbacksRef = useRef<LayerCallbacks>({ cabinetById, onCabinetClick, onCabinetDoubleClick, onBackgroundClick });
  const geojsonRef = useRef(geojson);
  useLayoutEffect(() => {
    callbacksRef.current = { cabinetById, onCabinetClick, onCabinetDoubleClick, onBackgroundClick };
    geojsonRef.current = geojson;
  });

  useEffect(() => {
    if (!map || !isLoaded) return;

    let cancelled = false;
    let unbind: (() => void) | undefined;
    // Kurulumdan ÖNCE bağlanır: katmanlar eklendiği anda isim görsellerini isterler. Resolver harita başına tektir;
    // uygulamada başka bir haritası katmanı resolver atamıyor.
    map.setMissingStyleImageResolver(id => provideLabelImage(map, id));

    const setup = async () => {
      const [icons] = await Promise.all([
        loadCabinetIcons().catch((error: unknown) => {
          // İkon yoksa katman yine kurulur: isim kutuları ve kümeler çizilir, MapLibre eksik ikonu atlar.
          console.error('Kabin ikonları yüklenemedi:', error);
          return [];
        }),
        // İsim kutusu fontla ölçülüp çizilir; font henüz inmediyse yedek fontla ölçülen genişlik önbellekte kalırdı.
        document.fonts.load(LABEL.font).catch(() => undefined)
      ]);
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
            alertCount: ['+', ['case', ['get', 'isAlert'], 1, 0]],
            statusRank: ['max', ['get', 'statusRank']]
          }
        });
      }
      if (!map.getLayer(POINT_LAYER_ID)) addCabinetLayers(map);
      unbind = bindCabinetEvents(map, callbacksRef);
    };
    void setup();

    return () => {
      cancelled = true;
      map.setMissingStyleImageResolver(null);
      unbind?.();
      try {
        for (const layerId of [LABEL_LAYER_ID, HOVER_LAYER_ID, POINT_LAYER_ID, CLUSTER_STATUS_LAYER_ID, CLUSTER_COUNT_LAYER_ID, CLUSTER_LAYER_ID]) {
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

  return null;
}
