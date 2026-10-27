/**
 * Saha cihazının (kontrol kartı, RemoteDesk PC'si) Basic Auth sırrı — 2026-10-25, MAC kimliğinin yerine. Kullanıcı adı cihazın Id'sidir.
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
