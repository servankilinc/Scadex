import { createPortal } from 'react-dom';
import { useArtwork } from './artwork';
import { SymbolUse } from './symbol-use';
import { useSvgDevice } from './use-svg-device';

/**
 * Kabinin ortak sireni — kasadaki `#Siren-Box`'ın çerçevesine `Siren-Opened.svg` (çalıyor) ya da `Siren-Closed.svg`
 * (kapalı) çizilir. İki dosya farklı genişlikte; sembol sağa yaslanır, gövde yerinde kalır (`artwork.ts > SYMBOL_ALIGN`).
 */
interface SirenFigureProps {
  isOn: boolean;
  /** Kabinde siren kanalı yoksa `undefined` — soluk çizilir, komut sunulmaz. */
  onActivate?: () => void;
}

export function SirenFigure({ isOn, onActivate }: SirenFigureProps) {
  const { anchors, slots } = useArtwork();
  const label = onActivate ? `Siren: ${isOn ? 'çalıyor' : 'kapalı'} — komut için tıklayın` : 'Bu kabinde siren tanımlı değil';

  useSvgDevice(anchors.siren, { label, onActivate, disabled: !onActivate });

  if (!slots.siren) return null;
  return createPortal(<SymbolUse symbol={isOn ? 'vc-siren-on' : 'vc-siren-off'} frame={slots.siren.frame} />, slots.siren.layer);
}
