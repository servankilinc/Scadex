import { addProtocol, setWorkerUrl, type StyleSpecification } from 'maplibre-gl';
import maplibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url';
import { Protocol } from 'pmtiles';
import { layers, namedFlavor } from '@protomaps/basemaps';
import { API_BASE_URL } from '@/lib/axios-helper';

/**
 * Uygulamadaki her haritanın altlığı. `<Map styles={BASEMAP_STYLES}>` ile verilir; mapcn'in (`components/ui/map`)
 * varsayılanları vendored koddadır ve orada değiştirilmez.
 *
 * Zemin tamamen yereldir: tile, font ve sprite WebAPI'nin `/basemap` yolundan gelir (`Scadex.WebAPI/MapTiles/`,
 * git'te yok — her sunucuya elle konur). Harita dışarıya istek atmaz; kabinlerin çevresindeki tile koordinatları
 * üçüncü taraf bir sunucuya gitmez ve internetsiz sahada da açılır.
 *
 * `basemap.pmtiles` **Protomaps Basemap şemasıdır** (Türkiye, z0–13): VersaTiles / Carto / OpenMapTiles stilleri bu
 * dosyayla çalışmaz, katmanlar `@protomaps/basemaps`'ten üretilir. z13 üstü büyütülerek çizilir. Font ve sprite
 * dosyaları `protomaps/basemaps-assets` deposundandır (`fonts/Noto Sans *`, `sprites/v4/{light,dark}`).
 */
addProtocol('pmtiles', new Protocol().tile);

/**
 * mapcn worker'ı unpkg.com'dan yükler (`components/ui/map`, yalnızca adres atanmamışsa); internetsiz sahada harita
 * hiç açılmazdı. Worker Vite ile `maplibre-gl-shared.mjs` bağımlılığıyla birlikte paketlenir ve buradan verilir.
 * Koşulsuz atanır: map.tsx bu modülden önce değerlendirilip unpkg adresini atamış olabilir; worker ilk `Map`
 * oluşturulurken açıldığı için son atama geçerlidir.
 */
setWorkerUrl(maplibreWorkerUrl);

const BASEMAP_URL = `${API_BASE_URL}/basemap`;

function protomapsStyle(flavor: 'light' | 'dark'): StyleSpecification {
  return {
    version: 8,
    glyphs: `${BASEMAP_URL}/fonts/{fontstack}/{range}.pbf`,
    sprite: `${BASEMAP_URL}/sprites/v4/${flavor}`,
    sources: {
      protomaps: {
        type: 'vector',
        url: `pmtiles://${BASEMAP_URL}/basemap.pmtiles`,
        attribution: '© OpenStreetMap'
      }
    },
    layers: layers('protomaps', namedFlavor(flavor), { lang: 'tr' })
  };
}

// Modül düzeyinde sabit: mapcn `styles`'ı referans değişince JSON'a çevirip karşılaştırır; her render'da yeni nesne
// ~70 katmanlık stili her seferinde yeniden seri hâle getirirdi.
export const BASEMAP_STYLES = { light: protomapsStyle('light'), dark: protomapsStyle('dark') };
