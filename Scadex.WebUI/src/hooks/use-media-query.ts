import { useCallback, useSyncExternalStore } from 'react';

// Sorgu başına tek `MediaQueryList`. Modul yuklenirken window'a dokunulmaz (test/node ortami icin);
// ilk kullanimda kurulur.
const mediaQueries = new Map<string, MediaQueryList>();

function getMediaQuery(query: string): MediaQueryList {
  let mql = mediaQueries.get(query);
  if (!mql) {
    mql = window.matchMedia(query);
    mediaQueries.set(query, mql);
  }
  return mql;
}

/** CSS medya sorgusunun anlık sonucu; eşleşme değişince bileşen yeniden render olur. */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onStoreChange: () => void) => {
      const mql = getMediaQuery(query);
      mql.addEventListener('change', onStoreChange);
      return () => mql.removeEventListener('change', onStoreChange);
    },
    [query]
  );
  return useSyncExternalStore(subscribe, () => getMediaQuery(query).matches);
}
