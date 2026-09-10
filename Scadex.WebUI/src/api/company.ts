import http from '@/lib/axios-helper';
import type { CompanyCreateRequest, CompanyDto, CompanyUpdateRequest } from '@/models/company';

const COMPANY_ROUTE = '/api/Company';

/**
 * Liste PASİF firmaları da döndürür — `IsActive` global query filter'ı bilerek
 * yok, pasif kaydı görüp geri alabilmek için. Ekran gerekiyorsa kendisi filtreler.
 *
 * Kabin formundaki açılır liste de `GET /selectlist` yerine BUNU kullanır:
 * `SelectList` ucu sunucuda filtresiz çağrılıyor (pasifler dahil) ve dönen
 * `SelectItemDto` `isActive` taşımadığı için pasifi ayırt etmek imkânsız.
 * `CompanyDto` taşıyor, üstelik iki ekran aynı cache anahtarını paylaşıyor.
 */
export async function getCompanyList(): Promise<CompanyDto[]> {
  return http.post<CompanyDto[]>(`${COMPANY_ROUTE}/list`, {});
}

/**
 * Başarıda gövdesiz 200 döner.
 *
 * Sözleşmedeki "her Create `{ id }` döndürür" kuralının İSTİSNASI:
 * `CompanyService.CreateAsync` `Result<CreatedDto>` değil düz `Result` dönüyor.
 * Kabinin aksine firmanın id'sine anında ihtiyaç duyan bir akış yok.
 */
export async function createCompany(request: CompanyCreateRequest): Promise<void> {
  return http.post(COMPANY_ROUTE, request);
}

/**
 * Başarıda gövdesiz 200 döner (`Result.Success()`).
 *
 * `GET /{id}/update` ucu BİLEREK kullanılmıyor: `CompanyUpdateDto` yalnızca `id`,
 * `name` ve `isActive` taşıyor, üçü de listede zaten var. (Kabinde durum farklı —
 * SCADA alanları listede olmadığı için orada o uç şart.)
 */
export async function updateCompany(request: CompanyUpdateRequest): Promise<void> {
  return http.put(COMPANY_ROUTE, request);
}
