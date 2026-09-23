import { Suspense, useState, useMemo } from 'react';
import { useQueries, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { getCabinetList } from '@/api/cabinet';
import { cabinetKeys } from '@/api/query-keys';
import { Map, MapControls, MapMarker, MarkerContent, MarkerLabel, MarkerPopup } from '@/components/ui/map';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { X, Info, Activity, Clock, MapPin, SearchIcon } from 'lucide-react';
import type { CabinetDetailDto } from '@/models/cabinet';
import { DeviceStatus, DeviceStatusLabels } from '@/models/enums';
import CabinetIdleIcon from '@/assets/cabinet-idle-2.png';
import CabinetInProcessIcon from '@/assets/cabinet-inproces-2.png';
import { busyCabinetQueries, cabinetPanelSections, cabinetPanelTopActions } from '@/modules';
import { DashboardMetrics } from './dashboard-metrics';
import { formatUtcDateTime } from '@/lib/utils';

/** Modüllerin kimlik listelerini tek kümede birleştirir. Modül düzeyinde: kararlı referans, sonuç memolanır. */
function combineBusyCabinetIds(results: UseQueryResult<string[]>[]): ReadonlySet<string> {
  return new Set(results.flatMap(result => result.data ?? []));
}

/** Kabin filtresinde "hepsi" için sentinel — Base UI Select boş string'i "seçim yok" sayar. */
const ALL_CABINETS = 'all';

/** Konumlu kabinlerin ağırlık merkezi. Liste boşsa `null` — harita mevcut konumunda kalır. */
function averageCenter(cabinets: { latitude: number | null; longitude: number | null }[]): [number, number] | null {
  const valid = cabinets.filter((c): c is { latitude: number; longitude: number } => c.latitude != null && c.longitude != null);
  if (valid.length === 0) return null;

  const sumLng = valid.reduce((acc, c) => acc + c.longitude, 0);
  const sumLat = valid.reduce((acc, c) => acc + c.latitude, 0);
  return [sumLng / valid.length, sumLat / valid.length];
}

/**
 * Harita üstü durum rozetlerinin sabit sırası: Online → Warning → Critical → Maintenance →
 * Offline, en sonda "hiç telemetri alınmadı" kovası. `statusId: null` Offline'dan (0) AYRI bir
 * kovadır (bkz. `CabinetDetailDto.deviceStatusId` XML doc'u) — StatusDot ile aynı renk sözleşmesi
 * (bkz. `template-node.tsx`), yeni bir şema icat edilmedi.
 */
const STATUS_CHIP_DEFS: { key: string; statusId: DeviceStatus | null; label: string; dotColor: string }[] = [
  { key: 'online', statusId: DeviceStatus.Online, label: DeviceStatusLabels[DeviceStatus.Online], dotColor: 'bg-emerald-500' },
  { key: 'warning', statusId: DeviceStatus.Warning, label: DeviceStatusLabels[DeviceStatus.Warning], dotColor: 'bg-amber-500' },
  { key: 'critical', statusId: DeviceStatus.Critical, label: DeviceStatusLabels[DeviceStatus.Critical], dotColor: 'bg-red-500' },
  { key: 'maintenance', statusId: DeviceStatus.Maintenance, label: DeviceStatusLabels[DeviceStatus.Maintenance], dotColor: 'bg-sky-500' },
  { key: 'offline', statusId: DeviceStatus.Offline, label: DeviceStatusLabels[DeviceStatus.Offline], dotColor: 'bg-slate-500' },
  { key: 'none', statusId: null, label: 'Telemetri yok', dotColor: 'bg-gray-400' }
];

/** Aktif kabinleri `deviceStatusId`'ye göre kovalar; yalnızca sayısı 0'dan büyük kovalar döner. */
function buildStatusChips(activeCabinets: { deviceStatusId: DeviceStatus | null }[]) {
  return STATUS_CHIP_DEFS.map(def => ({
    ...def,
    count: activeCabinets.filter(c => c.deviceStatusId === def.statusId).length
  })).filter(chip => chip.count > 0);
}

export default function Home() {
  const { data: cabinets = [], isLoading } = useQuery({
    queryKey: cabinetKeys.list(),
    queryFn: getCabinetList
  });

  // "İşlem yapılan" kabinler modüllerden gelir (bkz. `AppModule.busyCabinetsQuery`); modül yoksa küme boştur.
  const busyCabinetIds = useQueries({ queries: busyCabinetQueries, combine: combineBusyCabinetIds });

  const [selectedCabinet, setSelectedCabinet] = useState<CabinetDetailDto | null>(null);

  const [viewport, setViewport] = useState<{ center: [number, number]; zoom: number; bearing?: number; pitch?: number }>({
    center: [35.2433, 38.9637],
    zoom: 17
  });

  const validCabinets = useMemo(() => cabinets.filter(c => c.latitude != null && c.longitude != null), [cabinets]);
  const center = useMemo(() => averageCenter(validCabinets) ?? ([35.2433, 38.9637] as [number, number]), [validCabinets]);

  const [hasAutoCentered, setHasAutoCentered] = useState(false);

  // Harita üst solundaki arama/filtre paneli: seçilen kabne (ya da tümüne) göre haritayı ortalar.
  const [filterOpen, setFilterOpen] = useState(false);
  const [selectedCabinetId, setSelectedCabinetId] = useState(ALL_CABINETS);

  const cabinetOptions = useMemo(() => [...validCabinets].sort((a, b) => a.name.localeCompare(b.name, 'tr')), [validCabinets]);

  // Harita üst-sağındaki durum rozetleri: yalnızca AKTİF kabinler sayılır (isActive === true);
  // deaktive kabinler koordinatı olsun olmasın tamamen hariç. `cabinets` (tam liste) kullanılır,
  // `validCabinets` (yalnızca haritada iğnesi olanlar) DEĞİL — bu bir durum özeti, "haritada ne
  // çizili" özeti değil.
  const activeCabinets = useMemo(() => cabinets.filter(c => c.isActive), [cabinets]);
  const statusChips = useMemo(() => buildStatusChips(activeCabinets), [activeCabinets]);

  const handleFilter = () => {
    const matches = selectedCabinetId === ALL_CABINETS ? validCabinets : validCabinets.filter(c => c.id === selectedCabinetId);
    const next = averageCenter(matches);
    if (next) setViewport(prev => ({ ...prev, center: next }));
  };

  // Data yüklendikten sonra haritayı kabinlerin ortasına çek (yalnızca ilk yüklemede;
  // sonraki refetch'ler operatörün kaydırdığı haritayı geri çekmesin). Effect yerine
  // render sırasında ayarlanır — effect içinde setState fazladan bir render kaskadı doğurur.
  if (!hasAutoCentered && validCabinets.length > 0) {
    setHasAutoCentered(true);
    setViewport(prev => ({ ...prev, center }));
  }

  if (isLoading) {
    return <div className="flex h-full items-center justify-center text-muted-foreground">Harita yükleniyor...</div>;
  }

  return (
    <div className="flex flex-col h-[calc(100vh-6rem)] w-full gap-4 p-4 overflow-y-auto">
      <div className="flex w-full gap-4 shrink-0 min-h-[500px] h-[55vh]">
        <div className={`transition-all duration-300 h-full ${selectedCabinet ? 'w-9/12' : 'w-full'}`}>
          <div className="h-full w-full overflow-hidden rounded-lg border shadow-sm relative">
            <div className="absolute top-2 left-2 z-10 flex flex-col items-start gap-2">
              <Button
                variant="secondary"
                size="icon"
                className="shadow-lg"
                aria-label="Kabin filtrele"
                title="Kabin filtrele"
                onClick={() => setFilterOpen(open => !open)}
              >
                <SearchIcon className="size-4" />
              </Button>

              {filterOpen && (
                <div className="w-64 overflow-hidden rounded-lg border bg-popover/80 text-popover-foreground shadow-md backdrop-blur-sm">
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

            {/* Kabin durum sayaçları: yakınlaştırma düğmelerinin (top-2 right-2, ~34px) hemen solunda.
                right-12 (48px) grubun sol kenarına göre 6px boşluk bırakır — MapControls'un kendi
                grupları arasındaki gap-1.5 ile tutarlı. Yalnızca aktif kabinler sayılır; Toplam rozeti
                noktasız ve farklı zeminle (bg-foreground/10) diğerlerinden ayrışır — light modda
                bg-popover/bg-background birbirinin aynısı olduğundan onlar ayrım için kullanılamazdı. */}
            <div className="absolute top-2 right-12 z-10 flex items-center gap-1.5" role="group" aria-label="Aktif kabin durum özeti">
              <div
                className="flex h-6 min-w-6 items-center justify-center rounded-full border border-foreground/20 bg-foreground/10 px-1.5 text-[11px] font-semibold tabular-nums text-foreground shadow-sm backdrop-blur-sm"
                title={`Toplam: ${activeCabinets.length}`}
              >
                {activeCabinets.length}
              </div>

              {statusChips.map((chip) => (
                <div
                  key={chip.key}
                  className="relative flex h-6 min-w-6 items-center justify-center rounded-full border border-border bg-background/70 px-1.5 text-[11px] font-semibold tabular-nums text-foreground shadow-sm backdrop-blur-sm"
                  title={`${chip.label}: ${chip.count}`}
                >
                  <span className={`absolute -top-1 -left-1 size-2 rounded-full border border-background ${chip.dotColor}`} aria-hidden="true" />
                  {chip.count}
                </div>
              ))}
            </div>

            <Map viewport={viewport} onViewportChange={(v) => setViewport(prev => ({ ...prev, ...v }))}>
              <MapControls position="top-right" showZoom showCompass showLocate showFullscreen />

              {validCabinets.map((cabinet) => (
                <MapMarker
                  key={cabinet.id}
                  longitude={cabinet.longitude as number}
                  latitude={cabinet.latitude as number}
                >
                  <MarkerContent>
                    {/* `w-auto`: iki görselin en-boy oranı farklı, sabit genişlik işlem ikonunu ezerdi. */}
                    <img
                      src={busyCabinetIds.has(cabinet.id) ? CabinetInProcessIcon : CabinetIdleIcon}
                      alt={busyCabinetIds.has(cabinet.id) ? 'Kabin (işlem yapılıyor)' : 'Kabin'}
                      className="h-12 w-auto cursor-pointer transition-transform hover:scale-120 drop-shadow-lg"
                      onDoubleClick={(e) => {
                        // Haritanın kendi çift tıklama davranışı (yakınlaştırma) tetiklenmesin —
                        // burada çift tıklamanın anlamı "detayı direkt aç", yakınlaştırma değil.
                        e.stopPropagation();
                        setSelectedCabinet(cabinet);
                      }}
                    />
                    <MarkerLabel position="bottom" className='bg-white dark:bg-stone-800'>{cabinet.name}</MarkerLabel>
                  </MarkerContent>
                  <MarkerPopup className="w-64 p-0">
                    <div className="space-y-2 p-3">
                      <div>
                        <p className="pb-0.5 text-[11px] font-medium tracking-wide text-muted-foreground uppercase">
                          {cabinet.companyName}
                        </p>
                        <h3 className="leading-tight font-semibold text-foreground">
                          {cabinet.name}
                        </h3>
                      </div>
                      <div className="mt-1 flex items-center gap-1.5 text-sm">
                        <Activity className="size-4 text-primary" />
                        <span className="font-medium">{cabinet.deviceStatusName || 'Bilinmiyor'}</span>
                      </div>

                      <div className="mt-2 flex gap-2 border-t pt-2">
                        <Button size="sm" className="flex-1" onClick={() => setSelectedCabinet(cabinet)}>
                          <Info className="mr-1 size-3.5" />
                          Detay
                        </Button>
                      </div>
                    </div>
                  </MarkerPopup>
                </MapMarker>
              ))}
            </Map>
          </div>
        </div>

        {selectedCabinet && (
          <div className="w-3/12 transition-all duration-300">
            <div className="flex h-full flex-col rounded-lg border bg-card text-card-foreground shadow-sm">
              <div className="flex items-center justify-between border-b p-4">
                <h2 className="text-lg font-semibold line-clamp-1" title={selectedCabinet.name}>{selectedCabinet.name}</h2>
                <Button variant="ghost" size="icon" onClick={() => setSelectedCabinet(null)}>
                  <X className="size-4" />
                </Button>
              </div>

              <div className="flex-1 space-y-4 overflow-y-auto p-4">
                {/* Açık modüllerin panel EN ÜST eylemleri (bkz. `AppModule.CabinetPanelTopAction`); künyeden
                    önce gelir — modül yoksa liste boştur ve panel doğrudan "Firma"yla başlar. */}
                {cabinetPanelTopActions.map(({ key, Component }) => (
                  <Suspense key={key} fallback={<Skeleton className="h-12 w-full rounded-lg" />}>
                    <Component cabinetId={selectedCabinet.id} />
                  </Suspense>
                ))}

                <div className="space-y-1">
                  <p className="text-sm font-medium text-muted-foreground">Firma</p>
                  <p className="text-sm">{selectedCabinet.companyName}</p>
                </div>

                <div className="space-y-1">
                  <p className="text-sm font-medium text-muted-foreground">Durum</p>
                  <p className="flex items-center gap-2 text-sm">
                    <span className={`size-2.5 rounded-full ${selectedCabinet.isActive ? 'bg-green-500' : 'bg-red-500'}`} />
                    {selectedCabinet.deviceStatusName || 'Bilinmiyor'}
                  </p>
                </div>

                <div className="space-y-1">
                  <p className="text-sm font-medium text-muted-foreground">Ağ İP</p>
                  <p className="text-sm">{selectedCabinet.networkIp || '-'}</p>
                </div>

                <div className="space-y-1">
                  <p className="text-sm font-medium text-muted-foreground">GSM İP</p>
                  <p className="text-sm">{selectedCabinet.gsmIp || '-'}</p>
                </div>

                <div className="space-y-1">
                  <p className="text-sm font-medium text-muted-foreground">Lokasyon</p>
                  <p className="flex items-start gap-1.5 text-sm">
                    <MapPin className="mt-0.5 size-3.5 shrink-0" />
                    <span>{selectedCabinet.locationDescription || '-'}</span>
                  </p>
                </div>

                {selectedCabinet.updateDateUtc && (
                  <div className="space-y-1 border-t pt-4">
                    <p className="text-sm font-medium text-muted-foreground">Son Güncelleme</p>
                    <p className="flex items-center gap-1.5 text-sm">
                      <Clock className="size-3.5" />
                      {formatUtcDateTime(selectedCabinet.updateDateUtc)}
                    </p>
                  </div>
                )}

                {/* Açık modüllerin bu kabine dair bölümleri (bkz. `AppModule.CabinetPanelSection`);
                    modül yoksa liste boştur ve panel yalnızca künyeden ibaret kalır. */}
                {cabinetPanelSections.map(({ key, Component }) => (
                  <Suspense key={key} fallback={<Skeleton className="h-28 w-full rounded-lg" />}>
                    <Component cabinetId={selectedCabinet.id} />
                  </Suspense>
                ))}
              </div>
            </div>
          </div>
        )}
      </div>

      <div className="shrink-0 w-full pb-4">
        <DashboardMetrics />
      </div>
    </div>
  );
}
