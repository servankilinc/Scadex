import http, { ACCOUNT_ROUTE } from '@/lib/axios-helper';
import type { UserOwnPasswordChangeRequest, UserProfileDto, UserProfileUpdateRequest } from '@/models/user';

/**
 * Giriş yapan kullanıcının kendi hesabı. Uçlar yalnızca giriş ister, izin istemez; kimlik token'dan okunur, gövdede yoktur.
 * Başkasının hesabı `api/user.ts`'tedir (`ManageUsers`).
 */

export async function getProfile(): Promise<UserProfileDto> {
  return http.get<UserProfileDto>(`${ACCOUNT_ROUTE}/profile`);
}

/** Güncel profili döner. E-posta çakışması 400 (`errors.Email`). */
export async function updateProfile(request: UserProfileUpdateRequest): Promise<UserProfileDto> {
  return http.put<UserProfileDto>(`${ACCOUNT_ROUTE}/profile`, {
    fullName: request.fullName,
    // `string?` alanda "" ile null aynı şey değildir; boş bırakmak e-postayı/telefonu kaldırır.
    email: request.email?.trim() || null,
    phoneNumber: request.phoneNumber?.trim() || null
  });
}

/** Yanlış mevcut parola 400 (`errors.CurrentPassword`), kural ihlali 400 (`errors.NewPassword`). Başarıda gövdesiz 200. */
export async function changeOwnPassword(request: UserOwnPasswordChangeRequest): Promise<void> {
  return http.put(`${ACCOUNT_ROUTE}/password`, request);
}
