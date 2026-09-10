import { useCallback, useSyncExternalStore } from 'react';
import type { UserBaseDto } from '@/models/user';

/**
 * Oturuma dair HER SEY bu dosyada: localStorage'daki tek anahtar, degisiklikleri
 * React'e tasiyan kucuk store ve izin kontrolu.
 *
 * Icerik:
 *  - accessToken : her istege Bearer olarak eklenir
 *  - deviceId    : Login/Logout govdesinde gonderilir; backend her cihaz icin
 *                  ayri bir oturum zinciri tutar. Cikista/401'de SILINMEZ -
 *                  cihaz kimligi oturumdan uzun yasar.
 *  - userId      : Logout govdesinde gonderilir ve ileride eklenecek
 *                  RefreshAuth cagrisi icin gerekli olacak
 *  - user        : LoginResponse.user (UserBaseDto)
 *  - roles       : LoginResponse.roles
 *  - permissions : LoginResponse.permissions
 *
 * user/roles/permissions ucu de girise verilir; sunucuda ayri bir profil ucu
 * YOKTUR. Burada durduklari icin sayfa yenilendiginde ek istek atilmadan
 * render edilir.
 *
 * userId/deviceId/user SIR DEGILDIR - gercek sir olan refresh token HttpOnly
 * cookie'de durur ve JS'ten okunamaz. Buradaki izin listesi de YALNIZCA UX
 * icindir (menu/buton gizleme); gercek yetkilendirme backend'de policy ile
 * yapilir ve bu dosya atlatilabilir.
 *
 * Bu dosya BILEREK hicbir proje modulu import etmez (yalnizca react + tip).
 * axios-helper ve api/auth buradan okur; ters yonde bagimlilik olusursa
 * dongusel import ortaya cikar.
 */
const STORAGE_KEY = 'scadex_auth_session';

/** Oturum kimligi. Ancak ikisi de varsa anlamlidir. */
export interface AuthSession {
  userId: string;
  deviceId: string;
}

interface StoredSession {
  accessToken?: string;
  userId?: string;
  deviceId?: string;
  user?: UserBaseDto;
  roles?: string[];
  permissions?: string[];
}

// --- Depolama + bellek ici snapshot ---

function readStorage(): StoredSession {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredSession) : {};
  } catch {
    // Private mode / bozuk JSON: oturum yok kabul edilir.
    return {};
  }
}

/**
 * useSyncExternalStore getSnapshot'tan REFERANS OLARAK KARARLI bir deger bekler;
 * her cagrida JSON.parse etmek sonsuz render dongusune yol acardi. Bu yuzden
 * tek dogruluk kaynagi bellekteki bu nesnedir, localStorage onun yansimasidir.
 */
let cache: StoredSession = readStorage();

function write(next: StoredSession): void {
  cache = next;
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    /* storage yoksa sessizce gec; oturum yalnizca bu sekme icin calisir */
  }
  emit();
}

/** Var olan kaydin uzerine YAZMAZ, birlestirir - parcalar farkli anlarda gelir. */
function merge(patch: StoredSession): void {
  write({ ...cache, ...patch });
}

// --- Reaktif katman ---

const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function getSnapshot(): StoredSession {
  return cache;
}

// Sekmeler arasi senkron: bir sekmede cikis yapilinca digeri de kendiliginden
// /login'e duser. 'storage' olayi YALNIZCA diger sekmelerden tetiklenir, bu
// yuzden kendi yazmalarimizla dongu olusmaz.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', event => {
    if (event.key !== STORAGE_KEY) return;
    cache = readStorage();
    emit();
  });
}

// --- Access token ---

export function getAccessToken(): string | null {
  return cache.accessToken ?? null;
}

export function setAccessToken(token: string | null): void {
  merge({ accessToken: token ?? undefined });
}

// NOT: Tek basina "token'i sil" diye bir islem YOK. Token'i silip user'i birakmak
// anlamsiz bir ara durum uretirdi; deviceId zaten clearSession()'da korunuyor.

// --- Oturum kimligi ---

export function getSession(): AuthSession | null {
  const { userId, deviceId } = cache;
  if (!userId || !deviceId) return null;
  return { userId, deviceId };
}

export function setSession(session: AuthSession): void {
  merge(session);
}

/**
 * Token, oturum kimligi, kullanici, rol ve izinleri siler. deviceId BILEREK
 * KORUNUR: o oturuma degil CIHAZA aittir. Silinseydi bir sonraki giriste yeni
 * bir kimlik uretilir, backend'de her cikis sonrasi yeni bir refresh zinciri
 * acilir ve "bu cihazdan cikis yap" anlamini yitirirdi.
 */
export function clearSession(): void {
  write({ deviceId: cache.deviceId });
}

/**
 * Cihaz kimligi. Girise deviceId gonderilmezse backend her seferinde yeni bir
 * refresh zinciri acar; sabit tutuldugunda "bu cihazdan cikis yap" anlamli olur.
 * Uretildigi anda kaydedilir ki basarisiz bir giris denemesinden sonra bile
 * ayni cihaz kimligiyle devam edilsin.
 */
export function getOrCreateDeviceId(): string {
  const existing = cache.deviceId;
  if (existing) return existing;

  const deviceId = crypto.randomUUID();
  merge({ deviceId });
  return deviceId;
}

// --- Aktif kullanici ---

export function getCurrentUser(): UserBaseDto | null {
  return cache.user ?? null;
}

/** Uc parca da ayni yanittan (LoginResponse) gelir, birlikte yazilir. */
export function setCurrentUser(user: UserBaseDto, roles: string[], permissions: string[]): void {
  merge({ user, roles, permissions });
}

// --- Rol ve izin kontrolu (YALNIZCA UX) ---

/** React disindan cagrilabilir; reaktif degildir. Hook'lu surumu: usePermission. */
export function hasPermission(permission: string): boolean {
  return cache.permissions?.includes(permission) ?? false;
}

// --- React hook'lari ---

/** Oturumun tamamini reaktif okur; asagidaki hook'larin ortak temeli. */
export function useAuthSession(): StoredSession {
  return useSyncExternalStore(subscribe, getSnapshot);
}

export function useAccessToken(): string | null {
  return useAuthSession().accessToken ?? null;
}

export function useCurrentUser(): UserBaseDto | null {
  return useAuthSession().user ?? null;
}

export function useRoles(): string[] {
  return useAuthSession().roles ?? EMPTY;
}

/** Kullanim: const can = usePermission(); ... can('Cabinet.Read') */
export function usePermission(): (permission: string) => boolean {
  const permissions = useAuthSession().permissions;
  return useCallback((permission: string) => permissions?.includes(permission) ?? false, [permissions]);
}

/** useRoles bos dizide her render yeni referans uretmesin diye sabit. */
const EMPTY: string[] = [];
