import http from '@/lib/axios-helper';
import type { UserCreateRequest, UserDetailDto, UserUpdateRequest } from '@/models/user';

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
  return http.post(USER_ROUTE, { ...request, email: emptyToNull(request.email), phoneNumber: emptyToNull(request.phoneNumber) });
}

/**
 * Başarıda gövdesiz 200 döner.
 *
 * `GET /{id}/update` ucu BİLEREK kullanılmıyor: `UserUpdateDto`'nun dört alanı da
 * listede zaten var. Kendi hesabını pasife almak 400'dür (`errors.IsActive`).
 */
export async function updateUser(request: UserUpdateRequest): Promise<void> {
  return http.put(USER_ROUTE, { ...request, phoneNumber: emptyToNull(request.phoneNumber) });
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
