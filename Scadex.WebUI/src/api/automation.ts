import http from '@/lib/axios-helper';
import type { CreatedDto } from '@/models/common/createdDto';
import type { AutomationCreateRequest, AutomationDetailDto, AutomationStatusBreakdownDto, AutomationUpdateRequest, ScadaSyncResultDto } from '@/models/automation';

const AUTOMATION_ROUTE = '/api/Automation';

/**
 * Liste PASİF otomasyonları da döndürür — `IsActive` global query filter'ı bilerek
 * yok, pasif kaydı görüp geri alabilmek için. Ekran gerekiyorsa kendisi filtreler.
 */
export async function getAutomationList(): Promise<AutomationDetailDto[]> {
  return http.post<AutomationDetailDto[]>(`${AUTOMATION_ROUTE}/list`, {});
}

export async function createAutomation(request: AutomationCreateRequest): Promise<CreatedDto> {
  return http.post<CreatedDto>(AUTOMATION_ROUTE, request);
}

/**
 * Düzenleme formunun kaynağı — liste DEĞİL.
 *
 * Form `AutomationUpdateDto`'nun şeklidir (`isUnderMaintenance` dahil); listenin `AutomationDetailDto`'su başka
 * bir şekildir. Bu uç `AutomationUpdateDto`'nun tamamını döndürür.
 */
export async function getAutomationUpdateModel(id: string): Promise<AutomationUpdateRequest> {
  return http.get<AutomationUpdateRequest>(`${AUTOMATION_ROUTE}/${id}/update`);
}

/** Başarıda gövdesiz 200 döner (`Result.Success()`). */
export async function updateAutomation(request: AutomationUpdateRequest): Promise<void> {
  return http.put(AUTOMATION_ROUTE, request);
}

/** Otomasyon durumunun dökümü: hangi cihaz/kamera otomasyona ne katkı veriyor. Pasif ya da olmayan otomasyonda 404. */
export async function getAutomationStatusBreakdown(id: string): Promise<AutomationStatusBreakdownDto> {
  return http.get<AutomationStatusBreakdownDto>(`${AUTOMATION_ROUTE}/${id}/status-breakdown`);
}

/**
 * "Kartı oku": otomasyonun kartını okuyup (`GET {ScadaBaseUrl}/status`) farklı çıkan kanal değerlerini yazar. Değerler tuvale `DiagramHub`
 * ile gelir; yanıt yalnızca özettir. Kart yanıt vermezse / hata dönerse / SCADA kapalıysa hata (ProblemDetails) döner. `ViewDiagram`.
 */
export async function syncAutomationFromScada(id: string): Promise<ScadaSyncResultDto> {
  return http.post<ScadaSyncResultDto>(`${AUTOMATION_ROUTE}/${id}/scada-sync`, {});
}
