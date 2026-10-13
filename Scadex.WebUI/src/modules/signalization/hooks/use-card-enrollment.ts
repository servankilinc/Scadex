import { useCallback, useEffect, useState } from 'react';
import type { SignalCardPresentedMessage } from '../models/realtime';
import { subscribeToCabinetState, useSignalizationHubStatus } from '../signalr/signalization-hub';

/** Pencerede gösterilen okuma. `seq` her okumada artar: aynı kart tekrar okutulunca da ekran (seçim vb.) sıfırlansın. */
export interface EnrollmentRead extends SignalCardPresentedMessage {
  seq: number;
}

let readSeq = 0;

const ignore = () => {};

/**
 * "Kart tanımlama" penceresinin kaynağı: kabinin `SignalCardPresented` yayınını dinler (sanal kabin ekranıyla aynı sayaçlı abonelik).
 * Okuma motora normal gider — pencere yalnızca izler. Yalnızca SON okuma tutulur.
 */
export function useCardEnrollment(cabinetId: string) {
  const [read, setRead] = useState<EnrollmentRead | null>(null);
  const hubStatus = useSignalizationHubStatus();

  useEffect(
    () =>
      subscribeToCabinetState(cabinetId, {
        onCabinetStateChanged: ignore,
        onDoorSwitchChanged: ignore,
        onCardPresented: message => setRead({ ...message, seq: ++readSeq })
      }),
    [cabinetId]
  );

  /** Kart atandıktan sonra ekran "Tanımlı: …" durumuna geçsin — aynı kartı yeniden okutmaya gerek kalmasın. */
  const markAssigned = useCallback((userId: string, userFullName: string) => {
    setRead(current => (current ? { ...current, userId, userFullName } : current));
  }, []);

  return { read, hubStatus, markAssigned };
}
