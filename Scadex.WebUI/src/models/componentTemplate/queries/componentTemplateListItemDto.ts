/** Ayna: Scadex.Model/Dtos/ComponentTemplate/Queries/ComponentTemplateListItemDto.cs */
import type { DeviceType } from '@/models/enums';

/**
 * Şablon yönetim tablosunun satırı — `GET /api/ComponentTemplate/by-type/{deviceTypeId}`.
 *
 * Palet DTO'sunun aksine pin şeması TAŞIMAZ (listenin en ağır kısmı); `pinCount` sunucuda
 * sorguda hesaplanır, saklanan bir alan değildir.
 */
export interface ComponentTemplateListItemDto {
  id: string;
  name: string;
  deviceTypeId: DeviceType;
  isSystemTemplate: boolean;
  width: number;
  height: number;
  /** `#RRGGBB` renk dizesi. */
  backgroundColor: string;
  backgroundImageUrl: string | null;
  isMonitorable: boolean;
  pinCount: number;
}
