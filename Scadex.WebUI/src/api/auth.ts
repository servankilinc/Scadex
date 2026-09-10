import http, { ACCOUNT_ROUTE } from '@/lib/axios-helper';
import { getOrCreateDeviceId, setSession, clearSession, getSession, setAccessToken, setCurrentUser } from '@/lib/auth-session';
import type { LoginRequest, LoginResponse, SignUpRequest, SignUpResponse } from '@/models/auth';

export async function login(request: LoginRequest): Promise<LoginResponse> {
  const deviceId = getOrCreateDeviceId();
  const response = await http.post<LoginResponse>(`${ACCOUNT_ROUTE}/Login`, {
    userName: request.userName,
    password: request.password,
    deviceId,
    clientType: import.meta.env.VITE_CLIENT_TYPE
  });

  // Yanit oturumun tamamini tasir: token, kullanici, rol ve izinler. Sunucuda
  // ayri bir profil ucu yoktur, oturum tek istekte kurulur.
  setAccessToken(response.accessToken.token);
  setSession({ userId: response.user.id, deviceId: response.deviceId });
  setCurrentUser(response.user, response.roles ?? [], response.permissions);
  return response;
}

/**
 * Kayit OTURUM ACMAZ. SignUpResponse `user` tasimadigi icin (ve ayri bir profil
 * ucu olmadigi icin) buradan tam bir oturum kurulamaz; kullanici kayit sonrasi
 * bir kez giris yapar. Yanittaki accessToken bilerek kullanilmaz.
 */
export async function signUp(request: SignUpRequest): Promise<SignUpResponse> {
  const deviceId = getOrCreateDeviceId();
  return http.post<SignUpResponse>(`${ACCOUNT_ROUTE}/SignUp`, {
    userName: request.userName,
    email: request.email,
    fullName: request.fullName,
    companyId: request.companyId,
    phoneNumber: request.phoneNumber || null,
    password: request.password,
    deviceId,
    clientType: import.meta.env.VITE_CLIENT_TYPE
  });
}

// NOT: Otomatik token yenileme HENUZ EKLENMEDI. Backend hazir
// (POST /api/Account/RefreshAuth + HttpOnly cookie) ve sozlesme modeli
// models/auth/queries/refreshAuthResponse.ts altinda duruyor. Eklenirken
// tek ucuslu (single-flight) bir kuyruk sart: backend refresh token'i her
// kullanimda donduruyor, es zamanli iki refresh oturumu tumden dusuruyor.

export async function logout(): Promise<void> {
  const session = getSession();
  try {
    if (session) {
      await http.post(`${ACCOUNT_ROUTE}/Logout`, { userId: session.userId, deviceId: session.deviceId });
    }
  } finally {
    // Sunucu cagrisi basarisiz olsa bile yerel oturum MUTLAKA temizlenir;
    // aksi halde kullanici "cikis yaptim" sanip oturumda kalir.
    // clearSession() token/userId/user/rol/izin siler, deviceId'yi korur.
    clearSession();
  }
}

export async function revokeAll(): Promise<void> {
  try {
    await http.post(`${ACCOUNT_ROUTE}/RevokeAll`);
  } finally {
    // clearSession() token/userId/user/rol/izin siler, deviceId'yi korur.
    clearSession();
  }
}
