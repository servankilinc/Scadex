import { SymbolUse } from './symbol-use';
import { FigureButton } from './figure-button';

/**
 * Tek bir iç kapı. Gövde `Indoor-Opened.svg` / `Indoor-Closed.svg` sembolüdür ve **switch'ten** gelen bilgiyle seçilir;
 * kilit durumu gövdeyi değiştirmez, kilidin üstüne oturan rozetle gösterilir.
 *
 * Figür (0, 0) köşesinde `size` boyutunda çizilir; yerleşim `IndoorGrid`'dedir.
 */
interface IndoorFigureProps {
  name: string;
  /** `null` = anahtar okunamıyor; kapalı gövde çizilir ama kesikli çerçeveyle işaretlenir. */
  isOpen: boolean | null;
  /** `null` = kilit kanalı bilinmiyor (hiç başarılı komut görmemiş); gri rozet. */
  isUnlocked: boolean | null;
  /** Gövdenin boyutu — iç kapı dosyasının `viewBox`'ı. */
  size: { width: number; height: number };
  onActivate?: () => void;
}

/**
 * Kilit kolunun gövdedeki yeri — rozetin merkezi. `Indoor-*.svg` çizimine (140x196) BAĞLIDIR: iki gövdede kolun yeri
 * farklıdır (kapı açılınca kanat döner), kapalıda solda, açıkta sağda. Dosya başka boyutta çizilirse oranla ölçeklenir;
 * kol başka bir yere taşınırsa bu değerler de güncellenmeli.
 */
const LOCK_ANCHOR = {
  closed: { x: 21, y: 98 },
  open: { x: 129.5, y: 105 }
} as const;
const LOCK_ANCHOR_ART_SIZE = { width: 140, height: 196 } as const;

export function IndoorFigure({ name, isOpen, isUnlocked, size, onActivate }: IndoorFigureProps) {
  const anchor = isOpen === true ? LOCK_ANCHOR.open : LOCK_ANCHOR.closed;

  const doorState = isOpen === null ? 'anahtar okunamıyor' : isOpen ? 'açık' : 'kapalı';
  // Kapı açık ama kilit kanalı "kilitli": kilide komut gitmeden açılmış (zorlanmış açılış).
  const isForced = isOpen === true && isUnlocked === false;
  const lockState = isUnlocked === null ? 'bilinmiyor' : isUnlocked ? 'açık' : 'kilitli';
  const label = `${name}: kapı ${doorState}, kilit ${lockState}${isForced ? ' (zorlanmış açılış)' : ''}${onActivate ? ' — komut için tıklayın' : ''}`;

  return (
    <FigureButton label={label} onActivate={onActivate} disabled={!onActivate}>
      <SymbolUse symbol={isOpen === true ? 'vc-indoor-open' : 'vc-indoor-closed'} frame={{ x: 0, y: 0, ...size }} />

      {isOpen === null && (
        <rect x='1' y='1' width={size.width - 2} height={size.height - 2} rx='4' fill='none' stroke='#94A3B8' strokeWidth='2' strokeDasharray='6 5' />
      )}

      <LockBadge
        x={(anchor.x * size.width) / LOCK_ANCHOR_ART_SIZE.width}
        y={(anchor.y * size.height) / LOCK_ANCHOR_ART_SIZE.height}
        isUnlocked={isUnlocked}
        isForced={isForced}
      />
    </FigureButton>
  );
}

/**
 * Kilit kanalının son başarılı komutu. Bir ölçüm değildir. Zorlanmış açılışta (kapı açık + kilit kilitli) kırmızı, bilinmiyorsa gri.
 */
function LockBadge({ x, y, isUnlocked, isForced }: { x: number; y: number; isUnlocked: boolean | null; isForced: boolean }) {
  const fill = isForced ? '#DC2626' : isUnlocked === null ? '#64748B' : isUnlocked ? '#D97706' : '#15803D';

  return (
    <g transform={`translate(${x}, ${y})`}>
      <circle r='13' fill={fill} stroke='#FFFFFF' strokeWidth='2' />
      <g transform='translate(-9, -9) scale(0.75)' fill='none' stroke='#FFFFFF' strokeWidth='2.5' strokeLinecap='round' strokeLinejoin='round'>
        <rect x='3' y='11' width='18' height='11' rx='2' />
        <path d={isUnlocked ? 'M7 11V7a5 5 0 0 1 9.9-1' : 'M7 11V7a5 5 0 0 1 10 0v4'} />
      </g>
    </g>
  );
}
