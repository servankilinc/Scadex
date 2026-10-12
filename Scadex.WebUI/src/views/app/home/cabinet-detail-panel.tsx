import { Suspense } from 'react';
import { Clock, MapPin, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { useMediaQuery } from '@/hooks/use-media-query';
import { PanelField, PanelSection } from '@/components/custom/panel-section';
import type { CabinetDetailDto } from '@/models/cabinet';
import { cabinetPanelSections, cabinetPanelTopActions } from '@/modules';
import { formatUtcDateTime } from '@/lib/utils';
import { CabinetStatusSection } from './cabinet-status-section';

/**
 * Tailwind `lg`. Altında panel haritanın yanına sığmaz — yüzde genişlikle 375 piksellik ekranda 86 piksele
 * kadar sıkışıyordu — bu yüzden sağdan açılan bir çekmeceye (Sheet) döner.
 */
const SIDE_PANEL_QUERY = '(min-width: 1024px)';

type CabinetDetailPanelProps = {
  cabinet: CabinetDetailDto;
  onClose: () => void;
};

/**
 * Seçili kabinin detayı. Geniş ekranda haritanın sağında sabit genişlikli bir panel, dar ekranda sağdan açılan
 * bir çekmece; içerik iki yerleşimde de aynıdır. Yalnızca biri render edilir — modül bölümleri iki kez mount
 * olmasın.
 */
export function CabinetDetailPanel({ cabinet, onClose }: CabinetDetailPanelProps) {
  const isSidePanel = useMediaQuery(SIDE_PANEL_QUERY);

  if (!isSidePanel) {
    return (
      <Sheet open onOpenChange={open => !open && onClose()}>
        <SheetContent side="right" className="gap-0 p-0">
          {/* `pr-12`: Sheet'in kendi kapatma düğmesi sağ üstte, başlığın üstüne binmesin. */}
          <SheetHeader className="border-b pr-12">
            <SheetTitle className="line-clamp-1 text-lg font-semibold" title={cabinet.name}>
              {cabinet.name}
            </SheetTitle>
          </SheetHeader>
          <CabinetDetailBody cabinet={cabinet} />
        </SheetContent>
      </Sheet>
    );
  }

  return (
    <aside className="flex h-full w-80 shrink-0 flex-col rounded-lg border bg-card text-card-foreground shadow-sm animate-in fade-in-0 slide-in-from-right-4 duration-300 xl:w-96">
      <div className="flex items-center justify-between border-b p-4">
        <h2 className="text-lg font-semibold line-clamp-1" title={cabinet.name}>{cabinet.name}</h2>
        <Button variant="ghost" size="icon" aria-label="Kapat" onClick={onClose}>
          <X className="size-4" />
        </Button>
      </div>
      <CabinetDetailBody cabinet={cabinet} />
    </aside>
  );
}

/**
 * Panel gövdesi: en üstte modül kısayolları, altında açılır-kapanır bölümler (`PanelSection`) — Durum, modül bölümleri
 * (örn. Operatör İşlemi), Kabin bilgileri. Her bölüm kapalıyken de başlığında özetini gösterir; içerikler ayrı çerçeve açmaz.
 */
function CabinetDetailBody({ cabinet }: { cabinet: CabinetDetailDto }) {
  return (
    <div className="flex-1 space-y-3 overflow-y-auto scrollbar-thin p-4">
      {/* Açık modüllerin panel EN ÜST eylemleri (bkz. `AppModule.CabinetPanelTopAction`); bölümlerden önce gelir. */}
      {cabinetPanelTopActions.map(({ key, Component }) => (
        <Suspense key={key} fallback={<Skeleton className="h-12 w-full rounded-lg" />}>
          <Component cabinetId={cabinet.id} />
        </Suspense>
      ))}

      {/* Kabin neden bu renkte: hangi cihaz/kamera ne katkı veriyor (sunucunun kuralıyla). Kabin değişince yeni abonelik. */}
      <CabinetStatusSection key={cabinet.id} cabinetId={cabinet.id} statusId={cabinet.deviceStatusId} />

      {/* Açık modüllerin bu kabine dair bölümleri (bkz. `AppModule.CabinetPanelSection`); onlar da `PanelSection` kullanır. */}
      {cabinetPanelSections.map(({ key, Component }) => (
        <Suspense key={key} fallback={<Skeleton className="h-11 w-full rounded-lg" />}>
          <Component cabinetId={cabinet.id} />
        </Suspense>
      ))}

      {/* Künye nadiren okunur: kapalı başlar, başlıkta firma görünür. */}
      <PanelSection title="Kabin bilgileri" summary={<span className="max-w-32 truncate">{cabinet.companyName}</span>} defaultOpen={false}>
        <PanelField label="Firma">{cabinet.companyName}</PanelField>
        <PanelField label="Ağ IP">{cabinet.networkIp || '—'}</PanelField>
        <PanelField label="GSM IP">{cabinet.gsmIp || '—'}</PanelField>
        <PanelField label="Lokasyon">
          <span className="flex items-start gap-1.5">
            <MapPin className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" />
            {cabinet.locationDescription || '—'}
          </span>
        </PanelField>
        {cabinet.updateDateUtc && (
          <PanelField label="Son güncelleme">
            <span className="flex items-center gap-1.5">
              <Clock className="size-3.5 text-muted-foreground" />
              {formatUtcDateTime(cabinet.updateDateUtc)}
            </span>
          </PanelField>
        )}
      </PanelSection>
    </div>
  );
}
