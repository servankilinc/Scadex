import type { XYPosition } from '@xyflow/react';

/**
 * Hizalama ve dağıtma matematiği.
 */

export type AlignMode = 'left' | 'centerX' | 'right' | 'top' | 'middle' | 'bottom';
export type DistributeAxis = 'horizontal' | 'vertical';

export interface AlignBox {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
}

/** Hizalamak için en az iki kutu gerekir: tek kutu neye göre hizalanacak? */
export const MIN_ALIGN = 2;

/**
 * Dağıtmak için en az üç gerekir. İki kutuda "aralarını eşitle" zaten
 * sağlanmıştır — tek bir aralık vardır ve o aralık kendisiyle eşittir.
 */
export const MIN_DISTRIBUTE = 3;

/**
 * Kayan nokta toleransı.
 *
 * Dağıtmada konumlar toplaya toplaya ilerliyor; son kutu matematiksel olarak
 * yerinde kalsa bile birikmiş hata yüzünden 1e-13 kadar kayabilir. Tolerans
 * olmasaydı o kutu "taşındı" sayılır, günlüğe girer ve hiç kıpırdamamış bir
 * cihaz için sunucuya güncelleme gönderilirdi.
 */
const EPSILON = 0.01;

/** Yalnızca GERÇEKTEN taşınanları döndürür — bkz. `EPSILON`. */
export function alignBoxes(boxes: AlignBox[], mode: AlignMode): Record<string, XYPosition> {
  if (boxes.length < MIN_ALIGN) return {};

  const target = alignTarget(boxes, mode);

  return movedOnly(boxes, box => {
    switch (mode) {
      case 'left':
        return { x: target, y: box.y };
      case 'centerX':
        return { x: target - box.width / 2, y: box.y };
      case 'right':
        return { x: target - box.width, y: box.y };
      case 'top':
        return { x: box.x, y: target };
      case 'middle':
        return { x: box.x, y: target - box.height / 2 };
      case 'bottom':
        return { x: box.x, y: target - box.height };
    }
  });
}

/**
 * Aralıkları eşitler; ilk ve son kutu YERİNDE kalır.
 *
 * Eşit ARALIK bırakılır, eşit merkez mesafesi değil. Farklı boyutlu kutularda
 * merkezleri eşit aralamak, gözle bakıldığında düzensiz görünür: geniş kutunun
 * iki yanındaki boşluk dar kutununkinden küçük çıkar.
 *
 * Kutular toplam genişliğinden daha dar bir alana sıkışmışsa `gap` negatif olur
 * ve kutular eşit miktarda üst üste biner. Bu bilinçli: kullanıcının seçtiği iki
 * uç noktayı korumak, komutu sessizce reddetmekten daha öngörülebilir.
 */
export function distributeBoxes(boxes: AlignBox[], axis: DistributeAxis): Record<string, XYPosition> {
  if (boxes.length < MIN_DISTRIBUTE) return {};

  const horizontal = axis === 'horizontal';
  // `id` ikincil ölçüt: aynı koordinatta başlayan iki kutuda sıralama aksi halde
  // girdi sırasına kalırdı ve aynı seçim iki farklı sonuç üretebilirdi.
  const sorted = [...boxes].sort((a, b) => start(a, horizontal) - start(b, horizontal) || a.id.localeCompare(b.id));

  const first = sorted[0]!;
  const last = sorted[sorted.length - 1]!;
  const span = start(last, horizontal) + size(last, horizontal) - start(first, horizontal);
  const occupied = sorted.reduce((sum, box) => sum + size(box, horizontal), 0);
  const gap = (span - occupied) / (sorted.length - 1);

  let cursor = start(first, horizontal);
  const positions = new Map<string, XYPosition>();
  for (const box of sorted) {
    positions.set(box.id, horizontal ? { x: cursor, y: box.y } : { x: box.x, y: cursor });
    cursor += size(box, horizontal) + gap;
  }

  return movedOnly(boxes, box => positions.get(box.id)!);
}

function alignTarget(boxes: AlignBox[], mode: AlignMode): number {
  switch (mode) {
    case 'left':
      return Math.min(...boxes.map(b => b.x));
    case 'right':
      return Math.max(...boxes.map(b => b.x + b.width));
    case 'top':
      return Math.min(...boxes.map(b => b.y));
    case 'bottom':
      return Math.max(...boxes.map(b => b.y + b.height));
    // Orta hizalamada referans, kutuların ORTALAMASI değil sınırlayıcı
    // dikdörtgenin merkezi: ortalama, kalabalığın olduğu tarafa kayar ve
    // "ortala" komutundan beklenen simetriyi vermez.
    case 'centerX':
      return (Math.min(...boxes.map(b => b.x)) + Math.max(...boxes.map(b => b.x + b.width))) / 2;
    case 'middle':
      return (Math.min(...boxes.map(b => b.y)) + Math.max(...boxes.map(b => b.y + b.height))) / 2;
  }
}

function movedOnly(boxes: AlignBox[], next: (box: AlignBox) => XYPosition): Record<string, XYPosition> {
  const moved: Record<string, XYPosition> = {};
  for (const box of boxes) {
    const position = next(box);
    if (Math.abs(position.x - box.x) > EPSILON || Math.abs(position.y - box.y) > EPSILON) moved[box.id] = position;
  }
  return moved;
}

function start(box: AlignBox, horizontal: boolean): number {
  return horizontal ? box.x : box.y;
}

function size(box: AlignBox, horizontal: boolean): number {
  return horizontal ? box.width : box.height;
}
