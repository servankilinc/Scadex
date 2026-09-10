import { useState, useMemo, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getCabinetList } from '@/api/cabinet';
import { cabinetKeys } from '@/api/query-keys';
import { Map, MapControls, MapMarker, MarkerContent, MarkerLabel, MarkerPopup } from '@/components/ui/map';
import { Button } from '@/components/ui/button';
import { X, Info, Activity, Clock, MapPin } from 'lucide-react';
import type { CabinetDetailDto } from '@/models/cabinet';
import CabinetIdleIcon from '@/assets/cabinet-idle-2.png';
import { DashboardMetrics } from './dashboard-metrics';

export default function Home() {
  const { data: cabinets = [], isLoading } = useQuery({
    queryKey: cabinetKeys.list(),
    queryFn: getCabinetList
  });

  const [selectedCabinet, setSelectedCabinet] = useState<CabinetDetailDto | null>(null);

  const [viewport, setViewport] = useState<{ center: [number, number]; zoom: number; bearing?: number; pitch?: number }>({
    center: [35.2433, 38.9637],
    zoom: 6
  });

  const { center, validCabinets } = useMemo(() => {
    const valid = cabinets.filter(c => c.latitude != null && c.longitude != null);
    if (valid.length === 0) {
      return { center: [35.2433, 38.9637] as [number, number], validCabinets: [] };
    }

    const sumLng = valid.reduce((acc, c) => acc + (c.longitude as number), 0);
    const sumLat = valid.reduce((acc, c) => acc + (c.latitude as number), 0);

    return {
      center: [sumLng / valid.length, sumLat / valid.length] as [number, number],
      validCabinets: valid
    };
  }, [cabinets]);

  // Data yüklendikten sonra haritayı kabinlerin ortasına çek
  useEffect(() => {
    if (validCabinets.length > 0) {
      setViewport(prev => ({ ...prev, center }));
    }
  }, [center, validCabinets.length]);

  if (isLoading) {
    return <div className="flex h-full items-center justify-center text-muted-foreground">Harita yükleniyor...</div>;
  }

  return (
    <div className="flex flex-col h-[calc(100vh-6rem)] w-full gap-4 p-4 overflow-y-auto">
      <div className="flex w-full gap-4 shrink-0 min-h-[500px] h-[55vh]">
        <div className={`transition-all duration-300 h-full ${selectedCabinet ? 'w-9/12' : 'w-full'}`}>
          <div className="h-full w-full overflow-hidden rounded-lg border shadow-sm relative">
            <Map viewport={viewport} onViewportChange={(v) => setViewport(prev => ({ ...prev, ...v }))}>
              <MapControls position="top-right" showZoom showCompass showLocate showFullscreen />

              {validCabinets.map((cabinet) => (
                <MapMarker
                  key={cabinet.id}
                  longitude={cabinet.longitude as number}
                  latitude={cabinet.latitude as number}
                >
                  <MarkerContent>
                    <img
                      src={CabinetIdleIcon}
                      alt="Cabinet"
                      className="h-12 w-10 cursor-pointer transition-transform hover:scale-120 drop-shadow-lg"
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
                      {new Date(selectedCabinet.updateDateUtc).toLocaleString()}
                    </p>
                  </div>
                )}
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
