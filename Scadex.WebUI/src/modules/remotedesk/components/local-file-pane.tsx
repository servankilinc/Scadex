import { useState } from 'react';
import { ArrowUpIcon, FileIcon, FolderIcon, FolderOpenIcon, RefreshCwIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { formatBytes } from '../lib/file-path';
import { pickDirectory, type FsDirHandle, type FsFileHandle, type LocalState } from '../lib/local-fs';

/**
 * Yerel dosya gezgini — dosya transferi penceresinin sol paneli (RemoteDesk.md § 12.8). "Klasör seç" ile seçilen kök altında gezilir;
 * aktif klasör = indirme hedefi, seçili dosya = yükleme kaynağı. File System Access API yalnızca GÜVENLİ BAĞLAMDA (HTTPS/localhost)
 * çalışır; bu dağıtım gereğidir, kodda yedek yoktur (kullanıcı kararı 2026-10-06). Tip yüzeyi `lib/local-fs.ts`'tedir.
 */

interface LocalEntry {
  name: string;
  isDirectory: boolean;
  size: number;
  handle: FsFileHandle | FsDirHandle;
}

export function LocalFilePane({ onChange, disabled }: { onChange: (state: LocalState) => void; disabled: boolean }) {
  // stack[son] = mevcut klasör; kök seçilene kadar boş.
  const [stack, setStack] = useState<FsDirHandle[]>([]);
  const [entries, setEntries] = useState<LocalEntry[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const current = stack[stack.length - 1] ?? null;

  const list = async (dir: FsDirHandle) => {
    setBusy(true);
    setError(null);
    try {
      const out: LocalEntry[] = [];
      for await (const [name, handle] of dir.entries()) {
        if (handle.kind === 'directory') out.push({ name, isDirectory: true, size: 0, handle });
        else {
          let size = 0;
          try {
            size = (await handle.getFile()).size;
          } catch {
            /* boyut okunamadı, 0 bırak */
          }
          out.push({ name, isDirectory: false, size, handle });
        }
      }
      out.sort((a, b) => (a.isDirectory === b.isDirectory ? a.name.localeCompare(b.name, 'tr') : a.isDirectory ? -1 : 1));
      setEntries(out);
    } catch {
      setError('Klasör okunamadı.');
      setEntries([]);
    } finally {
      setBusy(false);
    }
  };

  const chooseRoot = async () => {
    try {
      const dir = await pickDirectory();
      setStack([dir]);
      setSelected(null);
      onChange({ dir, selected: null });
      await list(dir);
    } catch {
      /* kullanıcı iptal etti */
    }
  };

  const enter = async (entry: LocalEntry) => {
    if (!current || entry.handle.kind !== 'directory') return;
    const next = [...stack, entry.handle];
    setStack(next);
    setSelected(null);
    onChange({ dir: entry.handle, selected: null });
    await list(entry.handle);
  };

  const up = async () => {
    if (stack.length <= 1) return;
    const next = stack.slice(0, -1);
    setStack(next);
    setSelected(null);
    const dir = next[next.length - 1]!; // guard: stack.length > 1, bu yüzden boş değil
    onChange({ dir, selected: null });
    await list(dir);
  };

  const select = (entry: LocalEntry) => {
    if (entry.handle.kind !== 'file') return;
    setSelected(entry.name);
    onChange({ dir: current, selected: { handle: entry.handle, name: entry.name } });
  };

  const breadcrumb = stack.map(d => d.name).join(' / ');

  return (
    <div className='flex min-h-0 min-w-0 flex-1 flex-col gap-2'>
      <div className='flex items-center gap-2'>
        <span className='text-sm font-medium'>Bu bilgisayar</span>
        <Button size='sm' variant='outline' disabled={disabled} onClick={() => void chooseRoot()}>
          <FolderOpenIcon />
          Klasör seç
        </Button>
        {current && (
          <>
            <Button size='icon-sm' variant='ghost' disabled={busy || disabled} onClick={() => void list(current)} title='Yenile'>
              <RefreshCwIcon className={busy ? 'animate-spin' : undefined} />
            </Button>
            <Button size='icon-sm' variant='ghost' disabled={stack.length <= 1 || busy || disabled} onClick={() => void up()} title='Üst klasör'>
              <ArrowUpIcon />
            </Button>
          </>
        )}
        <code className='min-w-0 flex-1 truncate rounded bg-muted px-2 py-1 text-xs' title={breadcrumb}>
          {breadcrumb || 'Klasör seçilmedi'}
        </code>
      </div>

      {error && <div className='text-sm text-amber-600 dark:text-amber-400'>{error}</div>}

      <div className='scrollbar-thin min-h-0 flex-1 overflow-y-auto rounded-lg border'>
        {!current ? (
          <div className='flex h-full items-center justify-center p-6 text-center text-xs text-muted-foreground'>
            Yüklenecek dosyayı seçmek ya da indirme hedefi belirlemek için bir klasör seçin.
          </div>
        ) : (
          <table className='w-full text-sm'>
            <tbody>
              {entries.map(entry => (
                <tr
                  key={entry.name}
                  className={
                    'cursor-pointer border-b last:border-0 hover:bg-muted/60' +
                    (!entry.isDirectory && entry.name === selected ? ' bg-primary/10' : '')
                  }
                  onClick={() => !disabled && select(entry)}
                  onDoubleClick={() => !disabled && void enter(entry)}>
                  <td className='w-6 py-1.5 pl-3'>
                    {entry.isDirectory ? <FolderIcon className='size-4 text-sky-500' /> : <FileIcon className='size-4 text-muted-foreground' />}
                  </td>
                  <td className='truncate py-1.5'>{entry.name}</td>
                  <td className='w-24 whitespace-nowrap py-1.5 pr-3 text-right text-xs text-muted-foreground'>{entry.isDirectory ? '' : formatBytes(entry.size)}</td>
                </tr>
              ))}
              {entries.length === 0 && !busy && (
                <tr>
                  <td colSpan={3} className='py-4 text-center text-xs text-muted-foreground'>Bu klasör boş.</td>
                </tr>
              )}
            </tbody>
          </table>
        )}
      </div>
      <p className='text-xs text-muted-foreground'>Klasöre çift tıklayın, dosyaya tek tık ile seçin. İndirilenler seçili klasöre yazılır.</p>
    </div>
  );
}
