import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { toast } from 'sonner';
import { SearchIcon, TriangleAlertIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { toApiError } from '@/lib/axios-helper';
import { useSetSignalOperatorAuthority, useSignalAuthorities, useSignalOperators } from '../../hooks/use-signal-config';
import type { SignalAuthorityDto } from '../../models/authority';
import type { SignalOperatorDto } from '../../models/operator';

/** "Kurum yok" — Base UI Select boş string'i seçimsiz sayar. */
const NONE = 'none';

/**
 * Operatörler — `/signalization/operators`.
 *
 * Operatör = AKTİF kullanıcı; ayrı bir operatör kaydı yoktur. Bu ekran yalnızca kullanıcının TEK kurumunu
 * atar (kurum rolünü verir, diğer kurum rollerini çıkarır, kurum dışı rollere dokunmaz). Kart numarası
 * çekirdek kullanıcı kaydındadır ve /admin/users'tan yazılır.
 *
 * Seçim anında kaydedilir (satır başına tek alan; ayrı bir Kaydet düğmesi yalnızca adım eklerdi).
 * Değişiklik bir sonraki kart okumasında geçerlidir — oturum/token yenilemesi gerekmez, motor rolü her kart
 * okumasında sorar.
 */
export default function SignalOperators() {
  const operators = useSignalOperators();
  const authorities = useSignalAuthorities();
  const [search, setSearch] = useState('');

  const activeAuthorities = useMemo(() => authorities.data?.filter(a => a.isActive) ?? [], [authorities.data]);

  const filtered = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('tr');
    if (!term) return operators.data ?? [];
    return (operators.data ?? []).filter(op =>
      [op.fullName, op.userName, op.identityCardId, op.authorityName].some(value => value?.toLocaleLowerCase('tr').includes(term))
    );
  }, [operators.data, search]);

  const loadError = operators.error ?? authorities.error;
  const withoutCard = operators.data?.filter(op => op.authorityId && !op.identityCardId).length ?? 0;

  return (
    <div className='flex max-w-5xl flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Operatörler</h1>
        <p className='text-sm text-muted-foreground'>
          Aktif kullanıcılar ve kurumları. Kart numarası{' '}
          <Link to='/admin/users' className='underline underline-offset-4'>
            Kullanıcılar
          </Link>{' '}
          ekranından girilir; kurum burada seçilir. Bir kullanıcının yalnızca bir kurumu olabilir.
        </p>
      </div>

      <div className='flex flex-wrap items-center gap-3'>
        <div className='relative w-full max-w-xs'>
          <SearchIcon className='pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground' />
          <Input className='pl-8' placeholder='Ad, kullanıcı adı, kart…' value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        {withoutCard > 0 && (
          <Badge variant='outline'>
            <TriangleAlertIcon />
            {withoutCard} kurumlu kullanıcının kartı yok
          </Badge>
        )}
      </div>

      {loadError && <p className='text-sm text-destructive'>{loadError.message}</p>}
      {(operators.isPending || authorities.isPending) && !loadError && <Skeleton className='h-64 w-full rounded-xl' />}

      {operators.data && authorities.data && (
        <div className='overflow-x-auto rounded-xl border'>
          <table className='w-full min-w-[40rem] text-sm'>
            <thead className='bg-muted/50 text-muted-foreground'>
              <tr className='[&>th]:px-3 [&>th]:py-2 [&>th]:text-left [&>th]:font-medium'>
                <th>Kullanıcı</th>
                <th className='w-48'>Kart</th>
                <th className='w-64'>Kurum</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(op => (
                <OperatorRow key={op.userId} operator={op} authorities={activeAuthorities} />
              ))}
            </tbody>
          </table>

          {filtered.length === 0 && (
            <p className='py-8 text-center text-sm text-muted-foreground'>{search ? 'Aramaya uyan kullanıcı yok.' : 'Aktif kullanıcı yok.'}</p>
          )}
        </div>
      )}
    </div>
  );
}

function OperatorRow({ operator, authorities }: { operator: SignalOperatorDto; authorities: SignalAuthorityDto[] }) {
  const mutation = useSetSignalOperatorAuthority();

  // Birden fazla kurum: sunucu `authorityId`'yi null döner; seçim zorunlu olarak "boş" görünür.
  const value = operator.authorityId ?? NONE;
  const selectedLabel = operator.hasMultipleAuthorities
    ? 'Birden fazla — birini seçin'
    : (authorities.find(a => a.id === operator.authorityId)?.name ?? 'Kurum yok');

  return (
    <tr className='border-t [&>td]:px-3 [&>td]:py-2'>
      <td>
        <div className='font-medium'>{operator.fullName}</div>
        <div className='text-xs text-muted-foreground'>{operator.userName ?? '—'}</div>
      </td>
      <td>
        {operator.identityCardId ? (
          <span className='font-mono text-xs'>{operator.identityCardId}</span>
        ) : (
          <Badge variant='outline'>kart yok</Badge>
        )}
      </td>
      <td>
        <Select
          value={value}
          disabled={mutation.isPending}
          onValueChange={next => {
            const authorityId = !next || next === NONE ? null : next;
            if (authorityId === operator.authorityId && !operator.hasMultipleAuthorities) return;
            mutation.mutate(
              { userId: operator.userId, authorityId },
              // Satır başına tek alan: form yok, hata tek satırlık bildirim olarak gösterilir.
              { onError: error => toast.error(toApiError(error).message) }
            );
          }}>
          <SelectTrigger className='w-full' aria-label={`${operator.fullName} kurumu`}>
            <SelectValue>{mutation.isPending ? 'Kaydediliyor…' : selectedLabel}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={NONE}>Kurum yok</SelectItem>
            {authorities.map(authority => (
              <SelectItem key={authority.id} value={authority.id}>
                {authority.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {operator.hasMultipleAuthorities && (
          <p className='mt-1 flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400'>
            <TriangleAlertIcon className='size-3.5 shrink-0' />
            {operator.authorityName} — bu durumda kartla kapı açılmaz.
          </p>
        )}
      </td>
    </tr>
  );
}
