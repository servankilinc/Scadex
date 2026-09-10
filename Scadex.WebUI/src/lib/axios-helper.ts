import axios, { type AxiosInstance, type AxiosRequestConfig } from 'axios';
import { toast } from 'sonner';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { clearSession, getAccessToken } from './auth-session';
import type { ProblemDetails } from '@/models/common/problemDetails';

export const API_BASE_URL: string = import.meta.env.VITE_API_URL ?? 'https://localhost:7042';

/**
 * Account uclarinin yolu SABIT ve BUYUK/KUCUK HARFE DUYARLIDIR.
 * Backend rotasi "api/[controller]" oldugu icin dogru yazim "/api/Account"tir;
 * "/api/account/..." yazilirsa cookie tabanli uclar calismaz.
 */
export const ACCOUNT_ROUTE = '/api/Account';

class AxiosService {
  private _axios: AxiosInstance;

  constructor() {
    this._axios = axios.create({
      baseURL: API_BASE_URL,
      timeout: 30000,
      // Backend refresh token'i HttpOnly cookie ile yaziyor. Refresh su an
      // kullanilmasa da cookie'nin tasinmasi icin acik birakilir; kapatilirsa
      // refresh eklendiginde sessizce calismaz.
      withCredentials: true
    });

    this._axios.interceptors.request.use(
      config => {
        const token = getAccessToken();
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }

        // FormData'da Content-Type ACIKCA silinir.
        if (config.data instanceof FormData) {
          delete config.headers['Content-Type'];
        }

        return config;
      },
      error => Promise.reject(error)
    );

    this._axios.interceptors.response.use(
      response => response,
      error => {
        // Otomatik token yenileme HENUZ YOK - suresi dolan token'da kullanici
        // login'e dusuruluyor. Backend tarafi hazir (POST /api/Account/RefreshAuth
        // + HttpOnly cookie); eklenirken tek ucuslu (single-flight) bir kuyruk
        // sart, cunku backend refresh token'i her kullanimda donduruyor ve
        // es zamanli iki refresh oturumu tumden dusuruyor.
        if (error.response?.status === 401) {
          console.warn('[auth] 401 alindi, oturum sonlandiriliyor. Otomatik refresh henuz eklenmedi.', {
            url: error.config?.url,
            method: error.config?.method
          });
          // clearSession() store'u da bildirir; RequireAuth yeniden render olup kendiliginden /login'e yonlendirir.
          clearSession();
        }

        // TUM reject yollari ApiError uzerinden gecer; cagiran taraflarin
        // AxiosError tanimasina veya normalize etmesine gerek kalmaz.
        return Promise.reject(toApiError(error));
      }
    );
  }

  async get<TResponse = undefined>(url: string, config?: AxiosRequestConfig | undefined): Promise<TResponse> {
    const response = await this._axios.get<TResponse>(url, config);
    return response.data;
  }

  async post<TResponse = undefined>(url: string, data?: unknown, config?: AxiosRequestConfig | undefined): Promise<TResponse> {
    const response = await this._axios.post<TResponse>(url, data, config);
    return response.data;
  }

  async put<TResponse = undefined>(url: string, data?: unknown, config?: AxiosRequestConfig | undefined): Promise<TResponse> {
    const response = await this._axios.put<TResponse>(url, data, config);
    return response.data;
  }

  async delete<TResponse = undefined>(url: string, data?: unknown, config?: AxiosRequestConfig | undefined): Promise<TResponse> {
    const response = await this._axios.delete<TResponse>(url, { data, ...config });
    return response.data;
  }
}

const DEFAULT_MESSAGE = 'Beklenmeyen bir hata olustu. Lutfen tekrar deneyin.';
const NETWORK_MESSAGE = 'Sunucuya ulasilamiyor. Baglantinizi kontrol edin.';

export class ApiError extends Error {
  /** HTTP durum kodu; aga hic cikilamadiysa 0. */
  readonly status: number;
  /** Alan bazli dogrulama hatalari (yalnizca 400'de dolu). Anahtarlar camelCase'e cevrilmistir. */
  readonly fieldErrors: Record<string, string[]>;
  readonly problem?: ProblemDetails;

  constructor(message: string, status: number, fieldErrors: Record<string, string[]>, problem?: ProblemDetails, cause?: unknown) {
    super(message, { cause });
    this.name = 'ApiError';
    this.status = status;
    this.fieldErrors = fieldErrors;
    this.problem = problem;
  }

  get hasFieldErrors(): boolean {
    return Object.keys(this.fieldErrors).length > 0;
  }
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;

  if (axios.isAxiosError(error)) {
    // Sunucuya hic ulasilamadi (ag kopuk, CORS, sertifika).
    if (!error.response) {
      return new ApiError(NETWORK_MESSAGE, 0, {}, undefined, error);
    }

    const problem = error.response.data as ProblemDetails | undefined;
    // Backend PascalCase alan adi dondurur ("UserName"); form alanlari camelCase.
    const fieldErrors = toCamelCaseKeys(problem?.errors);

    // Oncelik sirasi: ilk dogrulama hatasi > detail > title. Boylece kullanici
    // "The operation could not be completed" yerine gercek sebebi gorur.
    const firstFieldError = Object.values(fieldErrors)[0]?.[0];
    const message = firstFieldError || problem?.detail || problem?.title || error.message || DEFAULT_MESSAGE;

    return new ApiError(message, error.response.status, fieldErrors, problem, error);
  }

  return new ApiError(error instanceof Error ? error.message : DEFAULT_MESSAGE, 0, {}, undefined, error);
}

function toCamelCaseKeys(errors: Record<string, string[]> | undefined): Record<string, string[]> {
  if (!errors) return {};
  const result: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    if (!messages?.length) continue;
    result[field.charAt(0).toLowerCase() + field.slice(1)] = messages;
  }
  return result;
}

// --- Transport ---

// --- Form hata politikasi ---

/**
 * Form mutation'larinin ortak hata politikasi:
 *   - Alan bazli dogrulama hatasi varsa ilgili input'un altinda gosterilir
 *   - Yoksa tek satirlik bir toast basilir
 *
 * Bu POLITIKADIR, transport degil - interceptor her hatayi ApiError'a cevirmekle
 * yetinir, ne gosterilecegine burasi karar verir. Liste sorgulari veya arka plan
 * mutation'lari farkli davranmak isteyebilir (or. sessiz kalmak) ve onlar bu
 * yardimciyi kullanmaz.
 *
 * Kullanim:  onError: error => handleFormApiError(error, form.setError)
 */
export function handleFormApiError<TFieldValues extends FieldValues>(error: unknown, setError: UseFormSetError<TFieldValues>): void {
  const apiError = toApiError(error);

  if (!apiError.hasFieldErrors) {
    toast.error(apiError.message);
    return;
  }

  for (const [field, messages] of Object.entries(apiError.fieldErrors)) {
    setError(field as Path<TFieldValues>, { message: messages[0] });
  }
}

export default new AxiosService();
