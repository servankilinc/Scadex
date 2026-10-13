import { useCallback, useEffect, useState } from 'react';
import { subscribeToCabinet, useHubStatus } from '@/lib/signalr/diagram-hub';
import type { CardRead } from '@/models/realtime';

/** Pencerede gösterilen okuma. `seq` her okumada artar: aynı kart tekrar okutulunca da ekran (seçim vb.) sıfırlansın. */
export interface EnrollmentRead extends CardRead {
  seq: number;
}

let readSeq = 0;

const ignore = () => {};

/**
 * Kabinler → "Kart tanımlama" penceresinin kaynağı: kabinin `CardRead` yayınını dinler (`/hubs/diagram`, diyagram ekranıyla aynı
 * sayaçlı abonelik). Okumayı yalnızca izler — modüller (ör. sinyalizasyon motoru) okumayı her zamanki gibi işler. Yalnızca SON okuma tutulur.
 */
export function useCardEnrollment(cabinetId: string) {
  const [read, setRead] = useState<EnrollmentRead | null>(null);
  const hubStatus = useHubStatus();

  useEffect(
    () =>
      subscribeToCabinet(cabinetId, {
        onChannelValues: ignore,
        onDeviceStatus: ignore,
        onCabinetStatus: ignore,
        onCommandCompleted: ignore,
        // İstemci olayları kabine göre dağıtmıyor; başka bir ekranın abone olduğu kabinin okuması da buraya gelir.
        onCardRead: change => {
          if (change.cabinetId === cabinetId) setRead({ ...change, seq: ++readSeq });
        }
      }),
    [cabinetId]
  );

  /** Kart atandıktan sonra ekran "Tanımlı: …" durumuna geçsin — aynı kartı yeniden okutmaya gerek kalmasın. */
  const markAssigned = useCallback((userId: string, userFullName: string) => {
    setRead(current => (current ? { ...current, userId, userFullName } : current));
  }, []);

  return { read, hubStatus, markAssigned };
}
