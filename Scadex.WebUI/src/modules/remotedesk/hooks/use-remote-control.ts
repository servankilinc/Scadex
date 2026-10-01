import { useCallback, useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { remoteDeskKeys } from '../api/query-keys';
import { startRemoteControl, type RemoteControlHandle } from '../lib/remote-control-session';

export type RemoteControlStatus = 'idle' | 'requesting' | 'active';

export interface RemoteControl {
  status: RemoteControlStatus;
  /** Son red/bitiş nedeni (kullanıcı kendisi bıraktıysa boş). */
  message?: string;
  request: () => void;
  release: () => void;
  /** Tarayıcının yakalayamadığı tuş kombinasyonunu gönderir (yalnızca kontrol sizdeyken). */
  sendCombo: (codes: string[]) => void;
}

/**
 * PC'nin uzaktan kontrolünü bir oynatıcıya bağlar. Oturum yalnızca kullanıcı isteyince açılır; bileşen sökülünce (ekrandan çıkış, monitör
 * değişimi) kontrol bırakılır. `surfaceRef` / `videoRef` PARAMETREDİR (React Compiler ref/render ayrımı — `use-pc-stream.ts`).
 */
export function useRemoteControl(
  surfaceRef: React.RefObject<HTMLElement | null>,
  videoRef: React.RefObject<HTMLVideoElement | null>,
  deviceId: string,
  monitorIndex: number
): RemoteControl {
  const queryClient = useQueryClient();
  const handleRef = useRef<RemoteControlHandle | null>(null);
  const [status, setStatus] = useState<RemoteControlStatus>('idle');
  const [message, setMessage] = useState<string | undefined>();

  const release = useCallback(() => {
    handleRef.current?.stop();
    handleRef.current = null;
  }, []);

  const request = useCallback(() => {
    const surface = surfaceRef.current;
    const video = videoRef.current;
    if (!surface || !video || handleRef.current) return;

    setMessage(undefined);
    handleRef.current = startRemoteControl({
      deviceId,
      monitorIndex,
      surface,
      video,
      onState: (state, text) => {
        if (state === 'ended') {
          handleRef.current = null;
          setStatus('idle');
          setMessage(text);
        } else {
          setStatus(state);
        }
        // "Kim kontrol ediyor" PC ekranında 10 sn'lik yoklamayı beklemesin.
        if (state !== 'requesting') void queryClient.invalidateQueries({ queryKey: remoteDeskKeys.pc(deviceId) });
      }
    });
  }, [deviceId, monitorIndex, surfaceRef, videoRef, queryClient]);

  const sendCombo = useCallback((codes: string[]) => handleRef.current?.sendCombo(codes), []);

  // Sökülünce ya da PC/monitör değişince bırak.
  useEffect(() => release, [deviceId, monitorIndex, release]);

  return { status, message, request, release, sendCombo };
}
