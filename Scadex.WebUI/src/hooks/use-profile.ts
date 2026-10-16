import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { changeOwnPassword, getProfile, updateProfile } from '@/api/profile';
import { userKeys } from '@/api/query-keys';
import { updateCurrentUser } from '@/lib/auth-session';
import type { UserOwnPasswordChangeRequest, UserProfileUpdateRequest } from '@/models/user';

/** Giriş yapan kullanıcının kendi profili — izin istemez. */
export function useProfile() {
  return useQuery({
    queryKey: userKeys.profile(),
    queryFn: getProfile
  });
}

/**
 * Profil mutation'ları. `onError` BİLEREK yok: hata politikası formda (`handleFormApiError`), `use-users.ts` ile aynı.
 */
export function useUpdateProfile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: UserProfileUpdateRequest) => updateProfile(request),
    onSuccess: profile => {
      queryClient.setQueryData(userKeys.profile(), profile);
      // Yönetici listesi de aynı satırı gösterir; profil anahtarı zaten yazıldı, yalnızca diğerleri tazelenir.
      void queryClient.invalidateQueries({ queryKey: userKeys.all, predicate: query => query.queryKey[1] !== 'profile' });
      // Menüdeki ad oturum kopyasından okunur (giriş yanıtı); yeniden giriş beklemesin.
      updateCurrentUser({ fullName: profile.fullName });
      toast.success('Profil güncellendi.');
    }
  });
}

export function useChangeOwnPassword() {
  return useMutation({
    mutationFn: (request: UserOwnPasswordChangeRequest) => changeOwnPassword(request),
    onSuccess: () => {
      toast.success('Parolanız değiştirildi. Diğer cihazlardaki oturumlarınız yeniden giriş isteyecek.');
    }
  });
}
