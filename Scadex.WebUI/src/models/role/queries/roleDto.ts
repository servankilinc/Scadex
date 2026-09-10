/** Ayna: Scadex.Model/Dtos/Role/Queries/RoleDto.cs */
export interface RoleDto {
  id: string;
  name: string;
  /** Sistem rolü: adı ve aktifliği değiştirilemez (sunucu 403 döner). İzinleri düzenlenebilir. */
  isImmutable: boolean;
  createdBy: string | null;
  updatedBy: string | null;
  /** `Z` soneki YOK (`datetime2`) — gösterilecekse `toUtcDate` ile. */
  createDateUtc: string | null;
  updateDateUtc: string | null;
  isActive: boolean;
}
