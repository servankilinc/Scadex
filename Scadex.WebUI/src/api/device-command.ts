import http from '@/lib/axios-helper';
import type { DeviceCommandResultDto, DeviceCommandSendRequest } from '@/models/deviceCommand';

/**
 * Sözleşme: `Backend/docs/api-contract/08-scada-command.md`
 *
 * Rota büyük/küçük harfe DUYARLIDIR: backend `api/[controller]` kullandığı için
 * doğru yazım `/api/Device`'tır.
 */
const DEVICE_ROUTE = '/api/Device';

/**
 * Cihaza kumanda gönderir ve **sonucu bekler**.
 *
 * Bu çağrı SCADA cevaplayana ya da zaman aşımına kadar dönmez — kabinin
 * `scadaCommandTimeoutMs` ayarı, en az 10 saniye. Arayüz bu süre boyunca
 * bekleyen bir durum göstermek zorunda.
 *
 * Alternatifi 202 + sonucu yalnızca SignalR'dan almaktı; seçilmedi çünkü o
 * durumda komutu gönderen kişinin sonucu görmesi canlı bağlantının ayakta
 * olmasına bağlı kalırdı.
 */
export async function sendDeviceCommand(deviceId: string, request: DeviceCommandSendRequest): Promise<DeviceCommandResultDto> {
  return http.post<DeviceCommandResultDto>(`${DEVICE_ROUTE}/${deviceId}/command`, request);
}

/** Cihazın son kumandaları, yeniden eskiye. `take` sunucuda 1–100 arasına kırpılır. */
export async function getDeviceCommands(deviceId: string, take = 20): Promise<DeviceCommandResultDto[]> {
  return http.get<DeviceCommandResultDto[]>(`${DEVICE_ROUTE}/${deviceId}/commands?take=${take}`);
}
