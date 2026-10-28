/**
 * RemoteDesk PC'sinin Basic Auth sırrı. Kullanıcı adı cihazın Id'sidir.
 * Sırrı TARAYICI üretir ve cihaz formunun Kaydet'iyle gönderir (`PUT …/devices/{id}`); sunucu yalnızca biçimi zorlar ve SHA-256'sını saklar
 * (`Scadex.Core/Utils/DeviceCredential.cs`). Biçim iki tarafta aynı olmalı: 32 küçük harf onaltılık karakter (128 bit).
 * `crypto.getRandomValues` güvenli bağlam istemez (düz HTTP'de de çalışır).
 */
export const DEVICE_SECRET_LENGTH = 32;

export function generateDeviceSecret(): string {
  const bytes = new Uint8Array(DEVICE_SECRET_LENGTH / 2);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
}

/**
 * Kontrol kartının MAC adresi tek tip yazılır: `AA:BB:CC:DD:EE:FF` — sunucudaki `MacAddressFormat.TryNormalize`'ın ELLE tutulan aynası.
 * Ayraçlar (`- : .` ve boşluk) atılıp 12 harf/rakam kalıyorsa biçimlenir; kalmıyorsa `null` (ham değer gönderilir, sunucu 400 verir).
 */
export function normalizeMacAddress(value: string): string | null {
  const compact = value.replace(/[-:. ]/g, '').toUpperCase();
  if (!/^[0-9A-Z]{12}$/.test(compact)) return null;
  return compact.match(/../g)!.join(':');
}
