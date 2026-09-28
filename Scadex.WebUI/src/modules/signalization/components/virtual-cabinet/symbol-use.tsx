import type { ArtworkBox, DeviceSymbol } from './artwork';

/** Bir cihaz dosyasının sembolünü çerçeveye çizer; sığdırma sembolün `viewBox` / `preserveAspectRatio`'suyla yapılır. */
export function SymbolUse({ symbol, frame }: { symbol: DeviceSymbol; frame: ArtworkBox }) {
  return <use href={`#${symbol}`} x={frame.x} y={frame.y} width={frame.width} height={frame.height} />;
}
