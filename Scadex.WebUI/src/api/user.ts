import http from '@/lib/axios-helper';
import type {
  UserCardHolderDto,
  UserCreateRequest,
  UserDetailDto,
  UserIdentityCardAssignRequest,
  UserPasswordChangeRequest,
  UserUpdateRequest
} from '@/models/user';

const USER_ROUTE = '/api/User';
const USER_ROLE_ROUTE = '/api/UserRole';

/** Formdaki boş metin sunucuya `null` gider — `string?` alanda "" ile null aynı şey değildir. */
function emptyToNull(value: string | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

/**
 * Liste PASİF kullanıcıları da döndürür — `IsActive` global query filter'ı bilerek
 * yok, pasif kaydı görüp geri alabilmek için.
 *
 * `list/detail` kullanılır, `list` DEĞİL: `UserBaseDto` kullanıcı adını, e-postayı,
 * firma adını ve `isActive`'i taşımıyor.
 */
export async function getUserList(): Promise<UserDetailDto[]> {
  return http.post<UserDetailDto[]>(`${USER_ROUTE}/list/detail`, {});
}

/**
 * Başarıda gövdesiz 200 döner — `UserService.CreateAsync` düz `Result` dönüyor
 * (firmadaki gibi "Create `{ id }` döndürür" kuralının istisnası). Kullanıcı aktif doğar.
 */
export async function createUser(request: UserCreateRequest): Promise<void> {
  return http.post(USER_ROUTE, {
    ...request,
    email: emptyToNull(request.email),
    phoneNumber: emptyToNull(request.phoneNumber),
    identityCardId: emptyToNull(request.identityCardId)
  });
}

/**
 * Başarıda gövdesiz 200 döner.
 *
 * `GET /{id}/update` ucu BİLEREK kullanılmıyor: `UserUpdateDto`'nun tüm alanları
 * listede zaten var. Kendi hesabını pasife almak 400'dür (`errors.IsActive`).
 */
export async function updateUser(request: UserUpdateRequest): Promise<void> {
  return http.put(USER_ROUTE, {
    ...request,
    phoneNumber: emptyToNull(request.phoneNumber),
    // Boş gönderim kartı KALDIRIR (sunucu "" → null yapar); alan her zaman gönderilir.
    identityCardId: emptyToNull(request.identityCardId)
  });
}

/**
 * Parolayı eskisini sormadan değiştirir (`ManageUsers`). Kullanıcının refresh token'ları iptal edilir ve kilidi sıfırlanır.
 * Parola kuralı ihlali 400 (`errors.NewPassword`). Başarıda gövdesiz 200.
 */
export async function changeUserPassword(userId: string, request: UserPasswordChangeRequest): Promise<void> {
  return http.put(`${USER_ROUTE}/${userId}/password`, request);
}

/** Kart tanımlama penceresinin listesi: AKTİF kullanıcılar, yalnızca ad / kullanıcı adı / kart (`ManageAccessCards`). */
export async function getCardHolders(): Promise<UserCardHolderDto[]> {
  return http.get<UserCardHolderDto[]>(`${USER_ROUTE}/card-holders`);
}

/** Okutulan kartı kullanıcıya yazar — YALNIZCA kart alanı (Kabinler → "Kart tanımlama"). Başarıda gövdesiz 200. */
export async function assignUserIdentityCard(userId: string, request: UserIdentityCardAssignRequest): Promise<void> {
  return http.put(`${USER_ROUTE}/${userId}/identity-card`, request);
}

/** Kullanıcının rol ADLARI — Identity kimlik değil ad döndürür. */
export async function getUserRoles(userId: string): Promise<string[]> {
  return http.get<string[]>(`${USER_ROLE_ROUTE}/user/${userId}`);
}

/**
 * Rol kümesini verilen adlarla birebir eşitler (ekle + çıkar), sunucuda tek transaction.
 * Tanımsız ad ya da yeni eklenen pasif rol 400 döner (`errors.roleNames`).
 */
export async function syncUserRoles(userId: string, roleNames: string[]): Promise<void> {
  return http.put(`${USER_ROLE_ROUTE}/user/${userId}/sync`, roleNames);
}
