import { useEffect, useRef, useState } from 'react';
import { Loader2, MapPin, MapPinPlus, Search, XIcon } from 'lucide-react';
import { toast } from 'sonner';
import { Map, MapControls, MapMarker, MarkerContent, MarkerPopup } from '@/components/ui/map';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

export interface LatLng {
  lat: number;
  lng: number;
}

interface LocationPickerProps {
  /**
   * Seçili konum. `null` = KONUM YOK — kabinin `latitude`/`longitude` alanları
   * nullable olduğu için bu gerçek bir durum, varsayılana düşürülmez.
   */
  value: LatLng | null;
  onChange: (location: LatLng | null) => void;
  /** Konum yokken haritanın açılacağı yer. Kaydedilen bir değer DEĞİL. */
  fallbackCenter?: LatLng;
}

/** Ankara — konum yokken haritanın açılacağı yer. */
const DEFAULT_CENTER: LatLng = { lat: 39.92077, lng: 32.85411 };

export function LocationPicker({ value, onChange, fallbackCenter = DEFAULT_CENTER }: LocationPickerProps) {
  const [searchQuery, setSearchQuery] = useState('');
  const [isSearching, setIsSearching] = useState(false);

  const [viewport, setViewport] = useState<{ center: [number, number]; zoom?: number; bearing?: number; pitch?: number }>(() => ({
    center: [(value ?? fallbackCenter).lng, (value ?? fallbackCenter).lat],
    zoom: value ? 14 : 6
  }));

  /**
   * Dışarıdan gelen konumu haritaya taşı — YALNIZCA BİR KEZ.
   *
   * Düzenleme formu açılırken `value` sunucudan SONRADAN gelir (`GET
   * .../update` asenkron); viewport hiç senkronlanmasaydı harita Ankara'da
   * kalır, pin ise kabinin gerçek konumunda olurdu — kullanıcı boş denize
   * bakardı. Bu efekt yalnızca `value` İLK KEZ dolduğunda çalışıp kendini
   * kapatıyor.
   *
   * Önceki sürüm her `value` değişiminde (yani sürüklemenin HER pikselinde,
   * hatta formdaki alakasız bir alan yazılırken bile — üst bileşen `value`
   * nesnesini her render'da yeniden kuruyor) haritayı pin'in üzerine
   * zorluyordu; harita kullanıcının fare hareketiyle yarışıp zıplıyordu.
   * Sürükleme ZATEN mapbox'ın kendi iç durumu — haritanın onu "takip etmesi"
   * hiç gerekmiyor, yalnızca ilk açılışta gerçek konuma gitmesi yeterli.
   */
  const didInitialSync = useRef(false);
  useEffect(() => {
    if (value == null || didInitialSync.current) return;
    didInitialSync.current = true;
    setViewport(current => ({ ...current, center: [value.lng, value.lat], zoom: 14 }));
  }, [value]);

  function moveMarker(next: LatLng) {
    onChange(next);
  }

  function placeMarker() {
    // Haritanın o an baktığı merkeze koy: kullanıcı zaten oraya gitmiştir.
    onChange({ lat: viewport.center[1], lng: viewport.center[0] });
  }

  async function handleSearch() {
    const query = searchQuery.trim();
    if (!query) return;

    setIsSearching(true);
    try {
      const response = await fetch(`https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=1`);
      if (!response.ok) throw new Error(`Nominatim ${response.status}`);

      const data: Array<{ lat: string; lon: string }> = await response.json();
      const hit = data[0];
      if (!hit) {
        // Sessiz kalmak en kötüsü: kullanıcı "Ara"ya basar, hiçbir şey olmaz ve
        // aramanın çalışmadığını mı yoksa sonuç mu olmadığını bilemez.
        toast.warning('Adres bulunamadı. Farklı bir ifade deneyin.');
        return;
      }

      const found = { lat: Number.parseFloat(hit.lat), lng: Number.parseFloat(hit.lon) };
      if (!Number.isFinite(found.lat) || !Number.isFinite(found.lng)) {
        toast.error('Arama sonucu okunamadı.');
        return;
      }

      onChange(found);
      setViewport({ center: [found.lng, found.lat], zoom: 14 });
    } catch (error) {
      console.error('[location-picker] Nominatim araması başarısız', error);
      toast.error('Adres araması yapılamadı. Bağlantınızı kontrol edin.');
    } finally {
      setIsSearching(false);
    }
  }

  return (
    <div className='flex w-full flex-col gap-3'>
      <div className='flex gap-2'>
        <Input
          placeholder='Adres, şehir veya bölge arayın…'
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          onKeyDown={e => {
            // Form içindeyiz: Enter'ın submit tetiklemesi engellenmezse arama
            // yerine kabin kaydedilir.
            if (e.key !== 'Enter') return;
            e.preventDefault();
            void handleSearch();
          }}
          className='h-8 flex-1'
        />
        <Button type='button' size='sm' variant='secondary' onClick={() => void handleSearch()} disabled={isSearching}>
          {isSearching ? <Loader2 className='animate-spin' /> : <Search />}
          {isSearching ? 'Aranıyor…' : 'Ara'}
        </Button>
      </div>

      <div className='relative h-64 w-full overflow-hidden rounded-md border'>
        <Map viewport={viewport} onViewportChange={v => setViewport(prev => ({ ...prev, ...v }))}>
          <MapControls position='top-right' showZoom showCompass showLocate showFullscreen />

          {value && (
            <MapMarker draggable longitude={value.lng} latitude={value.lat} onDrag={moveMarker}>
              <MarkerContent>
                <div className='cursor-move drop-shadow-md transition-transform hover:scale-110'>
                  <MapPin className='fill-primary stroke-primary-foreground' size={36} />
                </div>
              </MarkerContent>
              <MarkerPopup className='min-w-32 p-2 text-center'>
                <div className='space-y-1'>
                  <p className='text-[11px] font-semibold tracking-wider text-foreground uppercase'>Seçili Konum</p>
                  <p className='font-medium text-xs tabular-nums text-muted-foreground'>
                    {value.lat.toFixed(6)}, {value.lng.toFixed(6)}
                  </p>
                </div>
              </MarkerPopup>
            </MapMarker>
          )}
        </Map>
      </div>

      <div className='flex flex-wrap items-center justify-between gap-2'>
        {value ? (
          <>
            <p className='font-mono text-xs text-muted-foreground'>
              {value.lat.toFixed(6)}, {value.lng.toFixed(6)}
            </p>
            <Button type='button' size='xs' variant='outline' onClick={() => onChange(null)}>
              <XIcon />
              Konumu temizle
            </Button>
          </>
        ) : (
          <>
            <p className='text-xs text-muted-foreground'>Konum belirtilmedi — kabin haritada görünmez.</p>
            <Button type='button' size='xs' variant='outline' onClick={placeMarker}>
              <MapPinPlus />
              Buraya konum koy
            </Button>
          </>
        )}
      </div>
    </div>
  );
}
