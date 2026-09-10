import { useQuery } from '@tanstack/react-query';
import { getCabinetDiagram } from '@/api/diagram';
import { diagramKeys } from '@/api/query-keys';

/**
 * Sunucudan gelen DEĞİŞMEZ graf anlık görüntüsü.
 *
 * Bu veri yerelde ASLA mutasyona uğramaz. Düzenleme geldiğinde React Flow state'i ve ayrı bir değişiklik günlüğü tutulur;
 * buradaki cache yalnızca kaydetme başarılı olduğunda `setQueryData` ile güncellenir.
 */
export function useDiagramGraph(cabinetId: string | undefined) {
  return useQuery({
    queryKey: diagramKeys.cabinet(cabinetId ?? ''),
    queryFn: () => getCabinetDiagram(cabinetId!),
    enabled: !!cabinetId
  });
}
