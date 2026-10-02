/** Ayna: Scadex.Model/Dtos/ComponentTemplate/Queries/ComponentTemplateTypeCountDto.cs */
import type { DeviceType } from '@/models/enums';

/**
 * Bir cihaz tipindeki AKTİF şablon sayısı — `GET /api/ComponentTemplate/type-counts`.
 * Şablonu olmayan tip listede YOKTUR; eksik tip 0 sayılır.
 */
export interface ComponentTemplateTypeCountDto {
  deviceTypeId: DeviceType;
  count: number;
}
