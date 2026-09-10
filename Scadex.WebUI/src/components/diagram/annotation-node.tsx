import type { NodeProps } from '@xyflow/react';
import { AnnotationShape } from '@/models/enums';
import type { DiagramAnnotationItemDto } from '@/models/diagram';
import type { AnnotationNode } from '@/lib/diagram/to-rf-nodes';
import { cn } from '@/lib/utils';

/**
 * Cihaz olmayan diyagram elemanı: serbest metin, çerçeveli kutu, not veya ok.
 * Referans diyagramdaki "ŞEBEKE", "220V ÇIKIŞ" gibi etiketler bunlardır.
 *
 * Handle taşımaz — bir nota kablo bağlanamaz.
 */
export function AnnotationNode({ data, selected }: NodeProps<AnnotationNode>) {
  const { annotation } = data;

  // Ok AYRI bir dal: kutu değil, çizim. Ortak sarmalayıcıya sıkıştırmak
  // (arka plan rengi, kenarlık, ortalanmış metin) okun hiçbirini kullanmadığı
  // bir yığın stil taşımak olurdu.
  if (annotation.shape === AnnotationShape.Arrow) {
    return <ArrowAnnotation annotation={annotation} selected={selected} />;
  }

  // Text: cercevesiz duz metin (draw.io "text" stili).
  // Rectangle: cerceveli kutu. Note: kose kivrimli aciklama balonu.
  const shapeClass =
    annotation.shape === AnnotationShape.Text
      ? 'border-transparent bg-transparent'
      : annotation.shape === AnnotationShape.Note
        ? 'rounded-lg rounded-tr-none border'
        : 'rounded-sm border';

  return (
    <div
      className={cn('flex h-full w-full items-center justify-center px-2 text-center', shapeClass, selected && 'ring-primary ring-2')}
      style={{
        backgroundColor: annotation.shape === AnnotationShape.Text ? 'transparent' : annotation.backgroundColor,
        borderColor: annotation.borderColor,
        color: annotation.fontColor,
        fontSize: annotation.fontSize,
        fontWeight: annotation.isBold ? 700 : 400,
        transform: annotation.rotation ? `rotate(${annotation.rotation}deg)` : undefined
      }}>
      <span className='whitespace-pre-wrap'>{annotation.text}</span>
    </div>
  );
}

/** Okun kalınlığı bu sınırlar arasında kalır — 1 px altı görünmez, 12 px üstü kabloya benzemez. */
const MIN_ARROW_STROKE = 1;
const MAX_ARROW_STROKE = 12;

/**
 * Yön oku.
 *
 * Ayrı bir "uç noktası" alanı YOK: kutunun genişliği okun uzunluğu, yüksekliği
 * kalınlığı, `rotation` ise yönü. Uç noktayı ayrı saklamak, DTO'ya iki alan daha
 * eklemek ve taşıma/döndürme mantığını ikiye bölmek olurdu — oysa her node
 * zaten bir kutu ve bir açı taşıyor.
 */
function ArrowAnnotation({ annotation, selected }: { annotation: DiagramAnnotationItemDto; selected?: boolean }) {
  const width = Math.max(annotation.width, 1);
  const height = Math.max(annotation.height, 1);
  const stroke = Math.min(Math.max(height / 6, MIN_ARROW_STROKE), MAX_ARROW_STROKE);

  // Marker id BELGE genelinde benzersiz olmak zorunda: iki ok aynı id'yi
  // kullansaydı ikisi de ilk tanımlanan rengi alırdı.
  const markerId = `arrow-head-${annotation.id}`;

  return (
    <div
      className={cn('relative h-full w-full', selected && 'ring-primary rounded-sm ring-2')}
      style={{ transform: annotation.rotation ? `rotate(${annotation.rotation}deg)` : undefined }}>
      <svg width='100%' height='100%' viewBox={`0 0 ${width} ${height}`} className='overflow-visible'>
        <defs>
          {/* markerUnits varsayilani strokeWidth: uc, cizginin kalinligiyla
              birlikte olceklenir ve ince bir okta devasa bir uc olusmaz. */}
          <marker id={markerId} markerWidth={5} markerHeight={5} refX={4} refY={2.5} orient='auto'>
            <path d='M0,0 L5,2.5 L0,5 z' fill={annotation.borderColor} />
          </marker>
        </defs>
        <line
          x1={0}
          y1={height / 2}
          // Cizgi uc noktanin ONUNDE bitiyor: tam kenara kadar cizilseydi ok ucu
          // kutunun disina tasardi.
          x2={Math.max(width - stroke * 4, 1)}
          y2={height / 2}
          stroke={annotation.borderColor}
          strokeWidth={stroke}
          markerEnd={`url(#${markerId})`}
        />
      </svg>

      {/* Etiket okun USTUNDE: cizginin uzerine binseydi ikisi de okunmaz olurdu.
          Ok dondugunde etiket de onunla birlikte doner — kullanicinin okun
          yonunu takip eden bir yazi beklemesi dogal. */}
      {annotation.text && (
        <span
          className='pointer-events-none absolute bottom-full left-1/2 -translate-x-1/2 whitespace-pre'
          style={{ color: annotation.fontColor, fontSize: annotation.fontSize, fontWeight: annotation.isBold ? 700 : 400 }}>
          {annotation.text}
        </span>
      )}
    </div>
  );
}
