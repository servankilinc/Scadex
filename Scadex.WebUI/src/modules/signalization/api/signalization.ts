/**
 * Sinyalizasyon modülünün uçları. Backend'de `Modules:Signalization:Enabled` kapalıysa HEPSİ 404 döner
 * (controller'lar ApplicationPart listesinden çıkarılır).
 *
 * Rota adları büyük/küçük harfe duyarlı yazılır (`api/[controller]`) — çekirdek uçlarıyla aynı kural.
 */
import http from '@/lib/axios-helper';
import type { PaginationResponse } from '@/models/channelEvent';
import type { SignalAuthorityDto, SignalAuthoritySaveRequest } from '../models/authority';
import type { SignalCabinetDto, SignalCabinetOptionsDto, SignalCabinetSaveRequest } from '../models/cabinet';
import type { SignalOperatorAuthorityRequest, SignalOperatorDto } from '../models/operator';
import type {
  OperatorSessionDetailDto,
  OperatorSessionListItemDto,
  OperatorSessionOpenDto,
  OperatorSessionQueryRequest,
  OperatorSessionSummaryDto,
  OperatorSessionSummaryRequest
} from '../models/session';

const AUTHORITY_ROUTE = '/api/SignalAuthority';
const OPERATOR_ROUTE = '/api/SignalOperator';
const CABINET_ROUTE = '/api/SignalCabinet';
const SESSION_ROUTE = '/api/OperatorSession';

// ─────────────────────────────────────────────────────────── kurumlar

/** Pasifler DAHİL. */
export async function getSignalAuthorities(): Promise<SignalAuthorityDto[]> {
  return http.get<SignalAuthorityDto[]>(AUTHORITY_ROUTE);
}

/**
 * TAM liste; gövdede olmayan kurum pasife alınır. Aktif bir iç kapıda kullanılan kurumu pasife almak 400
 * (`errors.Authorities`). Başarıda gövdesiz 200.
 */
export async function saveSignalAuthorities(request: SignalAuthoritySaveRequest): Promise<void> {
  return http.put(AUTHORITY_ROUTE, request);
}

// ─────────────────────────────────────────────────────────── operatörler

export async function getSignalOperators(): Promise<SignalOperatorDto[]> {
  return http.get<SignalOperatorDto[]>(OPERATOR_ROUTE);
}

/** Kullanıcının TÜM kurum rollerini çıkarır, (varsa) seçileni ekler; kurum dışı roller korunur. */
export async function setSignalOperatorAuthority(userId: string, request: SignalOperatorAuthorityRequest): Promise<void> {
  return http.put(`${OPERATOR_ROUTE}/${userId}/authority`, request);
}

// ─────────────────────────────────────────────────────────── kabin yapılandırması

/** Yapılandırılmamış kabinde de 200 döner (`isConfigured: false` + varsayılanlar); kabin yoksa 404. */
export async function getSignalCabinet(cabinetId: string): Promise<SignalCabinetDto> {
  return http.get<SignalCabinetDto>(`${CABINET_ROUTE}/${cabinetId}`);
}

export async function getSignalCabinetOptions(cabinetId: string): Promise<SignalCabinetOptionsDto> {
  return http.get<SignalCabinetOptionsDto>(`${CABINET_ROUTE}/${cabinetId}/options`);
}

/** Yapılandırmanın TEK yazım yolu. Başarıda gövdesiz 200. */
export async function saveSignalCabinet(cabinetId: string, request: SignalCabinetSaveRequest): Promise<void> {
  return http.put(`${CABINET_ROUTE}/${cabinetId}`, request);
}

// ─────────────────────────────────────────────────────────── oturumlar (salt okunur)

/** Açık oturumlar — canlı panel. `cabinetId` verilmezse tüm kabinler. */
export async function getOpenSessions(cabinetId: string | null): Promise<OperatorSessionOpenDto[]> {
  return http.get<OperatorSessionOpenDto[]>(`${SESSION_ROUTE}/open`, { params: cabinetId ? { cabinetId } : undefined });
}

export async function getSessionList(request: OperatorSessionQueryRequest): Promise<PaginationResponse<OperatorSessionListItemDto>> {
  return http.post<PaginationResponse<OperatorSessionListItemDto>>(`${SESSION_ROUTE}/list`, request);
}

export async function getSessionDetail(id: number): Promise<OperatorSessionDetailDto> {
  return http.get<OperatorSessionDetailDto>(`${SESSION_ROUTE}/${id}`);
}

export async function getSessionSummary(request: OperatorSessionSummaryRequest): Promise<OperatorSessionSummaryDto> {
  return http.post<OperatorSessionSummaryDto>(`${SESSION_ROUTE}/summary`, request);
}
