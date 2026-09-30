import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { EyeIcon, MonitorIcon, SearchIcon, TriangleAlertIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { usePcs } from '../hooks/use-pcs';
import type { PcListItemDto } from '../models/pc';

/**
 * PC listesi — `/remote-desk/pcs`.
 *
 * Aktif ve tipi "Bilgisayar" (`DeviceType.Pc`) olan cihazlar. PC eklemek için ayrı bir ekran YOK: cihaz kabin diyagramına
 * çizilir, MAC adresi girilir; PC'deki Windows istemcisi bu adresle kendiliğinden eşlenir (RemoteDesk.md § 5).
 */
export default function RemoteDeskPcs() {
  const pcs = usePcs();
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('tr');
    if (!term) return pcs.data ?? [];
    return (pcs.data ?? []).filter(pc => [pc.deviceName, pc.cabinetName, pc.macAddress].some(v => v?.toLocaleLowerCase('tr').includes(term)));
  }, [pcs.data, search]);

  const connected = pcs.data?.filter(pc => pc.isConnected).length ?? 0;
  const withoutMac = pcs.data?.filter(pc => !pc.macAddress).length ?? 0;

  return (
    <div className='flex max-w-6xl flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>PC Ekranları</h1>
        <p className='text-sm text-muted-foreground'>
          Kabin diyagramlarındaki bilgisayarlar. İzlemek için PC'de Scadex RemoteDesk istemcisi çalışmalı ve cihazın MAC adresi
          diyagramda girilmiş olmalı. İzlenen PC'nin ekranında bunu belirten bir uyarı görünür.
        </p>
      </div>

      <div className='flex flex-wrap items-center gap-3'>
        <div className='relative w-full max-w-xs'>
          <SearchIcon className='pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground' />
          <Input className='pl-8' placeholder='PC, kabin, MAC…' value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        {pcs.data && (
          <Badge variant='outline'>
            {connected} / {pcs.data.length} bağlı
          </Badge>
        )}
        {withoutMac > 0 && (
          <Badge variant='outline'>
            <TriangleAlertIcon />
            {withoutMac} PC'nin MAC adresi yok
          </Badge>
        )}
      </div>

      {pcs.error && <p className='text-sm text-destructive'>{pcs.error.message}</p>}
      {pcs.isPending && !pcs.error && <Skeleton className='h-64 w-full rounded-xl' />}

      {pcs.data && (
        <div className='scrollbar-thin overflow-x-auto rounded-xl border'>
          <table className='w-full min-w-[48rem] text-sm'>
            <thead className='bg-muted/50 text-muted-foreground'>
              <tr className='[&>th]:px-3 [&>th]:py-2 [&>th]:text-left [&>th]:font-medium'>
                <th>PC</th>
                <th className='w-44'>MAC</th>
                <th className='w-32'>Durum</th>
                <th className='w-24'>Monitör</th>
                <th className='w-24'>İzleyen</th>
                <th className='w-28' />
              </tr>
            </thead>
            <tbody>
              {filtered.map(pc => (
                <PcRow key={pc.deviceId} pc={pc} />
              ))}
            </tbody>
          </table>

          {filtered.length === 0 && (
            <p className='py-8 text-center text-sm text-muted-foreground'>
              {search ? 'Aramaya uyan PC yok.' : 'Diyagramlarda "Bilgisayar" tipinde cihaz yok.'}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function PcRow({ pc }: { pc: PcListItemDto }) {
  return (
    <tr className='border-t [&>td]:px-3 [&>td]:py-2'>
      <td>
        <div className='flex items-center gap-2 font-medium'>
          <MonitorIcon className='size-4 text-muted-foreground' />
          {pc.deviceName}
        </div>
        <div className='text-xs text-muted-foreground'>{pc.cabinetName}</div>
      </td>
      <td>{pc.macAddress ? <span className='font-mono text-xs'>{pc.macAddress}</span> : <Badge variant='outline'>MAC tanımlı değil</Badge>}</td>
      <td>
        {pc.isConnected ? (
          <Badge variant='secondary' title={pc.clientVersion ? `İstemci sürümü ${pc.clientVersion}` : undefined}>
            <span className='size-2 rounded-full bg-emerald-500' />
            Bağlı
          </Badge>
        ) : (
          <Badge variant='outline'>
            <span className='size-2 rounded-full bg-muted-foreground/50' />
            Bağlı değil
          </Badge>
        )}
      </td>
      <td>{pc.isConnected ? pc.monitorCount : '—'}</td>
      <td>
        {pc.viewerCount > 0 ? (
          <Badge variant='destructive'>
            <EyeIcon />
            {pc.viewerCount}
          </Badge>
        ) : (
          <span className='text-muted-foreground'>0</span>
        )}
      </td>
      <td className='text-right'>
        <Button size='sm' variant={pc.isConnected ? 'default' : 'outline'} nativeButton={false} render={<Link to={`/remote-desk/pcs/${pc.deviceId}`} />}>
          {pc.isConnected ? 'İzle' : 'Detay'}
        </Button>
      </td>
    </tr>
  );
}
