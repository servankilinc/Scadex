import { createContext, use } from 'react';

/**
 * Sanal kabinin çizimi iki parçadır ve ikisi de `src/assets/signalization/` altındaki Figma dışa aktarımlarıdır; kod çizim
 * YAPMAZ, dosyaları olduğu gibi kullanır:
 *
 * - **`cabinet-inside.svg` — kasa + konumlar.** Cihaz kutuları (`<g id="…-Box">`) yalnızca yer tutar: kutunun İLK `<rect>`'i
 *   (dolgusuz, görünmez) cihazın çerçevesidir, cihaz bu çerçeveye oturtulur. İstisna `Camera-Box`: durumu olmadığı için
 *   çizimi kasanın içindedir.
 * - **Cihaz dosyaları — görünüm.** Her durum ayrı dosyadır (`Led-Opened.svg` / `Led-Closed.svg`…). Her biri `<defs>`'e bir kez
 *   `<symbol>` olarak eklenir, çerçeveye `<use>` ile çizilir; iç kapı gibi N kez çizilen cihazda da tanımlar çoğalmaz.
 *   Dosyaların iç id'leri (Figma'nın `clip0_55_452`…) sembol adıyla öneklenir — iki siren dosyası aynı id'leri taşıyor.
 *
 * SVG'leri TSX'e ÇEVİRMEYİN: çevrilen kopya asset'ten ayrışır ve Figma'daki değişiklik ekrana yansımaz (2026-09-28 kararı).
 * Figma dışa aktarımında "Include 'id' attribute" açık olmalı. Eksik kutu/çerçeve ekranı çökertmez: geliştirmede konsola
 * uyarı düşer, o cihaz çizilmez.
 */
export const ARTWORK_IDS = {
  camera: 'Camera-Box',
  led: 'Led-Light-Box',
  siren: 'Siren-Box',
  /** Çerçevesi iç kapı ızgarasının alanıdır; kapılar yapılandırmadaki sayı kadar dizilir. */
  indoorList: 'Indoor-List-Box',
  cardReader: 'Card-Reader-Box'
} as const;

export type ArtworkAnchor = keyof typeof ARTWORK_IDS;

/** Cihaz görünümleri: `<symbol>` id'si → kaynak dosya (`use-cabinet-artwork.ts` yükler). */
export const DEVICE_SYMBOLS = {
  'vc-led-on': 'Led-Opened.svg',
  'vc-led-off': 'Led-Closed.svg',
  'vc-siren-on': 'Siren-Opened.svg',
  'vc-siren-off': 'Siren-Closed.svg',
  'vc-indoor-open': 'Indoor-Opened.svg',
  'vc-indoor-closed': 'Indoor-Closed.svg',
  'vc-card-reader': 'Card-Reader.svg'
} as const;

export type DeviceSymbol = keyof typeof DEVICE_SYMBOLS;

/**
 * Çerçeveye sığdırma. Siren dosyaları farklı genişlikte (84 ve 82 — çalarken soldaki ses dalgaları eklenir): sağa
 * yaslanınca gövde iki durumda üst üste biner, ortalansa 1 birim kayardı.
 */
const SYMBOL_ALIGN: Partial<Record<DeviceSymbol, string>> = {
  'vc-siren-on': 'xMaxYMid meet',
  'vc-siren-off': 'xMaxYMid meet'
};

/** Yüklenmiş dosya metinleri. */
export interface ArtworkSources {
  cabinet: string;
  devices: Record<DeviceSymbol, string>;
}

/** SVG kullanıcı birimlerinde kutu. */
export interface ArtworkBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Bir cihaz kutusunun çerçevesi ve React'in portal ile çizdiği katman. */
export interface ArtworkSlot {
  frame: ArtworkBox;
  layer: SVGGElement;
}

/** Sayfaya eklenmiş, hazırlanmış çizim. Bileşenler buna `useArtwork` ile erişir. */
export interface MountedArtwork {
  svg: SVGSVGElement;
  anchors: Partial<Record<ArtworkAnchor, SVGGraphicsElement>>;
  /** Çerçevesi bulunan cihaz kutuları; çerçevesiz kutu burada yoktur ve cihazı çizilmez. */
  slots: Partial<Record<Exclude<ArtworkAnchor, 'camera'>, ArtworkSlot>>;
  /** Sembollerin doğal boyutu (dosyanın `viewBox`'ı) — iç kapı hücresinin boyutu buradan gelir. */
  symbolSizes: Partial<Record<DeviceSymbol, { width: number; height: number }>>;
}

const SVG_NS = 'http://www.w3.org/2000/svg';

/**
 * Kasayı `host`'a ekler, cihaz dosyalarını sembol olarak tanımlar ve her kutuya bir portal katmanı açar. Çağıran yalnızca
 * `svg.remove()` ile söker. Her çağrı dosyaları baştan ayrıştırır — StrictMode'un çift ref çağrısı önceki kopyayı söker.
 */
export function mountArtwork(host: HTMLElement, sources: ArtworkSources): MountedArtwork {
  const doc = host.ownerDocument;
  const svg = doc.importNode(parseSvg(sources.cabinet, 'cabinet-inside.svg'), true) as unknown as SVGSVGElement;

  // Boyutu kap belirler; viewBox oranı korur.
  svg.removeAttribute('width');
  svg.removeAttribute('height');
  svg.setAttribute('class', 'block h-full w-full');
  svg.setAttribute('role', 'img');
  svg.setAttribute('aria-label', 'Kabin iç görünümü');

  const defs = svg.querySelector('defs') ?? svg.appendChild(doc.createElementNS(SVG_NS, 'defs'));
  const symbolSizes: MountedArtwork['symbolSizes'] = {};
  for (const [symbolId, fileName] of Object.entries(DEVICE_SYMBOLS) as [DeviceSymbol, string][]) {
    const { symbol, size } = toSymbol(doc, parseSvg(sources.devices[symbolId], fileName), symbolId);
    defs.append(symbol);
    symbolSizes[symbolId] = size;
  }

  const anchors: MountedArtwork['anchors'] = {};
  for (const [anchor, id] of Object.entries(ARTWORK_IDS) as [ArtworkAnchor, string][]) {
    const element = svg.querySelector<SVGGraphicsElement>(`[id="${id}"]`);
    if (element) anchors[anchor] = element;
  }

  const slots: MountedArtwork['slots'] = {};
  for (const anchor of ['led', 'siren', 'indoorList', 'cardReader'] as const) {
    const box = anchors[anchor];
    const frame = box && readFrame(box);
    if (!box || !frame) continue;

    const layer = doc.createElementNS(SVG_NS, 'g');
    box.append(layer);
    slots[anchor] = { frame, layer };
  }

  warnMissing(anchors, slots);
  host.append(svg);
  return { svg, anchors, slots, symbolSizes };
}

function parseSvg(markup: string, fileName: string): Element {
  const parsed = new DOMParser().parseFromString(markup, 'image/svg+xml');
  if (parsed.documentElement.nodeName !== 'svg' || parsed.querySelector('parsererror')) {
    throw new Error(`Kabin çizimi okunamadı: ${fileName} geçerli bir SVG değil.`);
  }
  return parsed.documentElement;
}

/** Dosyanın kökü `<symbol>` olur: `viewBox` ve kök sunum öznitelikleri (Figma `fill="none"` koyar) korunur. */
function toSymbol(doc: Document, root: Element, symbolId: DeviceSymbol) {
  prefixIds(root, `${symbolId}-`);

  const symbol = doc.createElementNS(SVG_NS, 'symbol');
  symbol.setAttribute('id', symbolId);

  const width = Number(root.getAttribute('width')) || 0;
  const height = Number(root.getAttribute('height')) || 0;
  symbol.setAttribute('viewBox', root.getAttribute('viewBox') ?? `0 0 ${width} ${height}`);
  const fill = root.getAttribute('fill');
  if (fill) symbol.setAttribute('fill', fill);
  const align = SYMBOL_ALIGN[symbolId];
  if (align) symbol.setAttribute('preserveAspectRatio', align);

  for (const child of Array.from(root.childNodes)) symbol.append(doc.importNode(child, true));

  const [, , viewWidth, viewHeight] = (symbol.getAttribute('viewBox') ?? '').split(/[\s,]+/).map(Number);
  return { symbol, size: { width: viewWidth || width, height: viewHeight || height } };
}

/** Dosya içi id'leri ve onlara giden `url(#…)` / `href="#…"` referanslarını önekler. */
function prefixIds(root: Element, prefix: string): void {
  const renamed = new Map<string, string>();
  for (const element of Array.from(root.querySelectorAll('[id]'))) {
    renamed.set(element.id, prefix + element.id);
    element.id = prefix + element.id;
  }
  if (renamed.size === 0) return;

  for (const element of Array.from(root.querySelectorAll('*'))) {
    for (const attribute of Array.from(element.attributes)) {
      const { value } = attribute;
      if (value.startsWith('#') && renamed.has(value.slice(1))) {
        attribute.value = `#${renamed.get(value.slice(1))}`;
      } else if (value.includes('url(#')) {
        attribute.value = value.replace(/url\(#([^)]+)\)/g, (match, id: string) => (renamed.has(id) ? `url(#${renamed.get(id)})` : match));
      }
    }
  }
}

/** Kutunun ilk `<rect>`'i çerçevedir. Öznitelikten okunur (`getBBox` değil): kutu çizimde henüz görünür olmayabilir. */
function readFrame(box: Element): ArtworkBox | null {
  const rect = box.querySelector('rect');
  if (!rect) return null;

  const read = (name: string) => Number(rect.getAttribute(name) ?? 0);
  const frame = { x: read('x'), y: read('y'), width: read('width'), height: read('height') };
  return frame.width > 0 && frame.height > 0 ? frame : null;
}

/** Aynı eksik parça oturum boyunca bir kez duyurulur (StrictMode çift çağrısı tekrar etmesin). */
const reported = new Set<string>();

function warnMissing(anchors: MountedArtwork['anchors'], slots: MountedArtwork['slots']): void {
  if (!import.meta.env.DEV) return;

  const missing = (Object.entries(ARTWORK_IDS) as [ArtworkAnchor, string][])
    .filter(([anchor]) => !anchors[anchor] || (anchor !== 'camera' && !slots[anchor]))
    .map(([anchor, id]) => (anchors[anchor] ? `${id} (çerçeve <rect> yok)` : id))
    .filter(entry => !reported.has(entry));
  if (missing.length === 0) return;

  for (const entry of missing) reported.add(entry);
  console.warn(`[sanal kabin] cabinet-inside.svg içinde eksik: ${missing.join(', ')} — ilgili cihazlar çizilmez.`);
}

export const ArtworkContext = createContext<MountedArtwork | null>(null);

/** Kabin çizimine erişim; yalnızca `<CabinetArtwork>`'ün çocuklarında kullanılabilir (çocuklar çizim hazır olunca çizilir). */
export function useArtwork(): MountedArtwork {
  const artwork = use(ArtworkContext);
  if (!artwork) throw new Error('useArtwork yalnızca <CabinetArtwork> içinde kullanılabilir.');
  return artwork;
}
