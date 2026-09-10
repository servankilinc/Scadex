import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { createUser, getUserList, getUserRoles, syncUserRoles, updateUser } from '@/api/user';
import { userKeys } from '@/api/query-keys';
import type { UserCreateRequest, UserUpdateRequest } from '@/models/user';

/** Kullanıcı kartları — pasifler DAHİL (geri alınabilmeleri için). */
export function useUsers() {
  return useQuery({
    queryKey: userKeys.list(),
    queryFn: getUserList
  });
}

/** Roller dialogunun kaynağı. `userId` null iken istek atılmaz. */
export function useUserRoles(userId: string | null) {
  return useQuery({
    queryKey: userKeys.roles(userId ?? ''),
    queryFn: () => getUserRoles(userId!),
    enabled: userId != null
  });
}

/**
 * Kullanıcı mutation'ları.
 *
 * `onError` BİLEREK yok: hata politikası `handleFormApiError` ile forma ait ve
 * `form.setError`'a ihtiyaç duyar (firma hook'larıyla aynı politika). Sunucu
 * gövdesiz 200 döndüğü için `setQueryData` ile yazılacak veri de yok — invalidate.
 */
export function useCreateUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: UserCreateRequest) => createUser(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success('Kullanıcı oluşturuldu.');
    }
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: UserUpdateRequest) => updateUser(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: userKeys.all });
      toast.success('Kullanıcı güncellendi.');
    }
  });
}

export function useSyncUserRoles() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ userId, roleNames }: { userId: string; roleNames: string[] }) => syncUserRoles(userId, roleNames),
    onSuccess: (_data, { userId }) => {
      void queryClient.invalidateQueries({ queryKey: userKeys.roles(userId) });
      toast.success('Roller kaydedildi.');
    }
  });
}
