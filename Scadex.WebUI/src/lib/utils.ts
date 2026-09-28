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

/**
 * Metin aramasında karşılaştırma anahtarı: `tr` ile küçük harf, aksanlar (ç ş ğ ö ü â î û) ve `ı` düşer. "kizilay"
 * hem "Kızılay"ı hem "KIZILAY"ı bulur, "cankaya" "Çankaya"yı. `Intl.Collator` bunu yapmaz: `tr`'de ç/ş/ı ayrı harftir,
 * `en`'de ise `I`↔`ı` eşleşmez. Hem aranan metne hem sorguya uygulanıp `includes` ile karşılaştırılır.
 */
export function toSearchKey(text: string): string {
  return text.toLocaleLowerCase("tr").normalize("NFD").replace(/\p{M}/gu, "").replace(/ı/g, "i")
}
