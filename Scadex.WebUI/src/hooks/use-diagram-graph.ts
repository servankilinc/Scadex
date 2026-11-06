import { useQuery } from '@tanstack/react-query';
import { getAutomationDiagram } from '@/api/diagram';
import { diagramKeys } from '@/api/query-keys';

/**
 * Sunucudan gelen DEĞİŞMEZ graf anlık görüntüsü.
 *
 * Bu veri yerelde ASLA mutasyona uğramaz. Düzenleme React Flow state'inde yaşar (`useDiagramEditor`); başarılı istekte cache
 * yalnızca bayat işaretlenir, başarısız istekte yeniden çekilir ve tuval sunucu gerçeğine döner.
 */
export function useDiagramGraph(automationId: string | undefined) {
  return useQuery({
    queryKey: diagramKeys.automation(automationId ?? ''),
    queryFn: () => getAutomationDiagram(automationId!),
    enabled: !!automationId
  });
}
