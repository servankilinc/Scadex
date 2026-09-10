import { API_BASE_URL } from '@/lib/axios-helper';

/**
 * Şablon arka plan görselleri.
 *
 * Sunucu **göreli** URL saklıyor (`/uploads/templates/x.png`) — mutlak adres
 * yazmak, uygulama taşındığı gün (localhost → IIS → ters vekil) bütün
 * şablonların görselini kırardı.
 *
 * Ama frontend geliştirmede :5173'te, API :7042'de. Göreli URL'yi olduğu gibi
 * `<img src>`'ye vermek Vite'a gider ve 404 döner. Çevirme TEK yerde yapılıyor
 * ki bir bileşende unutulup "bazı yerlerde resim görünmüyor" hatası çıkmasın.
 */

/** Şablon kutusunun uzun kenarı için varsayılan hedef — akış birimi, piksel değil. */
export const TEMPLATE_IMAGE_MAX_SIDE = 320;

export interface ImageSize {
  width: number;
  height: number;
}

/**
 * Göreli URL'yi tarayıcının çözebileceği hale getirir.
 *
 * Zaten mutlak olan (http/https) ya da gömülü (`data:`) adresler olduğu gibi
 * geçer: ileride bir şablon dış bir CDN'den beslenirse bu fonksiyon onu bozmasın.
 */
export function templateImageSrc(url: string | null | undefined): string | null {
  const trimmed = url?.trim();
  if (!trimmed) return null;
  if (/^(https?:|data:|blob:)/i.test(trimmed)) return trimmed;
  return `${API_BASE_URL}${trimmed.startsWith('/') ? '' : '/'}${trimmed}`;
}

/**
 * Görselin doğal ölçüsünden şablon kutusunu türetir.
 *
 * **En-boy oranı KORUNUR.** Sebep doğrudan pinlerle ilgili: pinler `0..1` kesri
 * olarak saklanıyor, yani kutunun oranı değişirse görsel esner ama pinler
 * esnemez — "OUT 1 COM" klemensine koyduğunuz pin klemensten kayar. Bu kayma
 * ancak şablon kullanılırken fark edilir.
 *
 * Ölçekleme yalnızca KÜÇÜLTÜR: 80×60'lık bir çizimi 320'ye şişirmek, olmayan
 * çözünürlüğü varmış gibi göstermek olurdu.
 */
export function fitToTemplate(natural: ImageSize, maxSide: number = TEMPLATE_IMAGE_MAX_SIDE): ImageSize {
  const width = Math.max(natural.width, 1);
  const height = Math.max(natural.height, 1);

  const scale = Math.min(maxSide / Math.max(width, height), 1);
  return {
    // En az 1: sunucu doğrulayıcısı sıfır boyutu reddediyor ve sıfır boyutlu bir
    // node canvas'ta seçilemez hale gelirdi.
    width: Math.max(Math.round(width * scale), 1),
    height: Math.max(Math.round(height * scale), 1)
  };
}

/**
 * Bir kenar değiştiğinde diğerini oranı koruyacak şekilde hesaplar.
 *
 * Kilidin görünür karşılığı: kullanıcı genişliği yazar, yükseklik kendiliğinden
 * gelir. Serbest bırakılsaydı her pin klemensinden kayardı (bkz. `fitToTemplate`).
 */
export function lockedSide(changed: number, ratio: number): number {
  if (!Number.isFinite(changed) || !Number.isFinite(ratio) || ratio <= 0) return 1;
  return Math.max(Math.round(changed / ratio), 1);
}

/** Genişlik / yükseklik. Sıfıra bölmeye karşı korumalı. */
export function aspectRatio(size: ImageSize): number {
  const height = Math.max(size.height, 1);
  return Math.max(size.width, 1) / height;
}

/**
 * Görselin doğal ölçüsünü okur.
 *
 * SVG'de `naturalWidth` **0 gelebilir**: dosyada `width`/`height` yoksa yalnızca
 * `viewBox` vardır ve tarayıcı ölçüyü belirleyemez. O durumda makul bir kare
 * dönüyoruz — kullanıcı zaten formda değiştirebiliyor; alternatif, boyutu 0 olan
 * ve canvas'ta görünmeyen bir şablon üretmek olurdu.
 */
export function readImageSize(src: string): Promise<ImageSize> {
  return new Promise(resolve => {
    const image = new Image();
    image.onload = () => {
      const width = image.naturalWidth || 0;
      const height = image.naturalHeight || 0;
      resolve(width > 0 && height > 0 ? { width, height } : { width: TEMPLATE_IMAGE_MAX_SIDE, height: TEMPLATE_IMAGE_MAX_SIDE });
    };
    // Yüklenemeyen görselde de bir ölçü dönüyoruz: yükleme başarılı olduğu halde
    // önizlemenin çizilememesi (ör. ağ gecikmesi) formu kilitlememeli.
    image.onerror = () => resolve({ width: TEMPLATE_IMAGE_MAX_SIDE, height: TEMPLATE_IMAGE_MAX_SIDE });
    image.src = src;
  });
}
