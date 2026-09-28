import { useArtwork } from './artwork';
import { useSvgDevice } from './use-svg-device';

/**
 * Dış kapıyı gören kamera — çizimdeki `#Camera-Box`. Durumu yoktur (canlı görüntü ekranda değil): tıklanınca kabin ekranından
 * ayrılmadan izleme diyaloğu açılır. Kapıya kamera tanımlı değilse soluk çizilir ve tıklanamaz.
 */
interface CameraFigureProps {
  cameraName: string | null;
  onOpen?: () => void;
}

export function CameraFigure({ cameraName, onOpen }: CameraFigureProps) {
  const { anchors } = useArtwork();
  const label = cameraName ? `Kamera: ${cameraName} — canlı izlemeyi aç` : 'Bu dış kapıya kamera tanımlı değil';

  useSvgDevice(anchors.camera, { label, onActivate: onOpen, disabled: !onOpen });
  return null;
}
