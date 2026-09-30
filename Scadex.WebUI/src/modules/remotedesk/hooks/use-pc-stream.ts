import { useCallback, useEffect, useRef, useState } from 'react';
import { startPcStreamSession, type PcStreamSessionHandle, type PcStreamState } from '../lib/pc-stream-session';

export interface PcStream {
  state: PcStreamState;
  error?: string;
  retry: () => void;
}

/**
 * Bir PC monitörünü izler ve bileşen sökülünce kiralamayı bırakır. `useCameraStream` ile aynı kalıp: oturum React'in dışında,
 * hook yalnızca yaşam döngüsünü bağlar. `videoRef` PARAMETREDİR (React Compiler ref/render ayrımı — `use-camera-stream.ts`).
 */
export function usePcStream(videoRef: React.RefObject<HTMLVideoElement | null>, deviceId: string, monitorIndex: number): PcStream {
  const sessionRef = useRef<PcStreamSessionHandle | null>(null);
  const [state, setState] = useState<PcStreamState>('starting');
  const [error, setError] = useState<string | undefined>();

  useEffect(() => {
    const videoEl = videoRef.current;
    if (!videoEl) return;

    const session = startPcStreamSession({
      deviceId,
      monitorIndex,
      videoEl,
      onState: (next, message) => {
        setState(next);
        setError(message);
      }
    });
    sessionRef.current = session;

    return () => {
      session.close();
      sessionRef.current = null;
    };
  }, [deviceId, monitorIndex, videoRef]);

  const retry = useCallback(() => sessionRef.current?.retry(), []);

  return { state, error, retry };
}
