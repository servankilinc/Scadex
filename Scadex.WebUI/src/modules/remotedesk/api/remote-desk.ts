/**
 * RemoteDesk modülünün uçları (`api/RemoteDesk/...`). Backend'de `Modules:RemoteDesk:Enabled` kapalıysa HEPSİ 404 döner.
 */
import http from '@/lib/axios-helper';
import type { PcDetailDto, PcListItemDto, ScreenViewDto } from '../models/pc';

const ROUTE = '/api/RemoteDesk';

export async function getPcs(): Promise<PcListItemDto[]> {
  return http.get<PcListItemDto[]>(`${ROUTE}/pcs`);
}

export async function getPc(deviceId: string): Promise<PcDetailDto> {
  return http.get<PcDetailDto>(`${ROUTE}/pcs/${deviceId}`);
}

/**
 * İzlemeyi başlatır. Yayın yoksa sunucu PC'ye komut gönderir ve yol hazır olana kadar BEKLER — ilk yayında (PC kodlayıcıları
 * sınarken) 10–20 sn sürebilir, sonrakiler 1–3 sn. 400 = PC bağlı değil / monitör yok; 500 = PC yayını başlatamadı (başlıkta neden).
 */
export async function startScreenView(deviceId: string, monitorIndex: number, signal?: AbortSignal): Promise<ScreenViewDto> {
  return http.post<ScreenViewDto>(`${ROUTE}/pcs/${deviceId}/monitors/${monitorIndex}/view`, undefined, { signal, timeout: 60_000 });
}

/** 404 = izleme sona erdi (kiralama düştü ya da yayın durdu). */
export async function renewScreenView(viewId: string): Promise<void> {
  return http.post(`${ROUTE}/views/${viewId}/renew`);
}

/** Bilinmeyen kiralamada da 200. */
export async function releaseScreenView(viewId: string): Promise<void> {
  return http.delete(`${ROUTE}/views/${viewId}`);
}
