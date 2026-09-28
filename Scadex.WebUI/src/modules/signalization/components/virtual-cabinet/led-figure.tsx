import { createPortal } from 'react-dom';
import { useArtwork } from './artwork';
import { SymbolUse } from './symbol-use';
import { useSvgDevice } from './use-svg-device';

/**
 * Dış kapının aydınlatma LED'i — kasadaki `#Led-Light-Box`'ın çerçevesine `Led-Opened.svg` (yanıyor) ya da `Led-Closed.svg`
 * (sönük) çizilir.
 */
interface LedFigureProps {
  isOn: boolean;
  /** Kapıda aydınlatma kanalı yoksa `undefined` — cihaz soluk çizilir, komut sunulmaz. */
  onActivate?: () => void;
}

export function LedFigure({ isOn, onActivate }: LedFigureProps) {
  const { anchors, slots } = useArtwork();
  const label = onActivate
    ? `Aydınlatma: ${isOn ? 'yanıyor' : 'sönük'} — komut için tıklayın`
    : 'Bu dış kapıya aydınlatma tanımlı değil';

  useSvgDevice(anchors.led, { label, onActivate, disabled: !onActivate });

  if (!slots.led) return null;
  return createPortal(<SymbolUse symbol={isOn ? 'vc-led-on' : 'vc-led-off'} frame={slots.led.frame} />, slots.led.layer);
}
