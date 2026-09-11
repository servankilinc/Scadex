import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { userKeys } from '@/api/query-keys';
import {
  getSignalAuthorities,
  getSignalCabinet,
  getSignalCabinetOptions,
  getSignalOperators,
  saveSignalAuthorities,
  saveSignalCabinet,
  setSignalOperatorAuthority
} from '../api/signalization';
import { signalizationKeys } from '../api/query-keys';
import type { SignalAuthoritySaveRequest } from '../models/authority';
import type { SignalCabinetSaveRequest } from '../models/cabinet';

/**
 * Yapılandırma hook'ları: kurumlar, operatörler, kabin ağacı.
 *
 * Mutation'larda `onError` BİLEREK yok — hata politikası forma aittir (`handleFormApiError` /
 * `handleTreeFormApiError`). Sunucu gövdesiz 200 döndüğü için `setQueryData` ile yazılacak veri de yok: invalidate.
 */

export function useSignalAuthorities() {
  return useQuery({ queryKey: signalizationKeys.authorities(), queryFn: getSignalAuthorities });
}

export function useSaveSignalAuthorities() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: SignalAuthoritySaveRequest) => saveSignalAuthorities(request),
    onSuccess: () => {
      // Kurum adı operatör listesinde ve kabin seçeneklerinde de görünür; ikisi de eskir.
      void queryClient.invalidateQueries({ queryKey: signalizationKeys.all });
      toast.success('Kurumlar kaydedildi.');
    }
  });
}

export function useSignalOperators() {
  return useQuery({ queryKey: signalizationKeys.operators(), queryFn: getSignalOperators });
}

export function useSetSignalOperatorAuthority() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ userId, authorityId }: { userId: string; authorityId: string | null }) => setSignalOperatorAuthority(userId, { authorityId }),
    onSuccess: (_data, { userId }) => {
      void queryClient.invalidateQueries({ queryKey: signalizationKeys.operators() });
      // Kurum bir ROLDÜR: /admin/users'taki Roller dialogu da eskidi.
      void queryClient.invalidateQueries({ queryKey: userKeys.roles(userId) });
      toast.success('Kurum atandı.');
    }
  });
}

export function useSignalCabinet(cabinetId: string) {
  return useQuery({
    queryKey: signalizationKeys.cabinet(cabinetId),
    queryFn: () => getSignalCabinet(cabinetId),
    enabled: Boolean(cabinetId)
  });
}

export function useSignalCabinetOptions(cabinetId: string) {
  return useQuery({
    queryKey: signalizationKeys.cabinetOptions(cabinetId),
    queryFn: () => getSignalCabinetOptions(cabinetId),
    enabled: Boolean(cabinetId)
  });
}

export function useSaveSignalCabinet() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ cabinetId, request }: { cabinetId: string; request: SignalCabinetSaveRequest }) => saveSignalCabinet(cabinetId, request),
    onSuccess: (_data, { cabinetId }) => {
      // Seçeneklerdeki "kullanımda" etiketleri kayıtlı yapılandırmadan türer.
      void queryClient.invalidateQueries({ queryKey: signalizationKeys.cabinet(cabinetId) });
      void queryClient.invalidateQueries({ queryKey: signalizationKeys.cabinetOptions(cabinetId) });
      toast.success('Kabin yapılandırması kaydedildi.');
    }
  });
}
