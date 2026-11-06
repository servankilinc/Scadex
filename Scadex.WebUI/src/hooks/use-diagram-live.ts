import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { diagramKeys } from '@/api/query-keys';
import { applyAutomationStatus, applyChannelValues, applyDeviceStatuses, resetLiveStore } from '@/lib/diagram/live-store';
import { subscribeToAutomation, useHubStatus, type HubStatus } from '@/lib/signalr/diagram-hub';
import { applyCommandCompleted } from './use-device-commands';

/**
 * Bir otomasyonun canlı yayınını açar ve gelen olayları `live-store`'a yazar.
 *
 * Hook'un kendisi HİÇBİR ŞEY DÖNDÜRMEZ (durum dışında): telemetri React state'ine
 * değil harici store'a gider, bileşenler onu kendi kimlikleriyle okur. Değerleri
 * buradan prop olarak aşağı taşımak, her tick'te tüm ağacı yeniden çizdirirdi.
 */
export function useDiagramLive(automationId: string): HubStatus {
  const status = useHubStatus();
  const queryClient = useQueryClient();

  useEffect(() => {
    const dispose = subscribeToAutomation(automationId, {
      onChannelValues: applyChannelValues,
      onDeviceStatus: applyDeviceStatuses,
      onAutomationStatus: applyAutomationStatus,
      // Kumanda sonucu `live-store`'a YAZILMAZ: telemetri gibi "otomasyonun o anki
      // hâli" değil, bir olaydır ve yeri geçmiş listesidir. Store'a konsaydı
      // sonraki komutta üzerine yazılır ve geçmiş diye bir şey kalmazdı.
      onCommandCompleted: change => applyCommandCompleted(queryClient, change)
    });

    return () => {
      dispose();
      // Otomasyon değiştiğinde önceki otomasyonun değerleri kalmamalı: kimlikler farklı
      // olduğu için yanlış node'a düşmezler, ama bellekte birikirler.
      resetLiveStore();
    };
  }, [automationId, queryClient]);

  // Kopukluk sırasında saha değişmeye devam eder, ekrandaki değerler donar.
  // Bağlantı geri geldiğinde tek doğru telafi TAZELEME'dir: hub bir olay kuyruğu
  // değil, anlık değer yayınıdır — kaçırılan olaylar geri gelmez.
  //
  // Grafın yeniden çekilmesi de gerekiyor: kopukluk sırasında SCADA yeni bir
  // cihaz tanımış ya da bir kanal eklenmiş olabilir.
  //
  // `invalidateQueries` burada güvenli: `useDiagramEditor` sunucu grafını yalnızca
  // kaydedilmemiş iş YOKKEN state'ine alıyor, dolayısıyla açık bir düzenlemeyi ezemez.
  const hasConnectedBefore = useRef(false);
  useEffect(() => {
    if (status !== 'connected') return;

    // İLK bağlantıda tazeleme YAPILMAZ: graf zaten yeni çekildi, hemen ardından
    // bir kez daha çekmek her editör açılışına gereksiz bir istek eklerdi.
    if (!hasConnectedBefore.current) {
      hasConnectedBefore.current = true;
      return;
    }

    resetLiveStore();
    void queryClient.invalidateQueries({ queryKey: diagramKeys.automation(automationId) });
  }, [status, automationId, queryClient]);

  return status;
}
