import { useCallback, useEffect, useState } from 'react';
import { subscribeToAutomation, useHubStatus } from '@/lib/signalr/diagram-hub';
import type { CardReadNotify } from '@/models/realtime';

/** Pencerede gösterilen okuma. `seq` her okumada artar: aynı kart tekrar okutulunca da ekran (seçim vb.) sıfırlansın. */
export interface EnrollmentRead extends CardReadNotify {
  seq: number;
}

let readSeq = 0;

const ignore = () => {};

/**
 * Otomasyonlar → "Kart tanımlama" penceresinin kaynağı: otomasyonun `CardRead` yayınını dinler (`/hubs/diagram`, diyagram ekranıyla aynı
 * sayaçlı abonelik). Okumayı yalnızca izler — modüller (ör. sinyalizasyon motoru) okumayı her zamanki gibi işler. Yalnızca SON okuma tutulur.
 */
export function useCardEnrollment(automationId: string) {
  const [read, setRead] = useState<EnrollmentRead | null>(null);
  const hubStatus = useHubStatus();

  useEffect(
    () =>
      subscribeToAutomation(automationId, {
        onChannelValues: ignore,
        onDeviceStatus: ignore,
        onAutomationStatus: ignore,
        onCommandCompleted: ignore,
        // İstemci olayları otomasyona göre dağıtmıyor; başka bir ekranın abone olduğu otomasyonun okuması da buraya gelir.
        onCardRead: change => {
          if (change.automationId === automationId) setRead({ ...change, seq: ++readSeq });
        }
      }),
    [automationId]
  );

  /** Kart atandıktan sonra ekran "Tanımlı: …" durumuna geçsin — aynı kartı yeniden okutmaya gerek kalmasın. */
  const markAssigned = useCallback((userId: string, userFullName: string) => {
    setRead(current => (current ? { ...current, userId, userFullName } : current));
  }, []);

  return { read, hubStatus, markAssigned };
}
