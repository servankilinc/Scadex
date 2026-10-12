import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeftIcon, ArrowRightIcon, LoaderIcon, TriangleAlertIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import type { FileTransferApi } from '../lib/file-transfer-session';
import { FileTransferStatus, type DirEntryDto } from '../models/fileTransfer';
import { type LocalState } from '../lib/local-fs';
import { LocalFilePane } from './local-file-pane';
import { RemoteFilePane } from './remote-file-pane';

/**
 * Dosya aktarımı penceresi (RemoteDesk.md § 12.8): solda yerel, sağda uzak gezgin, ortada Yükle/İndir. Yerelde dosya seçili + Yükle →
 * uzak aktif klasöre; uzakta dosya seçili + İndir → yerel aktif klasöre (FSA) ya da tarayıcının indirilenler klasörüne (fallback).
 * Transfer izlemeye bağlıdır (kontrol almak gerekmez); `files` oturumu PC izlenirken açıktır.
 */
export function FileTransferDialog({ deviceId, files, onClose }: { deviceId: string; files: FileTransferApi; onClose: () => void }) {
  const queryClient = useQueryClient();

  const [remotePath, setRemotePath] = useState<string | null>(null);
  const [remoteIsRoot, setRemoteIsRoot] = useState(true);
  const [remoteSel, setRemoteSel] = useState<{ entry: DirEntryDto; fullPath: string } | null>(null);
  const [local, setLocal] = useState<LocalState>({ dir: null, selected: null });

  const [busy, setBusy] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ text: string; error: boolean } | null>(null);

  const uploadName = local.selected?.name ?? null;
  const canUpload = !busy && uploadName !== null && remotePath !== null && !remoteIsRoot;
  const canDownload = !busy && remoteSel !== null && local.dir !== null;

  const onRemoteSelect = (entry: DirEntryDto | null, fullPath: string | null) =>
    setRemoteSel(entry && fullPath ? { entry, fullPath } : null);

  const onRemotePath = (path: string | null, isRoot: boolean) => {
    setRemotePath(path);
    setRemoteIsRoot(isRoot);
  };

  const doUpload = async () => {
    if (!canUpload || remotePath === null) return;
    setBusy(`"${uploadName}" yükleniyor…`);
    setNotice(null);
    try {
      const file = await local.selected!.handle.getFile();
      const state = await files.upload(file, remotePath);
      if (state.status === FileTransferStatus.Completed) {
        setNotice({ text: `"${file.name}" uzak klasöre yüklendi.`, error: false });
        void queryClient.invalidateQueries({ queryKey: ['remoteDesk', 'fileBrowser', deviceId, remotePath] });
      } else {
        setNotice({ text: state.error ?? 'Yükleme başarısız.', error: true });
      }
    } catch (e) {
      setNotice({ text: toMessage(e), error: true });
    } finally {
      setBusy(null);
    }
  };

  const doDownload = async () => {
    if (!canDownload || !remoteSel) return;
    const name = remoteSel.entry.name;
    setBusy(`"${name}" indiriliyor…`);
    setNotice(null);
    try {
      const state = await files.prepareDownload(remoteSel.fullPath);
      if (state.status !== FileTransferStatus.ReadyForBrowser) {
        setNotice({ text: state.error ?? 'İndirme başarısız.', error: true });
        return;
      }
      const blob = await files.fetchDownloadBlob(state.transferId);
      const handle = await local.dir!.getFileHandle(name, { create: true });
      const writable = await handle.createWritable();
      await writable.write(blob);
      await writable.close();
      setNotice({ text: `"${name}" yerel klasöre indirildi. (Görmek için sol panelde Yenile'ye basın.)`, error: false });
    } catch (e) {
      setNotice({ text: toMessage(e), error: true });
    } finally {
      setBusy(null);
    }
  };

  return (
    <Dialog open onOpenChange={open => !open && !busy && onClose()}>
      <DialogContent className='flex h-[80vh] w-full max-w-[calc(100%-2rem)] flex-col gap-3 sm:max-w-5xl'>
        <DialogHeader>
          <DialogTitle>Dosya aktarımı</DialogTitle>
          <DialogDescription>Sol panel bu bilgisayar, sağ panel uzak PC. Bir dosya seçip ortadaki düğmeyle aktarın.</DialogDescription>
        </DialogHeader>

        {(busy || notice) && (
          <div className='flex items-center gap-2 text-sm'>
            {busy ? (
              <span className='flex items-center gap-2 text-muted-foreground'>
                <LoaderIcon className='size-4 animate-spin' />
                {busy}
              </span>
            ) : notice ? (
              <span className={notice.error ? 'flex items-center gap-1.5 text-amber-600 dark:text-amber-400' : 'text-emerald-600 dark:text-emerald-400'}>
                {notice.error && <TriangleAlertIcon className='size-4 shrink-0' />}
                {notice.text}
              </span>
            ) : null}
          </div>
        )}

        <div className='flex min-h-0 flex-1 gap-3'>
          <LocalFilePane onChange={setLocal} disabled={!!busy} />

          <div className='flex shrink-0 flex-col items-center justify-center gap-3'>
            <Button onClick={() => void doUpload()} disabled={!canUpload} title='Seçili yerel dosyayı uzak aktif klasöre yükle'>
              Yükle
              <ArrowRightIcon />
            </Button>
            <Button onClick={() => void doDownload()} disabled={!canDownload} title='Seçili uzak dosyayı yerel aktif klasöre indir'>
              <ArrowLeftIcon />
              İndir
            </Button>
          </div>

          <RemoteFilePane deviceId={deviceId} files={files} onSelect={onRemoteSelect} onPathChange={onRemotePath} disabled={!!busy} />
        </div>
      </DialogContent>
    </Dialog>
  );
}

function toMessage(e: unknown): string {
  return e instanceof Error ? e.message : 'İşlem başarısız.';
}
