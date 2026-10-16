import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { getPermissionList } from '@/api/permission';
import {
  addRoleMember,
  createRole,
  getPermissionMatrix,
  getRoleList,
  getRoleMembers,
  getRolePermissions,
  removeRoleMember,
  savePermissionMatrix,
  syncRolePermissions,
  updateRole
} from '@/api/role';
import { permissionKeys, roleKeys, userKeys } from '@/api/query-keys';
import { getCurrentUser } from '@/lib/auth-session';
import type { RoleCreateRequest, RoleUpdateRequest } from '@/models/role';
import type { RolePermissionMatrixItem } from '@/models/rolePermission';

/** Yetki yazımı token'a girişte yansır; başarı bildirimleri bunu söyler. */
export const RELOGIN_NOTICE = 'Değişiklik, ilgili kullanıcıların bir sonraki girişinde geçerli olur.';

/** Rol kartları ve kullanıcı Roller dialogu — pasifler DAHİL. */
export function useRoles() {
  return useQuery({
    queryKey: roleKeys.list(),
    queryFn: getRoleList
  });
}

/** İzinler dialogunun kaynağı. `roleId` null iken istek atılmaz. */
export function useRolePermissions(roleId: string | null) {
  return useQuery({
    queryKey: roleKeys.permissions(roleId ?? ''),
    queryFn: () => getRolePermissions(roleId!),
    enabled: roleId != null
  });
}

/** Seed'li katalog, çalışma anında değişmez — bir kez çekilir. */
export function usePermissions() {
  return useQuery({
    queryKey: permissionKeys.list(),
    queryFn: getPermissionList,
    staleTime: Infinity
  });
}

/** Her rol × izin kimlikleri — Yetki Matrisi ve kullanıcı Roller dialogundaki "etkin izinler" önizlemesi. */
export function usePermissionMatrix() {
  return useQuery({
    queryKey: roleKeys.matrix(),
    queryFn: getPermissionMatrix
  });
}

/** Üyeler dialogunun kaynağı. `roleId` null iken istek atılmaz. */
export function useRoleMembers(roleId: string | null) {
  return useQuery({
    queryKey: roleKeys.members(roleId ?? ''),
    queryFn: () => getRoleMembers(roleId!),
    enabled: roleId != null
  });
}

/**
 * Rol-izin ve rol-üye yazımları rol listesini (kullanıcı sayısı), matrisi ve kullanıcı listesini (roller sütunu) birlikte
 * etkiler; hepsi tazelenir.
 */
function useInvalidateAuthorization() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: roleKeys.all });
    void queryClient.invalidateQueries({ queryKey: userKeys.all });
  };
}

/** `onError` BİLEREK yok — `handleFormApiError` çağıran formda kurulur. */
export function useCreateRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: RoleCreateRequest) => createRole(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      toast.success('Rol oluşturuldu.');
    }
  });
}

export function useUpdateRole() {
  // Kullanıcının rolleri ADLA döner; ad değişince önbellekteki listeler eski adı taşır.
  const invalidate = useInvalidateAuthorization();

  return useMutation({
    mutationFn: (request: RoleUpdateRequest) => updateRole(request),
    onSuccess: () => {
      invalidate();
      toast.success('Rol güncellendi.', { description: RELOGIN_NOTICE });
    }
  });
}

export function useSyncRolePermissions() {
  const invalidate = useInvalidateAuthorization();

  return useMutation({
    mutationFn: ({ roleId, permissionIds }: { roleId: string; permissionIds: number[] }) => syncRolePermissions(roleId, permissionIds),
    onSuccess: () => {
      invalidate();
      toast.success('İzinler kaydedildi.', { description: RELOGIN_NOTICE });
    }
  });
}

/** Matris kaydı — yalnızca değişen roller gönderilir. Hata politikası ekranda (toast). */
export function useSavePermissionMatrix() {
  const invalidate = useInvalidateAuthorization();

  return useMutation({
    mutationFn: (items: RolePermissionMatrixItem[]) => savePermissionMatrix(items),
    onSuccess: (_data, items) => {
      invalidate();
      toast.success(`${items.length} rolün izinleri kaydedildi.`, { description: RELOGIN_NOTICE });
    }
  });
}

export function useAddRoleMember() {
  const invalidate = useInvalidateAuthorization();

  return useMutation({
    mutationFn: ({ roleId, userId }: { roleId: string; userId: string }) => addRoleMember(roleId, userId),
    onSuccess: (_data, { userId }) => {
      invalidate();
      toast.success('Kullanıcı role eklendi.', { description: selfAwareNotice(userId) });
    }
  });
}

export function useRemoveRoleMember() {
  const invalidate = useInvalidateAuthorization();

  return useMutation({
    mutationFn: ({ roleId, userId }: { roleId: string; userId: string }) => removeRoleMember(roleId, userId),
    onSuccess: (_data, { userId }) => {
      invalidate();
      toast.success('Kullanıcı rolden çıkarıldı.', { description: selfAwareNotice(userId) });
    }
  });
}

/** Değişen kullanıcı oturumu açan kişiyse bildirim ona hitap eder — kendi menüsü yeniden girişe kadar değişmez. */
export function selfAwareNotice(userId: string): string {
  return getCurrentUser()?.id === userId ? 'Yetkileriniz bir sonraki girişinizde güncellenecek.' : RELOGIN_NOTICE;
}
