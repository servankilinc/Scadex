import { useEffect, useMemo, useRef } from 'react';
import { startFileTransferSession, type FileTransferApi, type FileTransferHandle } from '../lib/file-transfer-session';

/**
 * PC dosya transferi oturumu (RemoteDesk.md § 12.8). İzleme sürerken + `RemotePcFileTransfer` izniyle açılır; uzaktan KONTROL ALMAK
 * gerekmez. Kendi viewer hub bağlantısını taşır (kontrol oturumundan ayrı); `enabled` kalkınca ya da bileşen sökülünce kapanır.
 * Oturum ilk işlemde tembel (lazy) kurulur — böylece izleme hazır olmadan boş bağlantı açılmaz ve gezgin panelinin ilk sorgusu bekler.
 */
export function useFileTransfer(deviceId: string, enabled: boolean): FileTransferApi {
  const handleRef = useRef<FileTransferHandle | null>(null);

  // enabled true iken açılan oturum, enabled false'a dönünce ya da PC değişince / sökülünce kapanır.
  useEffect(() => {
    if (!enabled) return;
    return () => {
      handleRef.current?.stop();
      handleRef.current = null;
    };
  }, [deviceId, enabled]);

  // Stabil API: metotlar çağrı anında (render'da değil) handleRef'i okur ve oturumu ilk kullanımda kurar.
  return useMemo<FileTransferApi>(() => {
    const ensure = (): FileTransferApi => {
      if (!enabled) throw new Error('PC izlenmiyor.');
      return (handleRef.current ??= startFileTransferSession(deviceId)).files;
    };
    return {
      listDirectory: async path => ensure().listDirectory(path),
      prepareDownload: async pcPath => ensure().prepareDownload(pcPath),
      fetchDownloadBlob: async transferId => ensure().fetchDownloadBlob(transferId),
      upload: async (file, dest) => ensure().upload(file, dest),
      getRemoteClipboard: async () => ensure().getRemoteClipboard(),
      setRemoteClipboard: async text => ensure().setRemoteClipboard(text)
    };
  }, [deviceId, enabled]);
}
