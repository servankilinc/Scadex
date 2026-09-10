/**
 * Renk yardımcıları.
 *
 * Sistemdeki TÜM renkler `#RRGGBB` dizesidir — şablon zemini, bağlantı rengi,
 * annotation renkleri, tual zemini ve ızgara. Şablon rengi eskiden 0xRRGGBB
 * tamsayısıydı; tek istisna olduğu ve "0 = siyah" ile "0 = boş"u ayırt
 * edemediği için (ROADMAP B4) dizeye çevrildi. Dönüşüm fonksiyonlarına artık
 * gerek yok.
 */

const HEX_COLOR = /^#[0-9a-f]{6}$/i;

/**
 * Sunucudan gelen rengi CSS'e basmadan önce doğrular.
 *
 * Geçersiz ve boş değerler siyaha düşürülür: bozuk tek bir şablon kaydı
 * yüzünden `undefined` bir CSS değeri üretip node'u görünmez kılmaktansa,
 * yanlış ama görünür bir renk basmak yeğdir.
 */
export function safeCssColor(value: string | null | undefined): string {
  return value != null && HEX_COLOR.test(value.trim()) ? value.trim() : '#000000';
}

/**
 * Verilen zemin rengine karşı okunur bir metin rengi seçer.
 * Şablon rengi serbest olduğu için (koyu zeminde koyu yazı okunmaz) node
 * etiketinin rengi sabit değil, hesaplanmış olmalı.
 *
 * W3C'nin göreli parlaklık eşiği yerine basit YIQ kullanılır: renkler zaten
 * kullanıcı seçimi, kontrast denetimi değil okunabilirlik hedefleniyor.
 */
export function readableTextColor(backgroundColor: string | null | undefined): string {
  const hex = safeCssColor(backgroundColor).slice(1);
  const value = Number.parseInt(hex, 16);
  const r = (value >> 16) & 0xff;
  const g = (value >> 8) & 0xff;
  const b = value & 0xff;
  const yiq = (r * 299 + g * 587 + b * 114) / 1000;
  return yiq >= 140 ? '#0f172a' : '#f8fafc';
}
