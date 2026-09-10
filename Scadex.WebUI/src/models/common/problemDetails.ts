/**
 * Ayna: Core/Utils/ResultPattern
 *
 * ErrorType -> HTTP eslemesi:
 *   Failure    = 100 -> 500
 *   NotFound   = 200 -> 404
 *   Validation = 300 -> 400  (errors sozlugu dolu gelir)
 *   Forbidden  = 400 -> 403
 */
export interface ProblemDetails {
  /* Failure(100), NotFound(200), Validation(300), Forbidden(400) */
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  /** Özelleştirilebilir detay kodu */
  code?: number;
  traceId?: string;
  /** Yalnizca dogrulama hatalarinda dolu gelir. Anahtarlar PascalCase'tir. */
  errors?: Record<string, string[]>;
}
