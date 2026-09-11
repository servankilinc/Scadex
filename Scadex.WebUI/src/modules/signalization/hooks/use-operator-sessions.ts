import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ApiError } from '@/lib/axios-helper';
import { getOpenSessions, getSessionDetail, getSessionList, getSessionSummary } from '../api/signalization';
import { signalizationKeys } from '../api/query-keys';
import type { OperatorSessionQueryRequest, OperatorSessionSummaryRequest } from '../models/session';

/** Uyarı yoklayıcısının aralığı. SignalR olayı bilerek eklenmedi — yoklama yeterli ve çekirdeğe dokunmaz. */
export const LIVE_POLL_MS = 10_000;

/**
 * 404 = backend'de modül kapalı (`Modules:Signalization:Enabled`). Yoklamayı sürdürmek her 10 sn'de bir boşuna
 * istek atmak olurdu; `VITE_MODULES` ile backend ayarı ayrı tutulduğu için bu durum gerçekten oluşabilir.
 */
function isModuleOff(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404;
}

/**
 * Açık oturumlar (tüm kabinler). Canlı panel ve uyarı yoklayıcısı AYNI anahtarı kullanır: iki gözlemci tek
 * istek üretir, TanStack en kısa aralığı uygular.
 *
 * `refetchIntervalInBackground`: sekme arka plandayken de yoklanır — güvenlik uyarısı kullanıcı başka sekmeye
 * geçti diye gecikmemeli.
 */
export function useOpenSessions(intervalMs = LIVE_POLL_MS) {
  return useQuery({
    queryKey: signalizationKeys.openSessions(null),
    queryFn: () => getOpenSessions(null),
    refetchInterval: query => (isModuleOff(query.state.error) ? false : intervalMs),
    refetchIntervalInBackground: true
  });
}

/** Sayfalı geçmiş. `keepPreviousData`: sayfa değişiminde tablo boşalıp yeniden dolmaz. */
export function useSessionList(request: OperatorSessionQueryRequest) {
  return useQuery({
    queryKey: signalizationKeys.sessionList(request),
    queryFn: () => getSessionList(request),
    placeholderData: keepPreviousData
  });
}

/** Oturum detayı. Oturum AÇIKKEN kendini yoklar (olaylar akmaya devam eder); kapanınca durur. */
export function useSessionDetail(id: number) {
  return useQuery({
    queryKey: signalizationKeys.sessionDetail(id),
    queryFn: () => getSessionDetail(id),
    enabled: Number.isInteger(id) && id > 0,
    refetchInterval: query => (query.state.data && query.state.data.endedAtUtc == null ? 3_000 : false)
  });
}

export function useSessionSummary(request: OperatorSessionSummaryRequest | null) {
  return useQuery({
    queryKey: signalizationKeys.summary(request ?? { fromUtc: '', toUtc: '' }),
    queryFn: () => getSessionSummary(request!),
    enabled: request != null,
    placeholderData: keepPreviousData
  });
}
