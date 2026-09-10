import type { XYPosition } from '@xyflow/react';
import { AnnotationShape, AnnotationShapeLabels } from '@/models/enums';
import type { DiagramAnnotationItemDto } from '@/models/diagram';

/** Yeni notun varsayılan ve oluşturulma süreçleri. */

/** Sunucu sınırları — doğrulayıcıyla birebir. */
export const ANNOTATION_NAME_MAX = 128;
export const ANNOTATION_TEXT_MAX = 4000;
export const ANNOTATION_COLOR_MAX = 32;
export const ANNOTATION_FONT_SIZE_MIN = 1;
export const ANNOTATION_FONT_SIZE_MAX = 200;

interface ShapeDefaults {
  width: number;
  height: number;
  text: string;
  backgroundColor: string;
  borderColor: string;
  fontColor: string;
  fontSize: number;
  isBold: boolean;
}

/** Şekle göre varsayılanlar. */
const DEFAULTS: Record<AnnotationShape, ShapeDefaults> = {
  [AnnotationShape.Text]: {
    width: 120,
    height: 32,
    text: 'Metin',
    backgroundColor: '#FFFFFF',
    borderColor: '#94A3B8',
    fontColor: '#0F172A',
    fontSize: 12,
    isBold: false
  },
  [AnnotationShape.Rectangle]: {
    width: 160,
    height: 48,
    text: 'Kutu',
    backgroundColor: '#FFFFFF',
    borderColor: '#94A3B8',
    fontColor: '#0F172A',
    fontSize: 12,
    isBold: true
  },
  [AnnotationShape.Note]: {
    width: 160,
    height: 72,
    text: 'Not',
    backgroundColor: '#FEF9C3',
    borderColor: '#EAB308',
    fontColor: '#0F172A',
    fontSize: 12,
    isBold: false
  },
  [AnnotationShape.Arrow]: {
    width: 120,
    height: 24,
    // Ok BOŞ metinle doğuyor: çizginin üstünde istenmeyen bir "Ok" yazısı
    // belirmesi, kullanıcının ilk işini silmek olurdu.
    text: '',
    backgroundColor: '#FFFFFF',
    borderColor: '#0F172A',
    fontColor: '#0F172A',
    fontSize: 11,
    isBold: false
  }
};

/**
 * Yeni not taslağı üretir. Konum, kutunun SOL ÜST köşesidir.
 *
 * `takenNames` ad çakışmasını önlemek için: `DiagramAnnotation.Name` üzerinde
 * benzersizlik kısıtı yok, ama üç tane "Kutu" kullanıcı için okunmaz olurdu.
 */
export function newAnnotationDraft(id: string, shape: AnnotationShape, position: XYPosition, takenNames: readonly string[]): DiagramAnnotationItemDto {
  const defaults = DEFAULTS[shape];

  return {
    id,
    name: nextName(AnnotationShapeLabels[shape], takenNames),
    coordinateX: position.x,
    coordinateY: position.y,
    width: defaults.width,
    height: defaults.height,
    rotation: 0,
    // zIndex 0: notlar cihazların ALTINDA kalmalı — bir kutu notu cihazın üstüne
    // binerse cihaz seçilemez hale gelir
    zIndex: 0,
    isLocked: false,
    isVisible: true,
    text: defaults.text,
    shape,
    backgroundColor: defaults.backgroundColor,
    fontColor: defaults.fontColor,
    fontSize: defaults.fontSize,
    isBold: defaults.isBold,
    borderColor: defaults.borderColor
  };
}

/** Üst üste binmeyi açmak için her adımda kaydırılan mesafe. */
const CASCADE_STEP = 24;

/**
 * Aynı noktada bir şey varsa yeni öğeyi kaydırır.
 *
 * Not araçları öğeyi görünen alanın ORTASINA koyuyor; bu kural olmasaydı üst
 * üste basmak tam olarak çakışan kutular üretir ve kullanıcı tek bir kutu
 * gördüğü için butonun çalışmadığını sanardı.
 */
export function cascadePosition(position: XYPosition, taken: readonly XYPosition[], limit = 12): XYPosition {
  let candidate = position;

  for (let step = 0; step < limit; step++) {
    if (!taken.some(point => point.x === candidate.x && point.y === candidate.y)) return candidate;
    candidate = { x: candidate.x + CASCADE_STEP, y: candidate.y + CASCADE_STEP };
  }

  return candidate;
}

function nextName(base: string, taken: readonly string[]): string {
  const used = new Set(taken);
  if (!used.has(base)) return base;

  for (let index = 2; ; index++) {
    const candidate = `${base} ${index}`;
    if (!used.has(candidate)) return candidate;
  }
}
