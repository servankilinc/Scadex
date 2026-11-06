/**
 * CameraManagement modülünün sorgu anahtarları — modül kuralı gereği kendi kökü altında (`['camera', …]`).
 * Kameranın cihaz bilgisi uçları (liste/detay/güncelleme) çekirdekte olsa da onları yalnızca bu modülün ekranları okuduğu için
 * anahtarları da buradadır.
 */
export const cameraKeys = {
  all: ['camera'] as const,
  /** Otomasyon başına liste; `includePassive` ayrı bir anahtar — iki liste farklı veri. */
  byAutomation: (automationId: string, includePassive: boolean) =>
    [...cameraKeys.all, 'automation', automationId, includePassive] as const,
  detail: (id: string) => [...cameraKeys.all, 'detail', id] as const,
  /**
   * Bir kameranın çekim geçmişi.
   *
   * Bilet için anahtar YOK: bilet almak bir mutation'dır (sunucuda yol kurar,
   * önbelleğe yazar) ve önbelleklenmesi 60 saniyelik bir sırrı yeniden
   * kullanmaya çalışmak olurdu.
   */
  captures: (cameraId: string) => [...cameraKeys.all, 'captures', cameraId] as const
};

/** Kamera çekim ayarı (sunucuda tek satırlık tablo + kendi önbellek anahtarı). */
export const captureSettingKeys = {
  // cameraKeys.all altına KONMADI: kamera güncellemesi tüm 'camera' anahtarlarını düşürür, ayar gereksiz yere yeniden çekilirdi.
  all: ['camera-capture-setting'] as const
};
