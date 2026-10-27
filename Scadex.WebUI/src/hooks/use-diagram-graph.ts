import { useQuery } from '@tanstack/react-query';
import { getCabinetDiagram } from '@/api/diagram';
import { diagramKeys } from '@/api/query-keys';

/**
 * Sunucudan gelen DEĞİŞMEZ graf anlık görüntüsü.
 *
 * Bu veri yerelde ASLA mutasyona uğramaz. Düzenleme React Flow state'inde yaşar (`useDiagramEditor`); başarılı istekte cache
 * yalnızca bayat işaretlenir, başarısız istekte yeniden çekilir ve tuval sunucu gerçeğine döner.
 */
export function useDiagramGraph(cabinetId: string | undefined) {
  return useQuery({
    queryKey: diagramKeys.cabinet(cabinetId ?? ''),
    queryFn: () => getCabinetDiagram(cabinetId!),
    enabled: !!cabinetId
  });
}
