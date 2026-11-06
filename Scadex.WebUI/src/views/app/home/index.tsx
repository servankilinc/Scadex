import { useEffect, useRef, useState, useMemo, type ReactNode } from 'react';
import { useQueries, useQuery, type UseQueryResult } from '@tanstack/react-query';
import type { LngLatBoundsLike } from 'maplibre-gl';
import { getAutomationList } from '@/api/automation';
import { automationKeys } from '@/api/query-keys';
import { Map, MapControls, MapPopup, type MapRef } from '@/components/ui/map';
import { Button } from '@/components/ui/button';
import { Combobox, ComboboxContent, ComboboxEmpty, ComboboxInput, ComboboxItem, ComboboxList } from '@/components/ui/combobox';
import { Info, Activity, SearchIcon } from 'lucide-react';
import type { AutomationDetailDto } from '@/models/automation';
import { DeviceStatus, deviceStatusLabel } from '@/models/enums';
import { alertAutomationQueries, busyAutomationQueries } from '@/modules';
import AutomationInProcessIcon from '@/assets/cabinet-inproces-2.png';
import { useAutomationOverviewLive } from '@/hooks/use-automation-overview-live';
import { BASEMAP_STYLES } from '@/lib/map-basemap';
import { AutomationDetailPanel } from './automation-detail-panel';
import { AutomationMapLayer, type LocatedAutomation } from './automation-map-layer';
import { DashboardMetrics } from './dashboard-metrics';
import { cn, toSearchKey } from '@/lib/utils';

/**
 * Modüllerin kimlik listelerini sıralı, tekil bir anahtara indirger. Modül düzeyinde: kararlı referans.
 * `Set` değil string döner: yoklama her 10 sn'de yeni sonuç nesneleri üretir, içerik aynıysa anahtar da
 * aynı kalır ve harita kaynağı boşuna yeniden yüklenmez (bkz. `busyAutomationIds`).
 */
function combineBusyAutomationKey(results: UseQueryResult<string[]>[]): string {
  return [...new Set(results.flatMap(result => result.data ?? []))].sort().join(',');
}

/** Konumlu otomasyon yokken haritanın açıldığı yer. */
const DEFAULT_CENTER: [number, number] = [35.2433, 38.9637];
/** İlk zoom ve `fitBounds`'un en fazla ineceği zoom: tek otomasyon seçilince eski "merkeze + zoom 17" ile aynı sonuç. */
const DEFAULT_ZOOM = 17;
/** Sol üstte rozet satırı, sağ üstte kontroller haritanın üzerinde durur; sığdırılan otomasyonlar altlarında kalmasın. */
const FIT_PADDING = { top: 64, bottom: 48, left: 48, right: 64 };

/** Otomasyonların sınır kutusu. Liste boşsa `null` — harita mevcut konumunda kalır. */
function automationBounds(automations: LocatedAutomation[]): LngLatBoundsLike | null {
  if (automations.length === 0) return null;

  let minLng = Infinity;
  let minLat = Infinity;
  let maxLng = -Infinity;
  let maxLat = -Infinity;
  for (const automation of automations) {
    minLng = Math.min(minLng, automation.longitude);
    minLat = Math.min(minLat, automation.latitude);
    maxLng = Math.max(maxLng, automation.longitude);
    maxLat = Math.max(maxLat, automation.latitude);
  }
  return [
    [minLng, minLat],
    [maxLng, maxLat]
  ];
}

/**
 * Harita üstü durum rozetlerinin sabit sırası: Online → Warning → Maintenance → Offline (kırmızı — en kötü durum;
 * Critical 2026-10-12'de kaldırıldı), en sonda "Bilinmiyor" kovası. `statusId: null` Offline'dan (0) AYRI bir kovadır — "canlılık
 * kanıtı yok" demektir (bkz. `deviceStatusLabel`) — StatusDot ile aynı renk sözleşmesi
 * (bkz. `template-node.tsx`), yeni bir şema icat edilmedi.
 */
const STATUS_CHIP_DEFS: { key: string; statusId: DeviceStatus | null; label: string; dotColor: string }[] = [
  { key: 'online', statusId: DeviceStatus.Online, label: deviceStatusLabel(DeviceStatus.Online), dotColor: 'bg-emerald-500' },
  { key: 'warning', statusId: DeviceStatus.Warning, label: deviceStatusLabel(DeviceStatus.Warning), dotColor: 'bg-amber-500' },
  { key: 'maintenance', statusId: DeviceStatus.Maintenance, label: deviceStatusLabel(DeviceStatus.Maintenance), dotColor: 'bg-sky-500' },
  { key: 'offline', statusId: DeviceStatus.Offline, label: deviceStatusLabel(DeviceStatus.Offline), dotColor: 'bg-red-500' },
  { key: 'none', statusId: null, label: deviceStatusLabel(null), dotColor: 'bg-gray-400' }
];

/**
 * Aktif otomasyonları `deviceStatusId`'ye göre kovalar. Sayısı 0 olan kovalar da döner: rozet satırı her zaman
 * aynı altı rozeti gösterir, durum dağılımı değişince rozetler yer değiştirmez.
 */
function buildStatusChips(activeAutomations: { deviceStatusId: DeviceStatus | null }[]) {
  return STATUS_CHIP_DEFS.map(def => ({
    ...def,
    count: activeAutomations.filter(c => c.deviceStatusId === def.statusId).length
  }));
}

/**
 * Haritanın tek seçimli filtresi: `'all'`, bir otomasyon durumu (`null` = Bilinmiyor) ya da modülden gelen kimlik listesi
 * (`'busy'` işlem yapılan). Durum ile işlem birlikte seçilmez.
 */
type MapFilter = 'all' | 'busy' | DeviceStatus | null;

/** Yalnızca AKTİF otomasyonlar eşleşir — rozet sayılarıyla aynı taban (`validAutomations` pasifleri zaten dışarıda bırakır). */
function matchesMapFilter(automation: AutomationDetailDto, filter: MapFilter, busyAutomationIds: ReadonlySet<string>): boolean {
  if (!automation.isActive) return false;
  if (filter === 'all') return true;
  if (filter === 'busy') return busyAutomationIds.has(automation.id);
  return automation.deviceStatusId === filter;
}

/**
 * Modül rozetleri — yalnızca bir modül ilgili sorguyu verdiyse (`AppModule.busyAutomationsQuery`); modül kapalıyken rozet
 * satırı değişmez. Çekirdek "işlem"in ne olduğunu bilmez, kimlikleri sayar. Nokta yerine haritadaki işaretle eşleşen
 * simge: amber nokta Uyarı rozetiyle karışırdı. Statik: modül listesi çalışırken değişmez. Alarm için rozet BİLEREK
 * yok (2026-09-28, kullanılmıyordu); alarmlı otomasyon haritada kırmızı "!" ikonuyla görünmeye devam eder.
 */
const MODULE_CHIP_DEFS: { filter: 'busy'; label: string; icon: ReactNode }[] =
  busyAutomationQueries.length > 0
    ? [{ filter: 'busy', label: 'İşlemde', icon: <img src={AutomationInProcessIcon} alt="" className="h-3.5 w-auto shrink-0" aria-hidden="true" /> }]
    : [];

/** Durum ve modül rozetlerinin ortak görünümü. Kenarlık kalınlığı seçimde sabit: rozet boyutu kaymasın. */
function chipClassName(selected: boolean) {
  return cn(
    'flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-lg px-2 py-1 text-[11px] font-medium',
    selected
      ? 'border-1 bg-white dark:bg-white/80 dark:text-secondary shadow-sm'
      : 'border-1 border-primary/5 bg-primary/5 dark:bg-secondary text-foreground shadow-sm hover:bg-accent'
  );
}

export default function Home() {
  const { data: automations = [], isLoading } = useQuery({
    queryKey: automationKeys.list(),
    queryFn: getAutomationList
  });
  // Otomasyon durumu değişince liste tazelenir: harita rozetleri ve popup sayfa yenilenmeden güncel kalır.
  useAutomationOverviewLive();

  // "İşlem yapılan" otomasyonlar modüllerden gelir (bkz. `AppModule.busyAutomationsQuery`); modül yoksa küme boştur.
  // Küme anahtar üzerinden memolanır: içerik değişmedikçe referans sabit, harita kaynağı yeniden yüklenmez.
  const busyAutomationKey = useQueries({ queries: busyAutomationQueries, combine: combineBusyAutomationKey });
  const busyAutomationIds = useMemo<ReadonlySet<string>>(() => new Set(busyAutomationKey ? busyAutomationKey.split(',') : []), [busyAutomationKey]);

  // "Alarm olan" otomasyonlar da modüllerden gelir (bkz. `AppModule.alertAutomationsQuery`); otomasyon durumunu değiştirmez, ayrı ikonla çizilir.
  const alertAutomationKey = useQueries({ queries: alertAutomationQueries, combine: combineBusyAutomationKey });
  const alertAutomationIds = useMemo<ReadonlySet<string>>(() => new Set(alertAutomationKey ? alertAutomationKey.split(',') : []), [alertAutomationKey]);

  const [selectedAutomation, setSelectedAutomation] = useState<AutomationDetailDto | null>(null);

  // Harita UNCONTROLLED: kamera React state'inde tutulmaz, yalnızca filtre/ilk açılışta örnek üzerinden
  // sürülür. Controlled modda her `move` olayı (pan sırasında ~60/sn) bu bileşeni baştan render ediyordu.
  const [map, setMap] = useState<MapRef | null>(null);

  // Haritaya yalnızca AKTİF ve konumu olan otomasyonlar gelir. Pasif otomasyon haritada hiçbir filtrede görünmez: rozet sayıları
  // aktifleri sayar, pasif otomasyonun durumu da artık hesaplanmadığı için bayattır (`RecalculateAsync` pasife dokunmaz).
  const validAutomations = useMemo(
    () => automations.filter((c): c is LocatedAutomation => c.isActive && c.latitude != null && c.longitude != null),
    [automations]
  );

  // Harita üst solundaki arama/filtre paneli: seçilen kabne (ya da tümüne) göre haritayı ortalar.
  const [filterOpen, setFilterOpen] = useState(false);
  /** `null` = tüm otomasyonlar (combobox boş). */
  const [selectedAutomationId, setSelectedAutomationId] = useState<string | null>(null);
  const filterRef = useRef<HTMLDivElement>(null);

  // Panel açıkken dışarıya (rozetler, harita, sayfanın geri kalanı) tıklanınca otomatik kapansın.
  useEffect(() => {
    if (!filterOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as Node;
      if (filterRef.current?.contains(target)) return;
      // Combobox açılır listesi `document.body`'ye PORTAL'lanıyor — filterRef'in DOM alt ağacında
      // DEĞİL. Bu kontrol olmadan bir seçeneğe tıklamak "dışarı tıklama" sayılıp paneli, seçim daha
      // gerçekleşmeden kapatıyordu.
      if (target instanceof Element && target.closest('[data-slot="combobox-content"]')) return;
      setFilterOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [filterOpen]);

  const automationOptions = useMemo(() => [...validAutomations].sort((a, b) => a.name.localeCompare(b.name, 'tr')), [validAutomations]);
  const filterAutomation = automationOptions.find(c => c.id === selectedAutomationId) ?? null;

  // Base UI'ın varsayılan filtresi (`Intl.Collator`) "kizilay" yazınca "Kızılay"ı bulmuyor — bkz. `toSearchKey`.
  // Kutuda seçili otomasyonun adı dururken liste yalnızca ona daralmasın: varsayılan tek seçim filtresi de böyle davranır.
  const matchesAutomationQuery = (automation: LocatedAutomation, query: string) =>
    query === filterAutomation?.name || toSearchKey(automation.name).includes(toSearchKey(query));

  // Harita üst-sağındaki durum rozetleri: yalnızca AKTİF otomasyonlar sayılır (isActive === true);
  // deaktive otomasyonlar koordinatı olsun olmasın tamamen hariç. `automations` (tam liste) kullanılır,
  // `validAutomations` (yalnızca haritada iğnesi olanlar) DEĞİL — bu bir durum özeti, "haritada ne
  // çizili" özeti değil.
  const activeAutomations = useMemo(() => automations.filter(c => c.isActive), [automations]);
  const statusChips = useMemo(() => buildStatusChips(activeAutomations), [activeAutomations]);

  // Durum rozetine tıklanınca haritada YALNIZCA o duruma sahip (ve aktif) otomasyonlar gösterilir ve
  // harita onları çerçeveye sığdırır; sayısı 0 olan rozette harita boşalır ve kamera yerinde kalır.
  // 'all' (Tümü) filtreyi tamamen kaldırır — haritada koordinatlı tüm AKTİF otomasyonlar yeniden görünür, tıpkı ilk
  // açılıştaki gibi.
  // Modül rozetleri de aynı filtreyi sürer: "İşlemde" seçiliyken oturum kapanınca otomasyon haritadan düşer.
  const [mapFilter, setMapFilter] = useState<MapFilter>('all');

  const visibleAutomations = useMemo(() => {
    if (mapFilter === 'all') return validAutomations;
    return validAutomations.filter(c => matchesMapFilter(c, mapFilter, busyAutomationIds));
  }, [validAutomations, mapFilter, busyAutomationIds]);

  const moduleChips = useMemo(
    () =>
      MODULE_CHIP_DEFS.map(def => ({
        ...def,
        count: activeAutomations.filter(c => matchesMapFilter(c, def.filter, busyAutomationIds)).length
      })),
    [activeAutomations, busyAutomationIds]
  );

  // Otomasyonların TAMAMINI çerçeveye sığdırır. Eski "ağırlık merkezi + sabit zoom", iki şehre dağılmış otomasyonlarda
  // haritayı aradaki boş bir noktaya, sokak seviyesinde götürüyordu. Tek otomasyonda sonuç aynıdır (`maxZoom`).
  const fitAutomations = (list: LocatedAutomation[], animate: boolean) => {
    const bounds = automationBounds(list);
    if (map && bounds) map.fitBounds(bounds, { padding: FIT_PADDING, maxZoom: DEFAULT_ZOOM, duration: animate ? 600 : 0 });
  };

  const handleMapFilter = (filter: MapFilter) => {
    setMapFilter(filter);
    fitAutomations(validAutomations.filter(c => matchesMapFilter(c, filter, busyAutomationIds)), true);
  };

  const handleFilter = () => {
    fitAutomations(filterAutomation ? [filterAutomation] : validAutomations, true);
    // Seçilen otomasyon bir durum filtresi yüzünden haritada gizli kalmasın diye filtre sıfırlanır.
    setMapFilter('all');
  };

  // Veri geldikten sonra haritayı otomasyonlara sığdır — yalnızca BİR KEZ; sonraki refetch'ler operatörün kaydırdığı
  // haritayı geri çekmesin. Bayrak state değil ref: effect'te setState yok, fazladan render kaskadı doğmaz.
  // Liste ilk açılışta boş gelip sonradan dolarsa da (ilk otomasyon eklendiğinde) bir kez çalışır.
  const autoFittedRef = useRef(false);
  useEffect(() => {
    if (autoFittedRef.current || !map) return;
    const bounds = automationBounds(validAutomations);
    if (!bounds) return;
    autoFittedRef.current = true;
    map.fitBounds(bounds, { padding: FIT_PADDING, maxZoom: DEFAULT_ZOOM, duration: 0 });
  }, [map, validAutomations]);

  // Tek popup: otomasyon başına bir popup örneği yerine kimlik tutulur ve otomasyon GÖRÜNÜR listeden türetilir — durum
  // filtresi otomasyonu gizlerse popup kendiliğinden kapanır.
  const [popupAutomationId, setPopupAutomationId] = useState<string | null>(null);
  const popupAutomation = useMemo(
    () => (popupAutomationId ? (visibleAutomations.find(c => c.id === popupAutomationId) ?? null) : null),
    [visibleAutomations, popupAutomationId]
  );

  if (isLoading) {
    return <div className="flex h-full items-center justify-center text-muted-foreground">Harita yükleniyor...</div>;
  }

  return (
    // `dvh`: mobil tarayıcıda `vh` adres çubuğunun arkasını da sayar, sayfanın altı çubuğun altında kalırdı.
    <div className="flex flex-col h-[calc(100dvh-6rem)] w-full gap-4 p-2 sm:p-4 overflow-y-auto scrollbar-thin">
      {/* Harita kalan genişliği alır (`flex-1 min-w-0`); geniş ekranda detay paneli yanında sabit genişlikte
          durur, dar ekranda panel çekmeceye döndüğü için harita tam genişlikte kalır (bkz. `AutomationDetailPanel`). */}
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
                  aria-label="Otomasyon filtrele"
                  title="Otomasyon filtrele"
                  onClick={() => setFilterOpen(open => !open)}
                >
                  <SearchIcon className="size-4" />
                </Button>

                {filterOpen && (
                  <div className="absolute top-full left-0 mt-2 w-64 overflow-hidden rounded-lg border bg-popover/80 text-popover-foreground shadow-md backdrop-blur-sm">
                    <div className="space-y-2 p-3">
                      <p className="text-xs font-medium text-muted-foreground">Otomasyon</p>
                      {/* Boş kutu = tüm otomasyonlar; seçimi temizle düğmesi oraya geri döndürür. Yazarken ilk eşleşme
                          vurgulanır (`autoHighlight`): Enter onu seçer. */}
                      <Combobox
                        items={automationOptions}
                        value={filterAutomation}
                        onValueChange={automation => setSelectedAutomationId(automation?.id ?? null)}
                        itemToStringLabel={automation => automation.name}
                        isItemEqualToValue={(a, b) => a.id === b.id}
                        filter={matchesAutomationQuery}
                        autoHighlight
                      >
                        <ComboboxInput className="w-full" placeholder="Tüm otomasyonlar — ara…" showClear={filterAutomation !== null} autoFocus />
                        <ComboboxContent>
                          <ComboboxEmpty>Otomasyon bulunamadı.</ComboboxEmpty>
                          <ComboboxList className="scrollbar-thin">
                            {(automation: LocatedAutomation) => (
                              <ComboboxItem key={automation.id} value={automation}>
                                {automation.name}
                              </ComboboxItem>
                            )}
                          </ComboboxList>
                        </ComboboxContent>
                      </Combobox>
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

              {/* Durum rozetleri: tıklanınca haritada yalnızca o duruma sahip aktif otomasyonlar
                  gösterilir ve harita onların ortasına çekilir. "Tümü" filtreyi kaldırıp haritayı
                  koordinatlı tüm aktif otomasyonlarla geri getirir (pasifler haritaya hiç gelmez); içindeki
                  sayı durum rozetlerinin toplamı (aktif otomasyon sayısı) ile aynıdır. Seçili filtre
                  `border-primary/40` ile hafifçe vurgulanır (tam opak `border-primary` gözü çok
                  yordu) ve `shadow-md` ile öne çıkar; kenarlık kalınlığı SEÇİLİ/SEÇİLİ-DEĞİL
                  arasında sabit (`border-2`) tutulur, aksi halde seçim değişince rozet boyutu
                  1px kayardı. */}
              <div
                className="pointer-events-auto flex min-w-0 items-center gap-1.5 overflow-x-auto scrollbar-thin px-0.5 pt-0.5 pb-1"
                role="group"
                aria-label="Otomasyon durum filtresi">
                <button
                  type="button"
                  onClick={() => handleMapFilter('all')}
                  className={cn(
                    'flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-md px-2 py-1 text-[11px] font-medium',
                    mapFilter === 'all'
? 'border-1 bg-white dark:bg-white/80 dark:text-secondary shadow-sm'
                        : 'border-1 border-primary/5 bg-primary/5 dark:bg-secondary text-foreground shadow-sm hover:bg-accent'
                  )}
                  title={`Tümü: ${activeAutomations.length}`}
                >
                  <span>Tümü</span>
                  <span className="font-semibold tabular-nums">{activeAutomations.length}</span>
                </button>

                {statusChips.map((chip) => (
                  <button
                    type="button"
                    key={chip.key}
                    onClick={() => handleMapFilter(chip.statusId)}
                    className={chipClassName(mapFilter === chip.statusId)}
                    title={`${chip.label}: ${chip.count}`}
                  >
                    <span className={`size-2 shrink-0 rounded-full ${chip.dotColor}`} aria-hidden="true" />
                    <span>{chip.label}</span>
                    <span className="font-semibold tabular-nums">{chip.count}</span>
                  </button>
                ))}

                {/* Modül rozetleri (İşlemde): otomasyon durumundan bağımsız bir eksen, ince çizgiyle ayrılır. */}
                {moduleChips.length > 0 && <span className="mx-0.5 w-px shrink-0 self-stretch bg-border" aria-hidden="true" />}
                {moduleChips.map((chip) => (
                  <button
                    type="button"
                    key={chip.filter}
                    onClick={() => handleMapFilter(chip.filter)}
                    className={chipClassName(mapFilter === chip.filter)}
                    title={`${chip.label}: ${chip.count}`}
                  >
                    {chip.icon}
                    <span>{chip.label}</span>
                    <span className="font-semibold tabular-nums">{chip.count}</span>
                  </button>
                ))}
              </div>
            </div>

            <Map ref={setMap} styles={BASEMAP_STYLES} center={DEFAULT_CENTER} zoom={DEFAULT_ZOOM}>
              <MapControls position="top-right" showZoom showCompass showLocate showFullscreen />

              {/* Otomasyonlar tek bir kümelenen WebGL katmanında çizilir (otomasyon başına DOM işaretçisi yok). Aynı
                  otomasyona yeniden tıklamak popup'ı kapatır — eski `MarkerPopup`'ın aç/kapa davranışı. Çift
                  tıklama detayı açar ve haritayı yakınlaştırmaz; iki tık popup'ı açıp kapattığından sonuç
                  eskisiyle aynı: popup kapalı, panel açık. */}
              <AutomationMapLayer
                automations={visibleAutomations}
                busyAutomationIds={busyAutomationIds}
                alertAutomationIds={alertAutomationIds}
                onAutomationClick={automation => setPopupAutomationId(current => (current === automation.id ? null : automation.id))}
                onAutomationDoubleClick={automation => {
                  setPopupAutomationId(null);
                  setSelectedAutomation(automation);
                }}
                onBackgroundClick={() => setPopupAutomationId(null)}
              />

              {/* `closeOnClick={false}`: kapanmayı MapLibre değil React state'i yönetir (bkz. `AutomationMapLayer`
                  tıklama işleyicisi). `offset`: popup 48 piksellik ikonun üstünde açılsın, üzerine binmesin. */}
              {popupAutomation && (
                <MapPopup
                  key={popupAutomation.id}
                  longitude={popupAutomation.longitude}
                  latitude={popupAutomation.latitude}
                  offset={28}
                  closeOnClick={false}
                  className="w-64 p-0"
                >
                  <div className="space-y-2 p-3">
                    <div>
                      <p className="pb-0.5 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                        {popupAutomation.companyName}
                      </p>
                      <h3 className="leading-tight font-semibold text-foreground">
                        {popupAutomation.name}
                      </h3>
                    </div>
                    <div className="mt-1 flex items-center gap-1.5 text-sm">
                      <Activity className="size-4 text-primary" />
                      <span className="font-medium">{deviceStatusLabel(popupAutomation.deviceStatusId)}</span>
                    </div>

                    <div className="mt-2 flex gap-2 border-t pt-2">
                      <Button size="sm" className="flex-1" onClick={() => setSelectedAutomation(popupAutomation)}>
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

        {selectedAutomation && <AutomationDetailPanel automation={selectedAutomation} onClose={() => setSelectedAutomation(null)} />}
      </div>

      <div className="shrink-0 w-full pb-4">
        <DashboardMetrics />
      </div>
    </div>
  );
}
