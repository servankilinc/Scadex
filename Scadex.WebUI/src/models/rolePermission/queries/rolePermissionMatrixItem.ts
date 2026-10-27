/**
 * Ayna: Scadex.Model/Dtos/RolePermission/Queries/RolePermissionMatrixItemDto.cs.
 *
 * Her rol (pasif ve izinsizler dahil) tam bir kez gelir.
 */
export interface RolePermissionMatrixItem {
  roleId: string;
  permissionIds: number[];
}
