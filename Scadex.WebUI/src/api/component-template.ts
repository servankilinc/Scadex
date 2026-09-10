import http from '@/lib/axios-helper';
import type { CreatedDto } from '@/models/common/createdDto';
import type { ComponentTemplateCreateRequest, ComponentTemplatePaletteDto, TemplateImageDto } from '@/models/componentTemplate';

/** Büyük/küçük harfe DUYARLI — küçük harfli `/api/componenttemplate` eşleşmez. */
const COMPONENT_TEMPLATE_ROUTE = '/api/ComponentTemplate';

/** Yalnızca aktif şablonlar döner. */
export async function getPalette(): Promise<ComponentTemplatePaletteDto[]> {
  return http.get<ComponentTemplatePaletteDto[]>(`${COMPONENT_TEMPLATE_ROUTE}/palette`);
}

/**
 * Şablonu ve pin şemasını TEK transaction'da oluşturur.
 */
export async function createComponentTemplate(request: ComponentTemplateCreateRequest): Promise<CreatedDto> {
  return http.post<CreatedDto>(COMPONENT_TEMPLATE_ROUTE, request);
}

/**
 * Şablon arka plan görselini yükler; URL döner.
 */
export async function uploadTemplateImage(file: File): Promise<TemplateImageDto> {
  const form = new FormData();
  form.append('file', file);
  return http.post<TemplateImageDto>(`${COMPONENT_TEMPLATE_ROUTE}/image`, form);
}
