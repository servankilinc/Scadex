import http from '@/lib/axios-helper';
import type { CreatedDto } from '@/models/common/createdDto';
import type {
  ComponentTemplateCreateRequest,
  ComponentTemplateListItemDto,
  ComponentTemplatePaletteDto,
  ComponentTemplateTypeCountDto,
  TemplateImageDto
} from '@/models/componentTemplate';
import type { DeviceType } from '@/models/enums';

/** Büyük/küçük harfe DUYARLI — küçük harfli `/api/componenttemplate` eşleşmez. */
const COMPONENT_TEMPLATE_ROUTE = '/api/ComponentTemplate';

/** Yalnızca aktif şablonlar döner. */
export async function getPalette(): Promise<ComponentTemplatePaletteDto[]> {
  return http.get<ComponentTemplatePaletteDto[]>(`${COMPONENT_TEMPLATE_ROUTE}/palette`);
}

/** Tip başına aktif şablon sayısı; şablonu olmayan tip listede yok. */
export async function getTemplateTypeCounts(): Promise<ComponentTemplateTypeCountDto[]> {
  return http.get<ComponentTemplateTypeCountDto[]>(`${COMPONENT_TEMPLATE_ROUTE}/type-counts`);
}

/** Bir tipin aktif şablonları — pin şeması olmadan, yalnızca pin sayısıyla. */
export async function getTemplatesByType(deviceTypeId: DeviceType): Promise<ComponentTemplateListItemDto[]> {
  return http.get<ComponentTemplateListItemDto[]>(`${COMPONENT_TEMPLATE_ROUTE}/by-type/${deviceTypeId}`);
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
