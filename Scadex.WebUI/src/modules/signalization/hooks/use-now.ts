import { useEffect, useState } from 'react';

/**
 * Saniyede bir ilerleyen "şimdi" — canlı paneldeki geçen süre sayacı için. Süre, sunucunun yanıt anındaki
 * `elapsedSec` + yanıttan bu yana geçen yerel süre olarak hesaplanır; istemci saatinin sunucudan kaymış
 * olması bu yüzden sonucu etkilemez.
 */
export function useNow(intervalMs = 1000): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), intervalMs);
    return () => window.clearInterval(timer);
  }, [intervalMs]);

  return now;
}
