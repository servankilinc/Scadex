import { useMemo } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ApiError } from '@/lib/axios-helper';
import { toUtcDate } from '@/lib/utils';
import type { CabinetIdsQueryOptions } from '../../types';
import { getOpenSessions, getSessionDetail, getSessionList, getSessionSummary } from '../api/signalization';
import { signalizationKeys } from '../api/query-keys';
import type { OperatorSessionOpenDto, OperatorSessionQueryRequest, OperatorSessionSummaryRequest } from '../models/session';

/**
 * Açık oturum yoklamasının aralığı. Anlık güncelleme canlı yayından gelir (`use-session-realtime.ts`, `/hubs/signalization`);
 * yoklama soket kurulamadığında ya da koptuğunda GERİ DÖNÜŞTÜR.
 */
export const LIVE_POLL_MS = 10_000;

/**
 * 404 = backend'de modül kapalı (`Modules:Signalization:Enabled`). Yoklamayı sürdürmek her 10 sn'de bir boşuna
 * istek atmak olurdu; `VITE_MODULES` ile backend ayarı ayrı tutulduğu için bu durum gerçekten oluşabilir.
 */
export function isModuleOff(error: unknown): boolean {
  return error instanceof ApiError && error.status === 404;
}

/**
 * Açık oturumlar (tüm kabinler). Canlı panel, uyarı yoklayıcısı ve ana sayfa haritası AYNI anahtarı kullanır:
 * gözlemciler tek istek üretir, TanStack en kısa aralığı uygular.
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

/**
 * Açık oturumu olan kabinler — ana sayfa haritasındaki "işlem yapılıyor" ikonu (manifestodaki
 * `busyCabinetsQuery`). `useOpenSessions` ile AYNI anahtar ve `queryFn`: önbellek satırı paylaşılır, kimlik
 * listesi `select` ile türetilir. `queryOptions` ile ortak bir fabrika bilerek yazılmadı — literal anahtar tipi
 * modül sözleşmesindeki genel `QueryKey`'e atanamıyor.
 */
export function busyCabinetsQueryOptions(intervalMs = LIVE_POLL_MS): CabinetIdsQueryOptions {
  return {
    queryKey: signalizationKeys.openSessions(null),
    queryFn: () => getOpenSessions(null),
    refetchInterval: query => (isModuleOff(query.state.error) ? false : intervalMs),
    refetchIntervalInBackground: true,
    select: (sessions: OperatorSessionOpenDto[]) => [...new Set(sessions.map(session => session.cabinetId))]
  };
}

/**
 * Alarm bayraklı (kartsız giriş / zorla açma — `hasAlert`) açık oturumu olan kabinler — ana sayfa haritasındaki alarm
 * ikonu (manifestodaki `alertCabinetsQuery`). `busyCabinetsQueryOptions` ile AYNI anahtar ve `queryFn`: ek istek
 * doğmaz, yalnızca `select` farklı. Kabin durumuna (`deviceStatusId`) yansımaz — 2026-09-24 kararı.
 */
export function alertCabinetsQueryOptions(intervalMs = LIVE_POLL_MS): CabinetIdsQueryOptions {
  return {
    queryKey: signalizationKeys.openSessions(null),
    queryFn: () => getOpenSessions(null),
    refetchInterval: query => (isModuleOff(query.state.error) ? false : intervalMs),
    refetchIntervalInBackground: true,
    select: (sessions: OperatorSessionOpenDto[]) => [...new Set(sessions.filter(session => session.hasAlert).map(session => session.cabinetId))]
  };
}

/**
 * Sayfalı geçmiş. `keepPreviousData`: sayfa değişiminde tablo boşalıp yeniden dolmaz.
 *
 * `enabled`: çağıran, sonucu gerçekten gerekmedikçe isteği bastırabilir (harita paneli, açık oturum
 * varken geçmişi hiç sormaz). Anahtar değişmez — canlı yayının `sessionLists()` tazelemesi bu
 * sorguyu da kapsar.
 */
export function useSessionList(request: OperatorSessionQueryRequest, enabled = true) {
  return useQuery({
    queryKey: signalizationKeys.sessionList(request),
    queryFn: () => getSessionList(request),
    enabled,
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

/**
 * Bir kabinin "şu anki" işlemi: açık oturum(lar) varsa ONLAR, yoksa geçmişteki en yeni kayıt.
 * Harita detay panelinin veri kaynağı.
 *
 * Açık oturumlar `useOpenSessions` önbelleğinden SÜZÜLÜR: ana sayfa haritası zaten aynı anahtarı
 * kullanıyor (`busyCabinetsQuery`), bu yüzden panel ek istek üretmez. Bir kabinin birden çok dış
 * kapısı olabilir ve açıklık kısıtı kapı bazlıdır — bu yüzden tekil değil, LİSTE döner.
 *
 * Geçmiş kaydı yalnızca açık oturum yokken ve tek satır olarak çekilir; sunucu `StartedAtUtc`
 * azalan sıralar, ilk satır en son işlemdir.
 */
export function useCabinetLatestSession(cabinetId: string) {
  const open = useOpenSessions();

  const openSessions = useMemo(
    () =>
      (open.data ?? [])
        .filter(session => session.cabinetId === cabinetId)
        .sort((a, b) => (toUtcDate(b.startedAtUtc)?.getTime() ?? 0) - (toUtcDate(a.startedAtUtc)?.getTime() ?? 0)),
    [open.data, cabinetId]
  );

  const hasOpen = openSessions.length > 0;
  const history = useSessionList({ cabinetId, page: 1, pageSize: 1 }, open.isSuccess && !hasOpen);

  return {
    openSessions,
    lastSession: hasOpen ? null : (history.data?.data[0] ?? null),
    /** Sunucunun `elapsedSec` değerinin ait olduğu an — canlı sayaç bunun üstüne ekler. */
    openUpdatedAt: open.dataUpdatedAt,
    isPending: open.isPending || (open.isSuccess && !hasOpen && history.isPending),
    isError: open.isError || history.isError,
    error: open.error ?? history.error,
    /** Backend'de modül kapalı (404): bölüm hiç çizilmemeli. */
    moduleOff: isModuleOff(open.error) || isModuleOff(history.error)
  };
}

export function useSessionSummary(request: OperatorSessionSummaryRequest | null) {
  return useQuery({
    queryKey: signalizationKeys.summary(request ?? { fromUtc: '', toUtc: '' }),
    queryFn: () => getSessionSummary(request!),
    enabled: request != null,
    placeholderData: keepPreviousData
  });
}
