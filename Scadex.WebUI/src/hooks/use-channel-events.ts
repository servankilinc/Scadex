import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { getChannelEvents } from '@/api/channel-event';
import { channelEventKeys } from '@/api/query-keys';
import type { ChannelEventQueryRequest } from '@/models/channelEvent';

/**
 * Bir kabinin olay geçmişi.
 *
 * `keepPreviousData`: sayfa değiştirirken liste boşalıp yeniden dolmaz, eski
 * sayfa yeni veri gelene kadar ekranda kalır. Sayfalı bir tabloda alternatifi,
 * her ok tuşunda görünen bir boşluk olurdu.
 */
export function useChannelEvents(request: ChannelEventQueryRequest, enabled = true) {
  return useQuery({
    queryKey: channelEventKeys.list(request),
    queryFn: () => getChannelEvents(request),
    enabled: enabled && Boolean(request.cabinetId),
    placeholderData: keepPreviousData
  });
}
