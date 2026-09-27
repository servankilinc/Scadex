import { useCallback, useEffect, useRef, useState } from 'react';
import { startStreamSession, type StreamSessionHandle, type StreamState } from '@/lib/camera/stream-session';
import type { CameraDto } from '@/models/camera';
import { StreamProfile } from '@/models/enums/entityEnums';

interface Options {
  /** `false` ise hiç bağlanılmaz — grid'de görünmeyen kutucuklar için. */
  enabled?: boolean;
}

export interface CameraStream {
  state: StreamState;
  error?: string;
  retry: () => void;
}

/**
 * Bir kameraya canlı bağlanır ve bileşen sökülünce temizler.
 *
 * Oturumun kendisi React'in dışında (`lib/camera/stream-session.ts`); bu hook
 * yalnızca yaşam döngüsünü bağlar ve durumu render'a taşır.
 *
 * **`videoRef` DÖNDÜRÜLMEZ, PARAMETRE olarak alınır.** Ref'i dönüş nesnesinin
 * içine koymak doğal görünüyor ama React Compiler o nesnenin tamamını
 * "ref benzeri" sayıyor ve `state` alanını render'da okumayı da ref erişimi
 * olarak işaretliyor. Ref'i girdi yapmak ayrımı net tutuyor: giren şey DOM
 * tutamacı, çıkan şey render verisi.
 */
export function useCameraStream(
  videoRef: React.RefObject<HTMLVideoElement | null>,
  camera: CameraDto | undefined,
  profile: StreamProfile,
  options: Options = {}
): CameraStream {
  const { enabled = true } = options;

  const sessionRef = useRef<StreamSessionHandle | null>(null);

  const [state, setState] = useState<StreamState>('queued');
  const [error, setError] = useState<string | undefined>();

  // Tali akımı olmayan markada `Sub` isteğini sunucu ana akım yoluna düşürür;
  // istemci farkı görmez, doğru yol WHEP adresinin içinde gelir.
  const cameraId = camera?.id;

  useEffect(() => {
    if (!enabled || !cameraId) return;

    const videoEl = videoRef.current;
    if (!videoEl) return;

    const session = startStreamSession({
      cameraId,
      profile,
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
  }, [cameraId, profile, enabled, videoRef]);

  const retry = useCallback(() => sessionRef.current?.retry(), []);

  return { state, error, retry };
}
