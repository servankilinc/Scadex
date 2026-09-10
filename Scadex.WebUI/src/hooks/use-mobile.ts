import { useSyncExternalStore } from 'react';

const MOBILE_BREAKPOINT = 768;
const QUERY = `(max-width: ${MOBILE_BREAKPOINT - 1}px)`;

// Modul yuklenirken window'a dokunulmaz (test/node ortami icin); ilk kullanimda kurulur.
let mediaQuery: MediaQueryList | null = null;
function getMediaQuery(): MediaQueryList {
  mediaQuery ??= window.matchMedia(QUERY);
  return mediaQuery;
}

function subscribe(onStoreChange: () => void): () => void {
  const mql = getMediaQuery();
  mql.addEventListener('change', onStoreChange);
  return () => mql.removeEventListener('change', onStoreChange);
}

export function useIsMobile(): boolean {
  return useSyncExternalStore(subscribe, () => getMediaQuery().matches);
}
