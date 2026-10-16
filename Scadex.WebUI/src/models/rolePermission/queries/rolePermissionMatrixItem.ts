/**
 * Ayna: Scadex.Model/Dtos/RolePermission/Queries/RolePermissionMatrixItemDto.cs (okuma) ve
 * Commands/RolePermissionMatrixSaveItem.cs (yazma) — iki DTO aynı biçimdedir.
 *
 * Okumada her rol (pasif ve izinsizler dahil) tam bir kez gelir. Yazmada yalnızca DEĞİŞEN roller gönderilir; gönderilmeyen
 * rollere sunucu dokunmaz.
 */
export interface RolePermissionMatrixItem {
  roleId: string;
  permissionIds: number[];
}
