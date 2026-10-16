import http from '@/lib/axios-helper';
import type { RoleCreateRequest, RoleDto, RoleMemberDto, RoleUpdateRequest } from '@/models/role';
import type { RolePermissionDto, RolePermissionMatrixItem } from '@/models/rolePermission';

const ROLE_ROUTE = '/api/Role';
const ROLE_PERMISSION_ROUTE = '/api/RolePermission';
const USER_ROLE_ROUTE = '/api/UserRole';

/**
 * Rol / izin uçları. Yazmaların hepsi `ManageUsers` ister ve rolü taşıyan kullanıcıların BİR SONRAKİ girişinde geçerli olur
 * (izinler token'a girişte yazılır).
 */

/** Liste PASİF rolleri de döndürür. `isImmutable` sistem rollerini ayırt eder. Yalnızca giriş ister (Kurumlar ekranı da okur). */
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
 * Bilinmeyen izin 400 (`errors.PermissionIds`).
 */
export async function syncRolePermissions(roleId: string, permissionIds: number[]): Promise<void> {
  return http.put(`${ROLE_PERMISSION_ROUTE}/role/${roleId}/sync`, permissionIds);
}

/** Her rol (pasif ve izinsizler dahil) × izin kimlikleri. */
export async function getPermissionMatrix(): Promise<RolePermissionMatrixItem[]> {
  return http.get<RolePermissionMatrixItem[]>(`${ROLE_PERMISSION_ROUTE}/matrix`);
}

/** Yalnızca DEĞİŞEN rolleri gönderin: sunucu hepsini tek transaction'da eşitler, gönderilmeyen rollere dokunmaz. */
export async function savePermissionMatrix(items: RolePermissionMatrixItem[]): Promise<void> {
  return http.put(`${ROLE_PERMISSION_ROUTE}/matrix`, items);
}

/** Role atanmış kullanıcılar (pasifler dahil), ada göre sıralı. */
export async function getRoleMembers(roleId: string): Promise<RoleMemberDto[]> {
  return http.get<RoleMemberDto[]>(`${USER_ROLE_ROUTE}/role/${roleId}/users`);
}

/** Zaten üyeyse işlem yapmaz. Pasif role üye eklemek 400. */
export async function addRoleMember(roleId: string, userId: string): Promise<void> {
  return http.put(`${USER_ROLE_ROUTE}/role/${roleId}/users/${userId}`);
}

/** Üye değilse işlem yapmaz. */
export async function removeRoleMember(roleId: string, userId: string): Promise<void> {
  return http.delete(`${USER_ROLE_ROUTE}/role/${roleId}/users/${userId}`);
}
