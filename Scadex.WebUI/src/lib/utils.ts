import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/**
 * C# backend datetime2 tipleri `Z` soneki olmadan ISO string döner (`2026-09-10T09:06:56.4089716`).
 * Çıplak `new Date(...)` bunu yerel saat sayıp kaydırır. Eksikse `Z` eklenerek UTC olarak parse edilir.
 */
export function toUtcDate(value: string | Date | null | undefined): Date | null {
  if (!value) return null
  if (value instanceof Date) return value
  return new Date(/(?:Z|[+-]\d{2}:?\d{2})$/i.test(value) ? value : `${value}Z`)
}

/** Sunucu UTC damgasını yerel tarih-saat metnine çevirir; değer yoksa `—`. */
export function formatUtcDateTime(value: string | Date | null | undefined, locale = "tr-TR"): string {
  const date = toUtcDate(value)
  return date ? date.toLocaleString(locale) : "—"
}
