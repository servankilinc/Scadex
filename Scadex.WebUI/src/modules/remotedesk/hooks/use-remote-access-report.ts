import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getControlReport, getReportFilters, getReportSummary, getTransferReport, getViewReport } from '../api/remote-desk';
import { remoteDeskKeys } from '../api/query-keys';
import type { RemoteAccessReportQuery } from '../models/report';

/**
 * Denetim raporu sorguları. Yoklama YOK: geçmişe bakılır, kullanıcı filtreyi değiştirince yeniden sorgulanır.
 * Listelerde `keepPreviousData` — sayfa değiştirirken tablo boşalıp yeniden dolmasın (Olay Geçmişi ile aynı).
 */
export function useReportFilters() {
  return useQuery({ queryKey: remoteDeskKeys.reportFilters(), queryFn: getReportFilters });
}

export function useViewReport(query: RemoteAccessReportQuery, enabled: boolean) {
  return useQuery({
    queryKey: remoteDeskKeys.report('views', query),
    queryFn: () => getViewReport(query),
    enabled,
    placeholderData: keepPreviousData
  });
}

export function useControlReport(query: RemoteAccessReportQuery, enabled: boolean) {
  return useQuery({
    queryKey: remoteDeskKeys.report('controls', query),
    queryFn: () => getControlReport(query),
    enabled,
    placeholderData: keepPreviousData
  });
}

export function useTransferReport(query: RemoteAccessReportQuery, enabled: boolean) {
  return useQuery({
    queryKey: remoteDeskKeys.report('transfers', query),
    queryFn: () => getTransferReport(query),
    enabled,
    placeholderData: keepPreviousData
  });
}

/** Özet sayfadan bağımsızdır: anahtara sayfa girmesin diye sayfa alanları sabitlenir. */
export function useReportSummary(query: RemoteAccessReportQuery) {
  const filterOnly = { ...query, page: 1, pageSize: 1 };
  return useQuery({
    queryKey: remoteDeskKeys.report('summary', filterOnly),
    queryFn: () => getReportSummary(filterOnly),
    placeholderData: keepPreviousData
  });
}
