import {
  AlignCenterHorizontal,
  AlignCenterVertical,
  AlignEndHorizontal,
  AlignEndVertical,
  AlignHorizontalDistributeCenter,
  AlignStartHorizontal,
  AlignStartVertical,
  AlignVerticalDistributeCenter
} from 'lucide-react';
import type { XYPosition } from '@xyflow/react';
import { Button } from '@/components/ui/button';
import { MIN_DISTRIBUTE, alignBoxes, distributeBoxes, type AlignBox, type AlignMode, type DistributeAxis } from '@/lib/diagram/align';
import type { DiagramNode } from '@/lib/diagram/to-rf-nodes';

/**
 * Hizalama ve dağıtma düğmeleri.
 *
 * Bu işin klavye karşılığı yok ve olmamalı: "seçilenleri sola hizala" bir tuş
 * kombinasyonuyla ifade edilse bile kimse onu hatırlamaz. Görünür düğme burada
 * kısayolun ikamesi değil, doğru arayüz.
 *
 * Matematik `lib/diagram/align.ts`'te ve saf — burada yalnızca node'lar kutuya
 * çevrilip sonuç editöre iletiliyor.
 */

const ALIGNMENTS: { mode: AlignMode; label: string; Icon: typeof AlignStartVertical }[] = [
  { mode: 'left', label: 'Sola hizala', Icon: AlignStartVertical },
  { mode: 'centerX', label: 'Yatayda ortala', Icon: AlignCenterVertical },
  { mode: 'right', label: 'Sağa hizala', Icon: AlignEndVertical },
  { mode: 'top', label: 'Üste hizala', Icon: AlignStartHorizontal },
  { mode: 'middle', label: 'Dikeyde ortala', Icon: AlignCenterHorizontal },
  { mode: 'bottom', label: 'Alta hizala', Icon: AlignEndHorizontal }
];

const DISTRIBUTIONS: { axis: DistributeAxis; label: string; Icon: typeof AlignStartVertical }[] = [
  { axis: 'horizontal', label: 'Yatayda eşit arala', Icon: AlignHorizontalDistributeCenter },
  { axis: 'vertical', label: 'Dikeyde eşit arala', Icon: AlignVerticalDistributeCenter }
];

export function AlignToolbar({ nodes, onMove }: { nodes: DiagramNode[]; onMove: (positions: Record<string, XYPosition>) => void }) {
  const boxes = nodes.map(toBox);
  // Üçten az seçimde dağıtma düğmeleri gizlenmiyor, PASİFLEŞİYOR: kaybolan bir
  // düğme kullanıcıya "bu özellik yok" der, pasif olan "bir seçim daha gerek".
  const canDistribute = boxes.length >= MIN_DISTRIBUTE;

  return (
    <div className='flex flex-col gap-2'>
      <p className='text-muted-foreground text-xs font-medium'>Hizala</p>
      <div className='grid grid-cols-6 gap-1'>
        {ALIGNMENTS.map(({ mode, label, Icon }) => (
          <Button key={mode} size='xs' variant='outline' aria-label={label} title={label} onClick={() => onMove(alignBoxes(boxes, mode))}>
            <Icon />
          </Button>
        ))}
      </div>

      <p className='text-muted-foreground text-xs font-medium'>Arala</p>
      <div className='grid grid-cols-6 gap-1'>
        {DISTRIBUTIONS.map(({ axis, label, Icon }) => (
          <Button
            key={axis}
            size='xs'
            variant='outline'
            aria-label={label}
            title={canDistribute ? label : `${label} — en az ${MIN_DISTRIBUTE} öğe gerekir`}
            disabled={!canDistribute}
            onClick={() => onMove(distributeBoxes(boxes, axis))}>
            <Icon />
          </Button>
        ))}
      </div>
    </div>
  );
}

function toBox(node: DiagramNode): AlignBox {
  return {
    id: node.id,
    x: node.position.x,
    y: node.position.y,
    // Ölçü ŞABLONDAN geliyor (`toRfNodes` `width`/`height` yazıyor). `measured`
    // yalnızca RF'in DOM'dan okuduğu değerdir ve node ekran dışındayken ya da
    // gizliyken hiç dolmaz — ona güvenmek görünmeyen kutuları 0 boyutlu sayardı.
    width: node.width ?? node.measured?.width ?? 0,
    height: node.height ?? node.measured?.height ?? 0
  };
}
