import http from '@/lib/axios-helper';
import type { CreatedDto } from '@/models/common/createdDto';
import type { CabinetCreateRequest, CabinetDetailDto, CabinetStatusBreakdownDto, CabinetUpdateRequest, ScadaSyncResultDto } from '@/models/cabinet';

const CABINET_ROUTE = '/api/Cabinet';

/**
 * Liste PASİF kabinleri de döndürür — `IsActive` global query filter'ı bilerek
 * yok, pasif kaydı görüp geri alabilmek için. Ekran gerekiyorsa kendisi filtreler.
 */
export async function getCabinetList(): Promise<CabinetDetailDto[]> {
  return http.post<CabinetDetailDto[]>(`${CABINET_ROUTE}/list`, {});
}

export async function createCabinet(request: CabinetCreateRequest): Promise<CreatedDto> {
  return http.post<CreatedDto>(CABINET_ROUTE, request);
}

/**
 * Düzenleme formunun kaynağı — liste DEĞİL.
 *
 * `CabinetDetailDto` SCADA alanlarını (`scadaBaseUrl`, `scadaCommandTimeoutMs`,
 * `scadaIsEnabled`) taşımıyor; forma listeden dolduran her deneme o üç alanı
 * sessizce sıfırlar. Bu uç `CabinetUpdateDto`'nun tamamını döndürür.
 */
export async function getCabinetUpdateModel(id: string): Promise<CabinetUpdateRequest> {
  return http.get<CabinetUpdateRequest>(`${CABINET_ROUTE}/${id}/update`);
}

/** Başarıda gövdesiz 200 döner (`Result.Success()`). */
export async function updateCabinet(request: CabinetUpdateRequest): Promise<void> {
  return http.put(CABINET_ROUTE, request);
}

/** Kabin durumunun dökümü: hangi cihaz/kamera kabine ne katkı veriyor. Pasif ya da olmayan kabinde 404. */
export async function getCabinetStatusBreakdown(id: string): Promise<CabinetStatusBreakdownDto> {
  return http.get<CabinetStatusBreakdownDto>(`${CABINET_ROUTE}/${id}/status-breakdown`);
}

/**
 * "Kartı oku": kabinin kartını okuyup (`GET {ScadaBaseUrl}/status`) farklı çıkan kanal değerlerini yazar. Değerler tuvale `DiagramHub`
 * ile gelir; yanıt yalnızca özettir. Kart yanıt vermezse / hata dönerse / SCADA kapalıysa hata (ProblemDetails) döner. `ViewDiagram`.
 */
export async function syncCabinetFromScada(id: string): Promise<ScadaSyncResultDto> {
  return http.post<ScadaSyncResultDto>(`${CABINET_ROUTE}/${id}/scada-sync`, {});
}
