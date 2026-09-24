import { useEffect, useRef, useState, useMemo } from 'react';
import { useQueries, useQuery, type UseQueryResult } from '@tanstack/react-query';
import type { LngLatBoundsLike } from 'maplibre-gl';
import { getCabinetList } from '@/api/cabinet';
import { cabinetKeys } from '@/api/query-keys';
import { Map, MapControls, MapPopup, type MapRef } from '@/components/ui/map';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Info, Activity, SearchIcon } from 'lucide-react';
import type { CabinetDetailDto } from '@/models/cabinet';
import { DeviceStatus, deviceStatusLabel } from '@/models/enums';
import { alertCabinetQueries, busyCabinetQueries } from '@/modules';
import { useCabinetOverviewLive } from '@/hooks/use-cabinet-overview-live';
import { CabinetDetailPanel } from './cabinet-detail-panel';
import { CabinetMapLayer, type LocatedCabinet } from './cabinet-map-layer';
import { DashboardMetrics } from './dashboard-metrics';
import { cn } from '@/lib/utils';

/**
 * Modüllerin kimlik listelerini sıralı, tekil bir anahtara indirger. Modül düzeyinde: kararlı referans.
 * `Set` değil string döner: yoklama her 10 sn'de yeni sonuç nesneleri üretir, içerik aynıysa anahtar da
 * aynı kalır ve harita kaynağı boşuna yeniden yüklenmez (bkz. `busyCabinetIds`).
 */
function combineBusyCabinetKey(results: UseQueryResult<string[]>[]): string {
  return [...new Set(results.flatMap(result => result.data ?? []))].sort().join(',');
}

/** Kabin filtresinde "hepsi" için sentinel — Base UI Select boş string'i "seçim yok" sayar. */
const ALL_CABINETS = 'all';

/** Konumlu kabin yokken haritanın açıldığı yer. */
const DEFAULT_CENTER: [number, number] = [35.2433, 38.9637];
/** İlk zoom ve `fitBounds`'un en fazla ineceği zoom: tek kabin seçilince eski "merkeze + zoom 17" ile aynı sonuç. */
const DEFAULT_ZOOM = 17;
/** Sol üstte rozet satırı, sağ üstte kontroller haritanın üzerinde durur; sığdırılan kabinler altlarında kalmasın. */
const FIT_PADDING = { top: 64, bottom: 48, left: 48, right: 64 };

/** Kabinlerin sınır kutusu. Liste boşsa `null` — harita mevcut konumunda kalır. */
function cabinetBounds(cabinets: LocatedCabinet[]): LngLatBoundsLike | null {
  if (cabinets.length === 0) return null;

  let minLng = Infinity;
  let minLat = Infinity;
  let maxLng = -Infinity;
  let maxLat = -Infinity;
  for (const cabinet of cabinets) {
    minLng = Math.min(minLng, cabinet.longitude);
    minLat = Math.min(minLat, cabinet.latitude);
    maxLng = Math.max(maxLng, cabinet.longitude);
    maxLat = Math.max(maxLat, cabinet.latitude);
  }
  return [
    [minLng, minLat],
    [maxLng, maxLat]
  ];
}

/**
 * Harita üstü durum rozetlerinin sabit sırası: Online → Warning → Critical → Maintenance →
 * Offline, en sonda "Bilinmiyor" kovası. `statusId: null` Offline'dan (0) AYRI bir kovadır — "canlılık
 * kanıtı yok" demektir (bkz. `deviceStatusLabel`) — StatusDot ile aynı renk sözleşmesi
 * (bkz. `template-node.tsx`), yeni bir şema icat edilmedi.
 */
const STATUS_CHIP_DEFS: { key: string; statusId: DeviceStatus | null; label: string; dotColor: string }[] = [
  { key: 'online', statusId: DeviceStatus.Online, label: deviceStatusLabel(DeviceStatus.Online), dotColor: 'bg-emerald-500' },
  { key: 'warning', statusId: DeviceStatus.Warning, label: deviceStatusLabel(DeviceStatus.Warning), dotColor: 'bg-amber-500' },
  { key: 'critical', statusId: DeviceStatus.Critical, label: deviceStatusLabel(DeviceStatus.Critical), dotColor: 'bg-red-500' },
  { key: 'maintenance', statusId: DeviceStatus.Maintenance, label: deviceStatusLabel(DeviceStatus.Maintenance), dotColor: 'bg-sky-500' },
  { key: 'offline', statusId: DeviceStatus.Offline, label: deviceStatusLabel(DeviceStatus.Offline), dotColor: 'bg-slate-500' },
  { key: 'none', statusId: null, label: deviceStatusLabel(null), dotColor: 'bg-gray-400' }
];

/**
 * Aktif kabinleri `deviceStatusId`'ye göre kovalar. Sayısı 0 olan kovalar da döner: rozet satırı her zaman
 * aynı altı rozeti gösterir, durum dağılımı değişince rozetler yer değiştirmez.
 */
function buildStatusChips(activeCabinets: { deviceStatusId: DeviceStatus | null }[]) {
  return STATUS_CHIP_DEFS.map(def => ({
    ...def,
    count: activeCabinets.filter(c => c.deviceStatusId === def.statusId).length
  }));
}

export default function Home() {
  const { data: cabinets = [], isLoading } = useQuery({
    queryKey: cabinetKeys.list(),
    queryFn: getCabinetList
  });
  // Kabin durumu değişince liste tazelenir: harita rozetleri ve popup sayfa yenilenmeden güncel kalır.
  useCabinetOverviewLive();

  // "İşlem yapılan" kabinler modüllerden gelir (bkz. `AppModule.busyCabinetsQuery`); modül yoksa küme boştur.
  // Küme anahtar üzerinden memolanır: içerik değişmedikçe referans sabit, harita kaynağı yeniden yüklenmez.
  const busyCabinetKey = useQueries({ queries: busyCabinetQueries, combine: combineBusyCabinetKey });
  const busyCabinetIds = useMemo<ReadonlySet<string>>(() => new Set(busyCabinetKey ? busyCabinetKey.split(',') : []), [busyCabinetKey]);

  // "Alarm olan" kabinler de modüllerden gelir (bkz. `AppModule.alertCabinetsQuery`); kabin durumunu değiştirmez, ayrı ikonla çizilir.
  const alertCabinetKey = useQueries({ queries: alertCabinetQueries, combine: combineBusyCabinetKey });
  const alertCabinetIds = useMemo<ReadonlySet<string>>(() => new Set(alertCabinetKey ? alertCabinetKey.split(',') : []), [alertCabinetKey]);

  const [selectedCabinet, setSelectedCabinet] = useState<CabinetDetailDto | null>(null);

  // Harita UNCONTROLLED: kamera React state'inde tutulmaz, yalnızca filtre/ilk açılışta örnek üzerinden
  // sürülür. Controlled modda her `move` olayı (pan sırasında ~60/sn) bu bileşeni baştan render ediyordu.
  const [map, setMap] = useState<MapRef | null>(null);

  const validCabinets = useMemo(() => cabinets.filter((c): c is LocatedCabinet => c.latitude != null && c.longitude != null), [cabinets]);

  // Harita üst solundaki arama/filtre paneli: seçilen kabne (ya da tümüne) göre haritayı ortalar.
  const [filterOpen, setFilterOpen] = useState(false);
  const [selectedCabinetId, setSelectedCabinetId] = useState(ALL_CABINETS);
  const filterRef = useRef<HTMLDivElement>(null);

  // Panel açıkken dışarıya (rozetler, harita, sayfanın geri kalanı) tıklanınca otomatik kapansın.
  useEffect(() => {
    if (!filterOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as Node;
      if (filterRef.current?.contains(target)) return;
      // `Select` açılır listesi `document.body`'ye PORTAL'lanıyor — filterRef'in DOM alt ağacında
      // DEĞİL. Bu kontrol olmadan bir seçeneğe tıklamak "dışarı tıklama" sayılıp paneli, seçim daha
      // gerçekleşmeden kapatıyordu.
      if (target instanceof Element && target.closest('[data-slot="select-content"]')) return;
      setFilterOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [filterOpen]);

  const cabinetOptions = useMemo(() => [...validCabinets].sort((a, b) => a.name.localeCompare(b.name, 'tr')), [validCabinets]);

  // Harita üst-sağındaki durum rozetleri: yalnızca AKTİF kabinler sayılır (isActive === true);
  // deaktive kabinler koordinatı olsun olmasın tamamen hariç. `cabinets` (tam liste) kullanılır,
  // `validCabinets` (yalnızca haritada iğnesi olanlar) DEĞİL — bu bir durum özeti, "haritada ne
  // çizili" özeti değil.
  const activeCabinets = useMemo(() => cabinets.filter(c => c.isActive), [cabinets]);
  const statusChips = useMemo(() => buildStatusChips(activeCabinets), [activeCabinets]);

  // Durum rozetine tıklanınca haritada YALNIZCA o duruma sahip (ve aktif) kabinler gösterilir ve
  // harita onları çerçeveye sığdırır; sayısı 0 olan rozette harita boşalır ve kamera yerinde kalır.
  // 'all' (Tümü) filtreyi tamamen kaldırır — haritada koordinatlı
  // TÜM kabinler (aktif/pasif fark etmeksizin) yeniden görünür, tıpkı ilk açılıştaki gibi.
  const [statusFilter, setStatusFilter] = useState<'all' | DeviceStatus | null>('all');

  const visibleCabinets = useMemo(() => {
    if (statusFilter === 'all') return validCabinets;
    return validCabinets.filter(c => c.isActive && c.deviceStatusId === statusFilter);
  }, [validCabinets, statusFilter]);

  // Kabinlerin TAMAMINI çerçeveye sığdırır. Eski "ağırlık merkezi + sabit zoom", iki şehre dağılmış kabinlerde
  // haritayı aradaki boş bir noktaya, sokak seviyesinde götürüyordu. Tek kabinde sonuç aynıdır (`maxZoom`).
  const fitCabinets = (list: LocatedCabinet[], animate: boolean) => {
    const bounds = cabinetBounds(list);
    if (map && bounds) map.fitBounds(bounds, { padding: FIT_PADDING, maxZoom: DEFAULT_ZOOM, duration: animate ? 600 : 0 });
  };

  const handleStatusFilter = (statusId: 'all' | DeviceStatus | null) => {
    setStatusFilter(statusId);
    fitCabinets(statusId === 'all' ? validCabinets : validCabinets.filter(c => c.isActive && c.deviceStatusId === statusId), true);
  };

  const handleFilter = () => {
    fitCabinets(selectedCabinetId === ALL_CABINETS ? validCabinets : validCabinets.filter(c => c.id === selectedCabinetId), true);
    // Seçilen kabin bir durum filtresi yüzünden haritada gizli kalmasın diye filtre sıfırlanır.
    setStatusFilter('all');
  };

  // Veri geldikten sonra haritayı kabinlere sığdır — yalnızca BİR KEZ; sonraki refetch'ler operatörün kaydırdığı
  // haritayı geri çekmesin. Bayrak state değil ref: effect'te setState yok, fazladan render kaskadı doğmaz.
  // Liste ilk açılışta boş gelip sonradan dolarsa da (ilk kabin eklendiğinde) bir kez çalışır.
  const autoFittedRef = useRef(false);
  useEffect(() => {
    if (autoFittedRef.current || !map) return;
    const bounds = cabinetBounds(validCabinets);
    if (!bounds) return;
    autoFittedRef.current = true;
    map.fitBounds(bounds, { padding: FIT_PADDING, maxZoom: DEFAULT_ZOOM, duration: 0 });
  }, [map, validCabinets]);

  // Tek popup: kabin başına bir popup örneği yerine kimlik tutulur ve kabin GÖRÜNÜR listeden türetilir — durum
  // filtresi kabini gizlerse popup kendiliğinden kapanır.
  const [popupCabinetId, setPopupCabinetId] = useState<string | null>(null);
  const popupCabinet = useMemo(
    () => (popupCabinetId ? (visibleCabinets.find(c => c.id === popupCabinetId) ?? null) : null),
    [visibleCabinets, popupCabinetId]
  );

  if (isLoading) {
    return <div className="flex h-full items-center justify-center text-muted-foreground">Harita yükleniyor...</div>;
  }

  return (
    // `dvh`: mobil tarayıcıda `vh` adres çubuğunun arkasını da sayar, sayfanın altı çubuğun altında kalırdı.
    <div className="flex flex-col h-[calc(100dvh-6rem)] w-full gap-4 p-2 sm:p-4 overflow-y-auto scrollbar-thin">
      {/* Harita kalan genişliği alır (`flex-1 min-w-0`); geniş ekranda detay paneli yanında sabit genişlikte
          durur, dar ekranda panel çekmeceye döndüğü için harita tam genişlikte kalır (bkz. `CabinetDetailPanel`). */}
      <div className="flex w-full gap-4 shrink-0 h-[55vh] min-h-[360px] md:min-h-[500px]">
        <div className="h-full min-w-0 flex-1">
          <div className="h-full w-full overflow-hidden rounded-lg border shadow-sm relative">
            {/* Arama düğmesi + durum rozetleri aynı sırada: rozetler düğmenin hemen sağında. Açılır
                filtre paneli artık `flex-col` istifiyle değil, düğmenin kendi `relative` çapasına göre
                `absolute top-full` ile konumlanıyor — böylece yanındaki rozet sırasını iteklemiyor.
                Satır sağ üstteki harita kontrollerinden önce biter (`right-12`); sığmayan rozetler yatay
                kayar — dar ekranda sağa taşıp kontrollerin altında kaybolmasınlar. Kap tıklamayı geçirir
                (`pointer-events-none`), yoksa rozetlerin sağındaki boş şerit haritayı sürüklemeyi engellerdi. */}
            <div className="pointer-events-none absolute top-2 right-12 left-2 z-10 flex items-start gap-2">
              <div className="pointer-events-auto relative shrink-0" ref={filterRef}>
                <Button
                  variant="secondary"
                  size="icon"
                  className="shadow-lg bg-primary/20 border-0 mr-4"
                  aria-label="Kabin filtrele"
                  title="Kabin filtrele"
                  onClick={() => setFilterOpen(open => !open)}
                >
                  <SearchIcon className="size-4" />
                </Button>

                {filterOpen && (
                  <div className="absolute top-full left-0 mt-2 w-64 overflow-hidden rounded-lg border bg-popover/80 text-popover-foreground shadow-md backdrop-blur-sm">
                    <div className="space-y-2 p-3">
                      <p className="text-xs font-medium text-muted-foreground">Kabin</p>
                      <Select value={selectedCabinetId} onValueChange={(value) => setSelectedCabinetId(value ?? ALL_CABINETS)}>
                        <SelectTrigger className="w-full">
                          <SelectValue>
                            {selectedCabinetId === ALL_CABINETS ? 'Tüm kabinler' : (cabinetOptions.find((c) => c.id === selectedCabinetId)?.name ?? 'Tüm kabinler')}
                          </SelectValue>
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={ALL_CABINETS}>Tüm kabinler</SelectItem>
                          {cabinetOptions.map((cabinet) => (
                            <SelectItem key={cabinet.id} value={cabinet.id}>
                              {cabinet.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>

                    {/* Form alanlarından ayrışsın diye ayrı bir "footer" şeridi — arka planı biraz daha
                        koyu, üstte ince bir çizgiyle bölünmüş: eylem burada bittiği belli olsun. */}
                    <div className="border-t bg-muted/40 p-2">
                      <Button size="sm" className="w-full" onClick={handleFilter}>
                        Filtrele
                      </Button>
                    </div>
                  </div>
                )}
              </div>

              {/* Durum rozetleri: tıklanınca haritada yalnızca o duruma sahip aktif kabinler
                  gösterilir ve harita onların ortasına çekilir. "Tümü" filtreyi kaldırıp haritayı
                  koordinatlı tüm kabinlerle (aktif/pasif fark etmeksizin) geri getirir; içindeki
                  sayı durum rozetlerinin toplamı (aktif kabin sayısı) ile aynıdır. Seçili filtre
                  `border-primary/40` ile hafifçe vurgulanır (tam opak `border-primary` gözü çok
                  yordu) ve `shadow-md` ile öne çıkar; kenarlık kalınlığı SEÇİLİ/SEÇİLİ-DEĞİL
                  arasında sabit (`border-2`) tutulur, aksi halde seçim değişince rozet boyutu
                  1px kayardı. */}
              <div
                className="pointer-events-auto flex min-w-0 items-center gap-1.5 overflow-x-auto scrollbar-thin px-0.5 pt-0.5 pb-1"
                role="group"
                aria-label="Kabin durum filtresi">
                <button
                  type="button"
                  onClick={() => handleStatusFilter('all')}
                  className={cn(
                    'flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-md px-2 py-1 text-[11px] font-medium',
                    statusFilter === 'all'
? 'border-1 bg-white dark:bg-white/80 dark:text-secondary shadow-sm'
                        : 'border-1 border-primary/5 bg-primary/5 dark:bg-secondary text-foreground shadow-sm hover:bg-accent'
                  )}
                  title={`Tümü: ${activeCabinets.length}`}
                >
                  <span>Tümü</span>
                  <span className="font-semibold tabular-nums">{activeCabinets.length}</span>
                </button>

                {statusChips.map((chip) => (
                  <button
                    type="button"
                    key={chip.key}
                    onClick={() => handleStatusFilter(chip.statusId)}
                    className={cn(
                      'flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-lg px-2 py-1 text-[11px] font-medium',
                      statusFilter === chip.statusId
                        ? 'border-1 bg-white dark:bg-white/80 dark:text-secondary shadow-sm'
                        : 'border-1 border-primary/5 bg-primary/5 dark:bg-secondary text-foreground shadow-sm hover:bg-accent'
                    )}
                    title={`${chip.label}: ${chip.count}`}
                  >
                    <span className={`size-2 shrink-0 rounded-full ${chip.dotColor}`} aria-hidden="true" />
                    <span>{chip.label}</span>
                    <span className="font-semibold tabular-nums">{chip.count}</span>
                  </button>
                ))}
              </div>
            </div>

            <Map ref={setMap} center={DEFAULT_CENTER} zoom={DEFAULT_ZOOM}>
              <MapControls position="top-right" showZoom showCompass showLocate showFullscreen />

              {/* Kabinler tek bir kümelenen WebGL katmanında çizilir (kabin başına DOM işaretçisi yok). Aynı
                  kabine yeniden tıklamak popup'ı kapatır — eski `MarkerPopup`'ın aç/kapa davranışı. Çift
                  tıklama detayı açar ve haritayı yakınlaştırmaz; iki tık popup'ı açıp kapattığından sonuç
                  eskisiyle aynı: popup kapalı, panel açık. */}
              <CabinetMapLayer
                cabinets={visibleCabinets}
                busyCabinetIds={busyCabinetIds}
                alertCabinetIds={alertCabinetIds}
                onCabinetClick={cabinet => setPopupCabinetId(current => (current === cabinet.id ? null : cabinet.id))}
                onCabinetDoubleClick={cabinet => {
                  setPopupCabinetId(null);
                  setSelectedCabinet(cabinet);
                }}
                onBackgroundClick={() => setPopupCabinetId(null)}
              />

              {/* `closeOnClick={false}`: kapanmayı MapLibre değil React state'i yönetir (bkz. `CabinetMapLayer`
                  tıklama işleyicisi). `offset`: popup 48 piksellik ikonun üstünde açılsın, üzerine binmesin. */}
              {popupCabinet && (
                <MapPopup
                  key={popupCabinet.id}
                  longitude={popupCabinet.longitude}
                  latitude={popupCabinet.latitude}
                  offset={28}
                  closeOnClick={false}
                  className="w-64 p-0"
                >
                  <div className="space-y-2 p-3">
                    <div>
                      <p className="pb-0.5 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                        {popupCabinet.companyName}
                      </p>
                      <h3 className="leading-tight font-semibold text-foreground">
                        {popupCabinet.name}
                      </h3>
                    </div>
                    <div className="mt-1 flex items-center gap-1.5 text-sm">
                      <Activity className="size-4 text-primary" />
                      <span className="font-medium">{deviceStatusLabel(popupCabinet.deviceStatusId)}</span>
                    </div>

                    <div className="mt-2 flex gap-2 border-t pt-2">
                      <Button size="sm" className="flex-1" onClick={() => setSelectedCabinet(popupCabinet)}>
                        <Info className="mr-1 size-3.5" />
                        Detay
                      </Button>
                    </div>
                  </div>
                </MapPopup>
              )}
            </Map>
          </div>
        </div>

        {selectedCabinet && <CabinetDetailPanel cabinet={selectedCabinet} onClose={() => setSelectedCabinet(null)} />}
      </div>

      <div className="shrink-0 w-full pb-4">
        <DashboardMetrics />
      </div>
    </div>
  );
}
