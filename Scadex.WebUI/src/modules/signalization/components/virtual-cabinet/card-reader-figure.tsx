import { createPortal } from 'react-dom';
import type { CardFlash } from '../../hooks/use-virtual-cabinet';
import { useArtwork } from './artwork';
import { SymbolUse } from './symbol-use';
import { useSvgDevice } from './use-svg-device';

/**
 * Kabinin kart okuyucusu — kasadaki `#Card-Reader-Box`'ın çerçevesine `Card-Reader.svg` çizilir. Tıklanınca son okumalar
 * açılır; her okumada okuyucunun arkasında `CARD_FLASH_MS` boyunca parlama yanar (kabul yeşil, red kırmızı). Parlama kodun
 * parçasıdır: Figma'da okuyucunun yalnızca boşta hâli çizilir.
 */
interface CardReaderFigureProps {
  flash: CardFlash | null;
  onActivate: () => void;
}

/** Parlamanın okuyucu kenarından taşma payı (SVG birimi). */
const GLOW_SPREAD = 18;

export function CardReaderFigure({ flash, onActivate }: CardReaderFigureProps) {
  const { anchors, slots } = useArtwork();
  const label = flash
    ? `Kart okuyucu: kart ${flash.isAccepted ? 'kabul edildi' : 'reddedildi'} — son okumalar için tıklayın`
    : 'Kart okuyucu — son okumalar için tıklayın';

  useSvgDevice(anchors.cardReader, { label, onActivate });

  const slot = slots.cardReader;
  if (!slot) return null;

  const { x, y, width, height } = slot.frame;
  const color = flash?.isAccepted ? '#22C55E' : '#EF4444';

  return createPortal(
    <>
      {/* Parlama okuyucunun ARKASINDA çizilir ve tıklamayı yutmaz. `key` her okumada değişir: animasyon baştan başlar. */}
      {flash && (
        <g key={flash.key} pointerEvents='none'>
          <defs>
            <radialGradient id='vc-card-glow'>
              <stop offset='0' stopColor={color} stopOpacity='0.9' />
              <stop offset='0.55' stopColor={color} stopOpacity='0.45' />
              <stop offset='1' stopColor={color} stopOpacity='0' />
            </radialGradient>
          </defs>
          <ellipse
            cx={x + width / 2}
            cy={y + height / 2}
            rx={width / 2 + GLOW_SPREAD}
            ry={height / 2 + GLOW_SPREAD}
            fill='url(#vc-card-glow)'
            className='animate-pulse [animation-duration:1s]'
          />
        </g>
      )}
      <SymbolUse symbol='vc-card-reader' frame={slot.frame} />
    </>,
    slot.layer
  );
}
