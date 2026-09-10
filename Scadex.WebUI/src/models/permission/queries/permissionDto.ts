/** Ayna: Scadex.Model/Dtos/Permission/Queries/PermissionDto.cs — seed'li, salt okunur katalog. */
export interface PermissionDto {
  id: number;
  /** `EntityEnums.Permission` üyesinin adı; token'daki `permission` claim'i budur. */
  code: string;
  displayName: string;
  category: string;
  createdBy: string | null;
  updatedBy: string | null;
  createDateUtc: string | null;
  updateDateUtc: string | null;
}
