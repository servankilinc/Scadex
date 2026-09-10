import { Position } from '@xyflow/react';
import { HandleSide } from '@/models/enums';
import type { DiagramPinDto } from '@/models/diagram';

/**
 * Pin → React Flow `<Handle>` yerleşimi.
 *
 * İki ayrı bilgi gerekir ve biri diğerinin yerine geçmez:
 *   - `side`              → handle'ın HANGİ KENARDA olduğu (bağlantı yönü buradan türer)
 *   - `relativeX/relativeY` → o kenar üzerinde NEREDE durduğu
 *
 * Koordinat tek başına kenarı belirleyemez: (0.5, 0) hem üst kenarın ortası hem
 * sol kenarın başlangıcı olarak okunabilir. Bu yüzden `side` ayrı bir kolondur ve
 * değerini palet yazarı belirler.
 */

export const SIDE_TO_POSITION: Record<HandleSide, Position> = {
  [HandleSide.Left]: Position.Left,
  [HandleSide.Right]: Position.Right,
  [HandleSide.Top]: Position.Top,
  [HandleSide.Bottom]: Position.Bottom
};

/**
 * Yerleşim için gereken en küçük şekil.
 *
 * `DiagramPinDto` yerine bu kullanılıyor ki şablon yazarlığı ekranı da AYNI
 * hesabı çağırabilsin. Editör kendi yerleşim matematiğini yazsaydı, yazarken
 * gördüğünüz pin konumu ile canvas'ta çizilen konum ayrışabilirdi — ve bu
 * ayrışma ancak şablonu kullanmaya kalktığınızda fark edilirdi.
 */
export type PinPlacement = Pick<DiagramPinDto, 'side' | 'relativeX' | 'relativeY'>;

/**
 * Pinin kutu içindeki konumu — **tek yerleşim fonksiyonu**.
 *
 * Yüzde kullanılır çünkü `relativeX/Y` zaten şablonun genişlik/yüksekliğine göre
 * 0..1 normalize edilmiş bir kesirdir — piksele çevirmek için şablon boyutunu
 * bilmek gerekirdi ve şablon boyutu değiştiğinde pinler kayardı.
 *
 * **İKİ eksen birden döner.** Eskiden yalnızca uzunlamasına eksen dönüyordu ve
 * çaprazı React Flow'un handle sınıfı (`react-flow__handle-right → right: 0`)
 * sessizce sağlıyordu. Bu yazılı olmayan bağımlılık tam olarak beklenen hatayı
 * üretti: React Flow DIŞINDA render eden şablon yazarlığı önizlemesinde sağ
 * kenardaki pin sola, alttaki üste düştü.
 *
 * İki eksen artık ZORUNLU: arka plan görseli olan şablonlarda pin kenarda değil,
 * çizimin ortasındaki bir klemensin üzerinde durabiliyor — yani "kenar" diye bir
 * çapraz eksen yok.
 *
 * `transform` inline: React Flow'un handle sınıfı da transform tanımlıyor ve
 * çakışmada inline kazanır.
 */
export function pinPlacementStyle(pin: PinPlacement): React.CSSProperties {
  return {
    left: toPercent(pin.relativeX),
    top: toPercent(pin.relativeY),
    transform: 'translate(-50%, -50%)'
  };
}

/**
 * DB'de `CHECK (0..1)` var, ama bu kısıt migration'dan ÖNCEKI satırlar için
 * garanti değildi ve şablon içe aktarma gibi ileride eklenecek yollar da bunu
 * bozabilir. Aralık dışı bir değer handle'ı node'un dışına atar.
 */
function clampFraction(value: number): number {
  if (!Number.isFinite(value)) return 0.5;
  return Math.min(Math.max(value, 0), 1);
}

/**
 * 0..1 kesri yüzde dizesine çevirir.
 *
 * Yuvarlama ŞART: `0.58 * 100` kayan noktada `57.99999999999999` veriyor.
 * Ekranda fark etmez ama karşılaştırılabilir bir değer değildir ve testte
 * gürültü üretir. Dört basamak, alt piksel hassasiyetinin çok ötesi.
 */
function toPercent(fraction: number): string {
  return `${Number((clampFraction(fraction) * 100).toFixed(4))}%`;
}

/**
 * Kutunun içindeki bir noktayı EN YAKIN KENARA oturtur.
 *
 * Şablon yazarlığında tıklanan yerden pin üretmek için: kullanıcı kutunun
 * herhangi bir yerine tıklar, pin en yakın kenara yapışır. Serbest bir (x, y)
 * kabul etmek mümkün değil — pin bir bağlantı noktasıdır ve kenarda durmak
 * zorundadır (bkz. `side` neden ayrı bir kolon).
 *
 * Girdi 0..1 normalize kesirdir. Beraberlik durumunda sıra sol → sağ → üst →
 * alt: tam köşeye tıklamak her seferinde AYNI kenarı vermeli, yoksa aynı tıklama
 * iki farklı sonuç üretirdi.
 */
export function snapToEdge(x: number, y: number): PinPlacement {
  const px = clampFraction(x);
  const py = clampFraction(y);

  const distances: { side: HandleSide; distance: number }[] = [
    { side: HandleSide.Left, distance: px },
    { side: HandleSide.Right, distance: 1 - px },
    { side: HandleSide.Top, distance: py },
    { side: HandleSide.Bottom, distance: 1 - py }
  ];

  // `reduce` + kesin `<` karsilastirmasi: esitlikte ILK giren kazanir, yani
  // yukaridaki sira belirleyici olur.
  const nearest = distances.reduce((best, candidate) => (candidate.distance < best.distance ? candidate : best));

  switch (nearest.side) {
    case HandleSide.Left:
      return { side: HandleSide.Left, relativeX: 0, relativeY: py };
    case HandleSide.Right:
      return { side: HandleSide.Right, relativeX: 1, relativeY: py };
    case HandleSide.Top:
      return { side: HandleSide.Top, relativeX: px, relativeY: 0 };
    case HandleSide.Bottom:
      return { side: HandleSide.Bottom, relativeX: px, relativeY: 1 };
  }
}
