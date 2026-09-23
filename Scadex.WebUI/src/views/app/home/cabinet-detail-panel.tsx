import { Suspense } from 'react';
import { Clock, MapPin, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Skeleton } from '@/components/ui/skeleton';
import { useMediaQuery } from '@/hooks/use-media-query';
import type { CabinetDetailDto } from '@/models/cabinet';
import { cabinetPanelSections, cabinetPanelTopActions } from '@/modules';
import { formatUtcDateTime } from '@/lib/utils';

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

function CabinetDetailBody({ cabinet }: { cabinet: CabinetDetailDto }) {
  return (
    <div className="flex-1 space-y-4 overflow-y-auto scrollbar-thin p-4">
      {/* Açık modüllerin panel EN ÜST eylemleri (bkz. `AppModule.CabinetPanelTopAction`); künyeden
          önce gelir — modül yoksa liste boştur ve panel doğrudan "Firma"yla başlar. */}
      {cabinetPanelTopActions.map(({ key, Component }) => (
        <Suspense key={key} fallback={<Skeleton className="h-12 w-full rounded-lg" />}>
          <Component cabinetId={cabinet.id} />
        </Suspense>
      ))}

      <div className="space-y-1">
        <p className="text-sm font-medium text-muted-foreground">Firma</p>
        <p className="text-sm">{cabinet.companyName}</p>
      </div>

      <div className="space-y-1">
        <p className="text-sm font-medium text-muted-foreground">Durum</p>
        <p className="flex items-center gap-2 text-sm">
          <span className={`size-2.5 rounded-full ${cabinet.isActive ? 'bg-green-500' : 'bg-red-500'}`} />
          {cabinet.deviceStatusName || 'Bilinmiyor'}
        </p>
      </div>

      <div className="space-y-1">
        <p className="text-sm font-medium text-muted-foreground">Ağ İP</p>
        <p className="text-sm">{cabinet.networkIp || '-'}</p>
      </div>

      <div className="space-y-1">
        <p className="text-sm font-medium text-muted-foreground">GSM İP</p>
        <p className="text-sm">{cabinet.gsmIp || '-'}</p>
      </div>

      <div className="space-y-1">
        <p className="text-sm font-medium text-muted-foreground">Lokasyon</p>
        <p className="flex items-start gap-1.5 text-sm">
          <MapPin className="mt-0.5 size-3.5 shrink-0" />
          <span>{cabinet.locationDescription || '-'}</span>
        </p>
      </div>

      {cabinet.updateDateUtc && (
        <div className="space-y-1 border-t pt-4">
          <p className="text-sm font-medium text-muted-foreground">Son Güncelleme</p>
          <p className="flex items-center gap-1.5 text-sm">
            <Clock className="size-3.5" />
            {formatUtcDateTime(cabinet.updateDateUtc)}
          </p>
        </div>
      )}

      {/* Açık modüllerin bu kabine dair bölümleri (bkz. `AppModule.CabinetPanelSection`);
          modül yoksa liste boştur ve panel yalnızca künyeden ibaret kalır. */}
      {cabinetPanelSections.map(({ key, Component }) => (
        <Suspense key={key} fallback={<Skeleton className="h-28 w-full rounded-lg" />}>
          <Component cabinetId={cabinet.id} />
        </Suspense>
      ))}
    </div>
  );
}
