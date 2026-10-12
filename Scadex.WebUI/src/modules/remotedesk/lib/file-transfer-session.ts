import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { API_BASE_URL } from '@/lib/axios-helper';
import { getAccessToken } from '@/lib/auth-session';
import type { FileTransferResponse, FileTransferStateNotice, ListDirectoryResponse } from '../models/fileTransfer';
import type { ClipboardResult } from '../models/clipboard';

/**
 * Bir PC'yle dosya transferi (RemoteDesk.md § 12.8). React'in dışında; `use-file-transfer.ts` yaşam döngüsünü bağlar.
 *
 * - Kendi `/hubs/remote-desk/viewer` bağlantısını açar; uzaktan KONTROLDEN bağımsızdır (2026-10-05 kararı). İşlem için PC'yi izliyor
 *   olmak yeter — bu, `use-pc-stream.ts`'in aynı kullanıcı için açtığı izleme kiralamasıyla sunucuda doğrulanır (aynı JWT, aynı userId).
 * - Dizin listesi küçük JSON, hub'dan (client-result) gelir. Dosyanın baytları hub'dan GEÇMEZ: biletli HTTP uçlarıyla merkez üzerinden
 *   akar. İndirme/yükleme sonucu `FileTransferState` ile bildirilir; `transferId` ile bekleyen söz verici çözülür.
 * - Bağlantı ilk işlemde (lazy) başlatılır; kopar ya da kapanırsa sonraki işlem yeniden başlatır. Otomatik yeniden bağlanma YOK.
 */

/** Dosya transferi işlemleri; tümü PC'yi O AN izliyor olmayı ister (sunucu doğrular). */
export interface FileTransferApi {
  /** PC'de bir klasörü (ya da `null`/`''` ile sürücü köklerini) listeler. */
  listDirectory: (path: string | null) => Promise<ListDirectoryResponse>;
  /**
   * İndirmeyi başlatır: PC dosyayı merkeze yükler; durum `ReadyForBrowser` olunca (ya da `Failed`) çözülür. Baytı çeken adım ayrıdır
   * (`fetchDownloadBlob`) — çağıran, baytı tarayıcıya mı yoksa seçili yerel klasöre mi yazacağına karar verir (iki panelli pencere).
   */
  prepareDownload: (pcPath: string) => Promise<FileTransferStateNotice>;
  /** Hazır transferin baytlarını çeker (biletli HTTP; tek kullanımlık — bir kez çağrılır). */
  fetchDownloadBlob: (transferId: string) => Promise<Blob>;
  /** Tarayıcıdaki dosyayı PC'deki klasöre yükler; biter/başarısız olunca çözülür. */
  upload: (file: File, destinationPath: string) => Promise<FileTransferStateNotice>;
  /** Uzak PC panosunun metnini getirir (içe aktar, RemoteDesk.md § 12.9). */
  getRemoteClipboard: () => Promise<ClipboardResult>;
  /** Metni uzak PC panosuna yazar (dışa aktar). */
  setRemoteClipboard: (text: string) => Promise<ClipboardResult>;
}

export interface FileTransferHandle {
  files: FileTransferApi;
  /** Bağlantıyı kapatır. Birden çok kez çağrılabilir. */
  stop: () => void;
}

const FILE_TIMEOUT_MS = 10 * 60 * 1000;

export function startFileTransferSession(deviceId: string): FileTransferHandle {
  let connection: HubConnection | null = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/remote-desk/viewer`, { accessTokenFactory: () => getAccessToken() ?? '' })
    .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
    .build();

  let stopped = false;
  let startPromise: Promise<void> | null = null;

  // Süren transferlerin bekleyen söz vericileri (transferId → resolve). FileTransferState ile çözülür.
  const pendingTransfers = new Map<string, (notice: FileTransferStateNotice) => void>();

  connection.on('FileTransferState', (notice: FileTransferStateNotice) => {
    pendingTransfers.get(notice.transferId)?.(notice);
  });
  connection.onclose(() => {
    // Kopunca sonraki işlem yeniden başlatsın; süren transferler zaman aşımıyla düşer.
    startPromise = null;
  });

  function ensureStarted(): Promise<void> {
    const conn = connection;
    if (!conn) return Promise.reject(new Error('Dosya oturumu kapandı.'));
    if (conn.state === HubConnectionState.Connected) return Promise.resolve();
    if (!startPromise) {
      startPromise = conn.start().catch(error => {
        startPromise = null;
        throw error;
      });
    }
    return startPromise;
  }

  async function invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    await ensureStarted();
    const conn = connection;
    if (!conn) throw new Error('Dosya oturumu kapandı.');
    return conn.invoke<T>(method, ...args);
  }

  function waitTransfer(transferId: string): Promise<FileTransferStateNotice> {
    return new Promise((resolve, reject) => {
      const timer = window.setTimeout(() => {
        pendingTransfers.delete(transferId);
        reject(new Error('Transfer zaman aşımına uğradı.'));
      }, FILE_TIMEOUT_MS);
      pendingTransfers.set(transferId, notice => {
        window.clearTimeout(timer);
        pendingTransfers.delete(transferId);
        resolve(notice);
      });
    });
  }

  async function httpAuth(path: string, init: RequestInit): Promise<Response> {
    const headers = new Headers(init.headers);
    headers.set('Authorization', `Bearer ${getAccessToken() ?? ''}`);
    return fetch(`${API_BASE_URL}${path}`, { ...init, headers });
  }

  const files: FileTransferApi = {
    listDirectory: path => invoke<ListDirectoryResponse>('ListPcDirectory', deviceId, path ?? ''),
    prepareDownload: async pcPath => {
      const res = await invoke<FileTransferResponse>('RequestDownload', deviceId, pcPath);
      if (!res.accepted || !res.transferId) throw new Error(res.message ?? 'İndirme reddedildi.');
      return waitTransfer(res.transferId);
    },
    fetchDownloadBlob: async transferId => {
      const response = await httpAuth(`/api/RemoteDesk/filetransfer/${transferId}`, { method: 'GET' });
      if (!response.ok) throw new Error('Dosya merkeze indirilemedi.');
      return response.blob();
    },
    upload: async (file, destinationPath) => {
      // 1) dosyayı merkeze yükle
      const centralResp = await httpAuth(`/api/RemoteDesk/filetransfer?fileName=${encodeURIComponent(file.name)}`, { method: 'POST', body: file });
      const central: FileTransferResponse = await centralResp.json();
      if (!centralResp.ok || !central.accepted || !central.transferId) throw new Error(central.message ?? 'Merkeze yükleme reddedildi.');
      // 2) PC'ye yazdır
      const res = await invoke<FileTransferResponse>('RequestUpload', deviceId, central.transferId, destinationPath);
      if (!res.accepted) throw new Error(res.message ?? 'Yükleme reddedildi.');
      return waitTransfer(central.transferId);
    },
    getRemoteClipboard: () => invoke<ClipboardResult>('GetRemoteClipboard', deviceId),
    setRemoteClipboard: text => invoke<ClipboardResult>('SetRemoteClipboard', deviceId, text)
  };

  function stop(): void {
    if (stopped) return;
    stopped = true;
    const conn = connection;
    connection = null;
    if (conn) void conn.stop().catch(() => undefined);
  }

  return { files, stop };
}
