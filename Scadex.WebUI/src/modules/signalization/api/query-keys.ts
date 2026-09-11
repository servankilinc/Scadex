/**
 * Modülün TanStack Query anahtarları — hepsi `['signalization', …]` altında: modül çekirdek anahtarlarını
 * uçurmaz, çekirdek invalidation'ı modül verisine dokunmaz.
 */
import type { OperatorSessionQueryRequest, OperatorSessionSummaryRequest } from '../models/session';

const root = ['signalization'] as const;

export const signalizationKeys = {
  all: root,
  /** Pasifler DAHİL — geri alınabilsinler diye. */
  authorities: () => [...root, 'authorities'] as const,
  /** Aktif kullanıcılar + türetilmiş kurum. */
  operators: () => [...root, 'operators'] as const,
  cabinet: (cabinetId: string) => [...root, 'cabinet', cabinetId] as const,
  /** Yapılandırma ekranının seçenekleri (kanallar, kameralar, kurumlar). */
  cabinetOptions: (cabinetId: string) => [...root, 'cabinet-options', cabinetId] as const,

  sessions: () => [...root, 'session'] as const,
  /** Canlı panel. Uyarı yoklayıcısı ve oturum ekranı AYNI anahtarı paylaşır (tüm kabinler). */
  openSessions: (cabinetId: string | null) => [...root, 'session', 'open', cabinetId] as const,
  /** Filtre nesnesinin TAMAMI anahtara girer — filtre değişimi başka bir sorgudur. */
  sessionList: (request: OperatorSessionQueryRequest) => [...root, 'session', 'list', request] as const,
  sessionDetail: (id: number) => [...root, 'session', 'detail', id] as const,
  summary: (request: OperatorSessionSummaryRequest) => [...root, 'session', 'summary', request] as const
};
