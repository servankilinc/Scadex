import http from '@/lib/axios-helper';
import type { RoleCreateRequest, RoleDto, RoleUpdateRequest } from '@/models/role';
import type { RolePermissionDto } from '@/models/rolePermission';

const ROLE_ROUTE = '/api/Role';
const ROLE_PERMISSION_ROUTE = '/api/RolePermission';

/** Liste PASİF rolleri de döndürür. `isImmutable` sistem rollerini ayırt eder. */
export async function getRoleList(): Promise<RoleDto[]> {
  return http.post<RoleDto[]>(`${ROLE_ROUTE}/list`, {});
}

/** Başarıda gövdesiz 200 döner (`RoleService.CreateAsync` düz `Result`). Rol aktif doğar. */
export async function createRole(request: RoleCreateRequest): Promise<void> {
  return http.post(ROLE_ROUTE, request);
}

/** Başarıda gövdesiz 200 döner. Sistem rolünde (`isImmutable`) sunucu 403 döner. */
export async function updateRole(request: RoleUpdateRequest): Promise<void> {
  return http.put(ROLE_ROUTE, request);
}

export async function getRolePermissions(roleId: string): Promise<RolePermissionDto[]> {
  return http.get<RolePermissionDto[]>(`${ROLE_PERMISSION_ROUTE}/role/${roleId}`);
}

/**
 * Rolün izin kümesini birebir eşitler, sunucuda tek transaction.
 *
 * Sistem rollerinde de SERBEST: seed'deki dört rolün hepsi `isImmutable` ve kilit
 * yalnızca ad ile aktifliği kapsıyor — aksi halde bu ekran seed rolleri için işe yaramazdı.
 */
export async function syncRolePermissions(roleId: string, permissionIds: number[]): Promise<void> {
  return http.put(`${ROLE_PERMISSION_ROUTE}/role/${roleId}/sync`, permissionIds);
}
