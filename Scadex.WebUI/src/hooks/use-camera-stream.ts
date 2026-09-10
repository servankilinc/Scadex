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

  const cameraId = camera?.id;

  // İlgili akım kapalıysa hiç bağlanma. Sunucu zaten 400 döner; bu kontrol
  // yalnızca boşuna gidiş-gelişi engelliyor ve sebebi anında gösteriyor.
  //
  // Tali akım kapalıyken ana akıma SESSİZCE DÜŞÜLMÜYOR: `Camera.SubStreamEnabled`
  // "bilinçli bir tercih olmalı" diye işaretli ve otomatik düşmek, 12 kutucuklu
  // bir grid'i kamera başına ~4 Mbps'e çıkarırdı.
  const streamDisabled =
    camera != null && (profile === StreamProfile.Main ? !camera.mainStreamEnabled : !camera.subStreamEnabled);

  useEffect(() => {
    if (!enabled || !cameraId || streamDisabled) return;

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
  }, [cameraId, profile, enabled, streamDisabled, videoRef]);

  const retry = useCallback(() => sessionRef.current?.retry(), []);

  if (streamDisabled) {
    return {
      state: 'failed',
      error: profile === StreamProfile.Main ? 'Ana akım bu kamerada kapalı.' : 'Tali akım bu kamerada kapalı.',
      retry
    };
  }

  return { state, error, retry };
}
