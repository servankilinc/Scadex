import http from '@/lib/axios-helper';
import type { ChannelEventDto, ChannelEventQueryRequest, PaginationResponse } from '@/models/channelEvent';

const CHANNEL_EVENT_ROUTE = '/api/ChannelEvent';

/**
 * Bir kabinin olay geçmişi — yeniden eskiye, sayfalı.
 *
 * SALT OKUNUR bir kaynaktır: olayları yalnızca SCADA ingest'i üretir, dışarıdan
 * yazdırılamaz. Bu yüzden burada create/update/delete fonksiyonu yok ve
 * olmayacak.
 *
 * GET değil POST: filtre gövdesi (kabin, kanal, tarih aralığı, sayfalama) query
 * string'e sıkıştırılmaktansa tiplenmiş bir gövdede taşınıyor — kod tabanının
 * diğer listeleme uçlarıyla aynı desen.
 */
export async function getChannelEvents(request: ChannelEventQueryRequest): Promise<PaginationResponse<ChannelEventDto>> {
  return http.post<PaginationResponse<ChannelEventDto>>(`${CHANNEL_EVENT_ROUTE}/list`, request);
}
