import { useQuery } from '@tanstack/react-query';
import { getPc, getPcs } from '../api/remote-desk';
import { remoteDeskKeys } from '../api/query-keys';

/** Bağlantı ve izleyici sayıları sunucunun belleğinde; MVP'de canlı yayın yok, ekran açıkken yoklanır (RemoteDesk.md § 10.6). */
const REFRESH_MS = 10_000;

export function usePcs() {
  return useQuery({ queryKey: remoteDeskKeys.pcs(), queryFn: getPcs, refetchInterval: REFRESH_MS });
}

export function usePc(deviceId: string | undefined) {
  return useQuery({
    queryKey: remoteDeskKeys.pc(deviceId ?? ''),
    queryFn: () => getPc(deviceId!),
    enabled: !!deviceId,
    refetchInterval: REFRESH_MS
  });
}
