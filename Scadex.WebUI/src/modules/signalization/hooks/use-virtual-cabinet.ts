import { useEffect, useRef } from 'react';
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { toApiError } from '@/lib/axios-helper';
import { CommandStatus, CommandStatusLabels } from '@/models/enums';
import { getSignalCabinetLive, sendSignalCabinetCommand } from '../api/signalization';
import { signalizationKeys } from '../api/query-keys';
import { SignalDoorKind, type SignalCabinetStateChangedMessage, type SignalDoorSwitchChangedMessage } from '../models/realtime';
import { subscribeToCabinetState, useSignalizationHubStatus } from '../signalr/signalization-hub';
import { SignalCabinetOutput, SignalCabinetOutputLabels, type SignalCabinetCommandRequest, type SignalCabinetLiveDto } from '../models/virtual-cabinet';

/**
 * Sanal kabin ekranının okuması. **Yoklama YOKTUR** (`refetchInterval` yok): ilk yüklemeyi bu sorgu yapar, gerisini
 * `useVirtualCabinetLive` canlı yayından getirir.
 */
export function useSignalCabinetLive(cabinetId: string) {
  return useQuery({
    queryKey: signalizationKeys.cabinetLive(cabinetId),
    queryFn: () => getSignalCabinetLive(cabinetId),
    enabled: Boolean(cabinetId)
  });
}

/**
 * Elle komut. Sonuç HTTP koduyla değil gövdedeki `status` ile gelir — çekirdeğin `useSendCommand`'iyle aynı kural
 * (`hooks/use-device-commands.ts`).
 *
 * İstek SCADA cevaplayana kadar bekler (kabinin komut zaman aşımı, 5-180 sn); çağıran `isPending` ile bekleme
 * göstermek zorunda.
 */
export function useSignalCabinetCommand(cabinetId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: SignalCabinetCommandRequest) => sendSignalCabinetCommand(cabinetId, request),
    onSuccess: result => {
      const label = SignalCabinetOutputLabels[result.target];

      // Canlı yayın değişimi zaten getirir; bu okuma yayın kopmuşsa diye bir güvencedir. Başarısızlıkta ekran ESKİ değeri göstermeli.
      void queryClient.invalidateQueries({ queryKey: signalizationKeys.cabinetLive(cabinetId) });

      if (result.status === CommandStatus.Succeeded) {
        toast.success(`${label}: komut gönderildi.`);
        return;
      }

      toast.error(`${label} komutu başarısız — ${CommandStatusLabels[result.status]}`, {
        description: result.resultMessage ?? undefined,
        duration: 8000
      });
    },
    onError: error => toast.error(toApiError(error).message)
  });
}

/**
 * Ekranın canlılığı — TEK kaynak modülün `/hubs/signalization`'ı, **hiçbir yoklama yoktur**. Çekirdeğin `/hubs/diagram`'ına
 * bağlanılmaz: sunucu çekirdeğin kanal değişimlerini (`IScadaEventObserver`) kapı/siren/aydınlatma/kilide eşleyip anlamlı
 * olaylar olarak yayınlar.
 *
 * Her iki olay da VERİYİ taşır; önbellekteki ilgili alan `setQueryData` ile yamalanır, HTTP isteği atılmaz.
 *
 * Yeniden bağlanmada (ilk bağlantı hariç) tazeleme yapılır: kopukluk sırasında kaçan olayların tek telafisi budur.
 */
export function useVirtualCabinetLive(cabinetId: string): void {
  const queryClient = useQueryClient();
  const status = useSignalizationHubStatus();
  const hasConnectedBefore = useRef(false);

  useEffect(() => {
    if (!cabinetId) return;

    return subscribeToCabinetState(cabinetId, {
      onCabinetStateChanged: message => patchCabinetLive(queryClient, cabinetId, live => applyStateChanged(live, message)),
      onDoorSwitchChanged: message => patchCabinetLive(queryClient, cabinetId, live => applyDoorSwitchChanged(live, message))
    });
  }, [cabinetId, queryClient]);

  // İLK bağlantıda tazeleme YAPILMAZ — sorgu zaten yeni çekildi. Sonraki her "connected" bir YENİDEN bağlanmadır ve aradaki
  // olaylar kaçmıştır (`use-diagram-live.ts` / `use-session-realtime.ts` ile aynı kural).
  useEffect(() => {
    if (status !== 'connected') return;

    if (!hasConnectedBefore.current) {
      hasConnectedBefore.current = true;
      return;
    }

    void queryClient.invalidateQueries({ queryKey: signalizationKeys.cabinetLive(cabinetId) });
  }, [status, cabinetId, queryClient]);
}

// ------------------------------------------------------------------ önbellek yamaları

function patchCabinetLive(queryClient: QueryClient, cabinetId: string, patch: (live: SignalCabinetLiveDto) => SignalCabinetLiveDto): void {
  // Sorgu henüz yüklenmediyse yamanacak bir şey yok: ilk okuma zaten güncel değeri getirecek.
  queryClient.setQueryData<SignalCabinetLiveDto>(signalizationKeys.cabinetLive(cabinetId), live => (live ? patch(live) : live));
}

function applyStateChanged(live: SignalCabinetLiveDto, message: SignalCabinetStateChangedMessage): SignalCabinetLiveDto {
  switch (message.target) {
    case SignalCabinetOutput.Siren:
      return { ...live, sirenIsOn: message.isOn, sirenChangedAtUtc: message.changedAtUtc };

    case SignalCabinetOutput.OuterDoorLight:
      return {
        ...live,
        outerDoors: live.outerDoors.map(outer =>
          outer.id === message.targetId ? { ...outer, lightIsOn: message.isOn, lightChangedAtUtc: message.changedAtUtc } : outer
        )
      };

    case SignalCabinetOutput.InnerDoorLock:
      return {
        ...live,
        outerDoors: live.outerDoors.map(outer => ({
          ...outer,
          innerDoors: outer.innerDoors.map(inner =>
            inner.id === message.targetId ? { ...inner, isUnlocked: message.isOn, lockChangedAtUtc: message.changedAtUtc } : inner
          )
        }))
      };

    default:
      return live;
  }
}

function applyDoorSwitchChanged(live: SignalCabinetLiveDto, message: SignalDoorSwitchChangedMessage): SignalCabinetLiveDto {
  const patch = { isOpen: message.isOpen, switchChangedAtUtc: message.changedAtUtc };

  if (message.doorKind === SignalDoorKind.Outer) {
    return { ...live, outerDoors: live.outerDoors.map(outer => (outer.id === message.doorId ? { ...outer, ...patch } : outer)) };
  }

  return {
    ...live,
    outerDoors: live.outerDoors.map(outer => ({
      ...outer,
      innerDoors: outer.innerDoors.map(inner => (inner.id === message.doorId ? { ...inner, ...patch } : inner))
    }))
  };
}
