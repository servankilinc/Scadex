import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowUpIcon, FileIcon, FolderIcon, HardDriveIcon, RefreshCwIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { formatUtcDateTime } from '@/lib/utils';
import type { FileTransferApi } from '../lib/file-transfer-session';
import { joinPath, formatBytes } from '../lib/file-path';
import type { DirEntryDto } from '../models/fileTransfer';

/**
 * Uzak (PC) dosya gezgini — dosya transferi penceresinin sağ paneli (RemoteDesk.md § 12.8). Listeleme/gezinme react-query ile
 * (`['remoteDesk','fileBrowser',deviceId,path]`; dialog yükleme sonrası aynı anahtarı invalidate eder). Satıra tıklama dosyayı
 * SEÇER (ortadaki "İndir" düğmesi için), klasöre çift tık girer. Seçili dosya ve aktif klasör dialoga bildirilir.
 */
export function RemoteFilePane({
  deviceId,
  files,
  onSelect,
  onPathChange,
  disabled
}: {
  deviceId: string;
  files: FileTransferApi;
  /** Seçilen dosya ve tam yolu (klasör seçilince / seçim temizlenince `null`). */
  onSelect: (entry: DirEntryDto | null, fullPath: string | null) => void;
  /** Aktif klasör değişti (`path` = null sürücü kökleri; `isRoot` kökte mi). */
  onPathChange: (path: string | null, isRoot: boolean) => void;
  /** Aktarım sürerken etkileşim kapalı. */
  disabled: boolean;
}) {
  // path null = sürücü kökleri.
  const [path, setPath] = useState<string | null>(null);
  const [selectedName, setSelectedName] = useState<string | null>(null);

  const listing = useQuery({
    queryKey: ['remoteDesk', 'fileBrowser', deviceId, path ?? ''],
    queryFn: () => files.listDirectory(path),
    retry: false
  });
  const data = listing.data;
  const isRoot = data?.isRoot ?? path === null;

  const navigate = (target: string | null) => {
    setPath(target);
    setSelectedName(null);
    onSelect(null, null);
    // Sunucu yanıtı gelince isRoot netleşir; şimdilik hedefe göre bildir (kök yalnızca null hedefte).
    onPathChange(target, target === null);
  };

  const select = (entry: DirEntryDto) => {
    if (isRoot) return; // kökte girişler sürücüdür, dosya değil
    setSelectedName(entry.name);
    onSelect(entry, joinPath(path, entry.name));
  };

  const openEntry = (entry: DirEntryDto) => {
    if (entry.isDirectory) navigate(isRoot ? entry.name : joinPath(path, entry.name));
    else select(entry);
  };

  return (
    <div className='flex min-h-0 min-w-0 flex-1 flex-col gap-2'>
      <div className='flex items-center gap-2'>
        <span className='text-sm font-medium'>Uzak PC</span>
        <Button size='icon-sm' variant='ghost' disabled={listing.isFetching || disabled} onClick={() => void listing.refetch()} title='Yenile'>
          <RefreshCwIcon className={listing.isFetching ? 'animate-spin' : undefined} />
        </Button>
        <Button
          size='icon-sm'
          variant='ghost'
          disabled={isRoot || listing.isFetching || disabled}
          onClick={() => navigate(data?.parentPath ? data.parentPath : null)}
          title='Üst klasör'>
          <ArrowUpIcon />
        </Button>
        <code className='min-w-0 flex-1 truncate rounded bg-muted px-2 py-1 text-xs' title={isRoot ? 'Bu Bilgisayar' : (data?.path ?? '')}>
          {isRoot ? 'Bu Bilgisayar' : (data?.path ?? path ?? '')}
        </code>
      </div>

      {data && !data.allowed && <div className='text-sm text-amber-600 dark:text-amber-400'>{data.message}</div>}
      {data?.error && <div className='text-sm text-muted-foreground'>{data.error}</div>}
      {listing.isError && <div className='text-sm text-amber-600 dark:text-amber-400'>Klasör okunamadı.</div>}

      <div className='scrollbar-thin min-h-0 flex-1 overflow-y-auto rounded-lg border'>
        <table className='w-full text-sm'>
          <tbody>
            {data?.entries.map(entry => (
              <tr
                key={entry.name}
                className={
                  'cursor-pointer border-b last:border-0 hover:bg-muted/60' +
                  (!entry.isDirectory && entry.name === selectedName ? ' bg-primary/10' : '')
                }
                onClick={() => !disabled && select(entry)}
                onDoubleClick={() => !disabled && openEntry(entry)}>
                <td className='w-6 py-1.5 pl-3'>
                  {isRoot ? <HardDriveIcon className='size-4 text-muted-foreground' /> : entry.isDirectory ? <FolderIcon className='size-4 text-sky-500' /> : <FileIcon className='size-4 text-muted-foreground' />}
                </td>
                <td className='truncate py-1.5'>{entry.name}</td>
                <td className='w-24 whitespace-nowrap py-1.5 text-right text-xs text-muted-foreground'>{entry.isDirectory ? '' : formatBytes(entry.size)}</td>
                <td className='w-36 whitespace-nowrap py-1.5 pr-3 text-right text-xs text-muted-foreground'>
                  {isRoot || entry.modifiedUtc.startsWith('0001') ? '' : formatUtcDateTime(entry.modifiedUtc)}
                </td>
              </tr>
            ))}
            {data && data.allowed && data.entries.length === 0 && !data.error && (
              <tr>
                <td colSpan={4} className='py-4 text-center text-xs text-muted-foreground'>Bu klasör boş.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      <p className='text-xs text-muted-foreground'>Klasöre çift tıklayın, dosyaya tek tık ile seçin.</p>
    </div>
  );
}
