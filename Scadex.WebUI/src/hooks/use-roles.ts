import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { getPermissionList } from '@/api/permission';
import { createRole, getRoleList, getRolePermissions, syncRolePermissions, updateRole } from '@/api/role';
import { permissionKeys, roleKeys, userKeys } from '@/api/query-keys';
import type { RoleCreateRequest, RoleUpdateRequest } from '@/models/role';

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
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: RoleUpdateRequest) => updateRole(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.all });
      // Kullanıcının rolleri ADLA döner; ad değişince önbellekteki listeler eski adı taşır.
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success('Rol güncellendi.');
    }
  });
}

export function useSyncRolePermissions() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ roleId, permissionIds }: { roleId: string; permissionIds: number[] }) => syncRolePermissions(roleId, permissionIds),
    onSuccess: (_data, { roleId }) => {
      void queryClient.invalidateQueries({ queryKey: roleKeys.permissions(roleId) });
      toast.success('İzinler kaydedildi.');
    }
  });
}
