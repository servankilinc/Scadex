import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { upsertCanvasSettings } from '@/api/canvas-settings';
import { diagramKeys } from '@/api/query-keys';
import { toApiError } from '@/lib/axios-helper';
import type { CanvasSettingsUpsertRequest } from '@/models/canvasSettings';
import type { DiagramDto } from '@/models/diagram';

/**
 * Canvas ayarlarını yazar.
 *
 * OKUMA burada yok: ayarlar kabin bazlı olduğu için diyagram aggregate'inin
 * içinde geliyor (`useDiagramGraph().data.canvasSettings`). Ayrı bir query
 * açmak aynı veriyi iki kez indirmek ve iki cache'i senkron tutmak olurdu.
 */
export function useCanvasSettings(cabinetId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CanvasSettingsUpsertRequest) => upsertCanvasSettings(cabinetId, request),
    onSuccess: settings => {
      // Sunucu zaten yazdığı değerleri döndürdüğü için cache'i yerinde güncellemek daha doğru
      queryClient.setQueryData<DiagramDto>(diagramKeys.cabinet(cabinetId), previous =>
        previous ? { ...previous, canvasSettings: settings } : previous
      );
    },
    onError: error => {
      toast.error(toApiError(error).message);
    }
  });
}
