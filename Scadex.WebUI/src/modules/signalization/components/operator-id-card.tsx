import { IdCardIcon } from 'lucide-react';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { cn } from '@/lib/utils';
import defaultAvatar from '@/assets/avatar.png';
import { formatUtcTime } from '../lib';
import type { OperatorSessionOperatorDto } from '../models/session';

interface OperatorIdCardProps {
  operator: OperatorSessionOperatorDto;
  /** Oturum sürüyor mu — kartta "sahada" işareti ve yeşil şerit. */
  isLive?: boolean;
  /** Harita paneli gibi dar kolonlar için sıkışık düzen (küçük avatar, künye ikonu yok). */
  compact?: boolean;
}

/**
 * Operatör kimlik kartı — bir işlem oturumunda kart okutan yetkilinin künyesi.
 *
 * Gösterilen ad ve kurum oturum anındaki ENSTANTANEDİR (`FullNameSnapshot` / `AuthorityNameSnapshot`);
 * kullanıcının bugünkü adı değişmiş olabilir, kart bilerek eski hâli gösterir.
 *
 * Avatar sabittir: sistemde kullanıcı fotoğrafı tutulmaz, bu yüzden tek bir yerel görsel kullanılır ve
 * yüklenemezse ad baş harflerine düşülür.
 */
export function OperatorIdCard({ operator, isLive = false, compact = false }: OperatorIdCardProps) {
  return (
    <article
      className={cn(
        'relative overflow-hidden rounded-xl border bg-card',
        isLive ? 'border-emerald-500/50' : 'border-border'
      )}
    >
      {/* Sol şerit: karta rozet/künye hissi verir; sahadaki operatörde yeşile döner. */}
      <span aria-hidden className={cn('absolute inset-y-0 left-0 w-1', isLive ? 'bg-emerald-500' : 'bg-primary/60')} />

      {!compact && <IdCardIcon aria-hidden className='pointer-events-none absolute top-2 right-2 size-8 text-muted-foreground/10' />}

      <div className='flex items-center gap-3 p-3 pl-4'>
        <Avatar size={compact ? 'default' : 'lg'} className='shrink-0'>
          <AvatarImage src={defaultAvatar} alt='' />
          <AvatarFallback>{initialsOf(operator.fullName)}</AvatarFallback>
        </Avatar>

        <div className='min-w-0 flex-1'>
          <div className='truncate text-sm font-semibold tracking-wide uppercase' title={operator.fullName}>
            {operator.fullName || 'Tanımsız operatör'}
          </div>
          <div className='truncate text-xs text-muted-foreground' title={operator.authorityName}>
            {operator.authorityName || 'kurum atanmamış'}
          </div>
        </div>

        {isLive && (
          <span className='flex shrink-0 items-center gap-1.5 text-[10px] font-medium tracking-wider text-emerald-600 uppercase dark:text-emerald-400'>
            <span className='relative flex size-2'>
              <span className='absolute inline-flex size-full animate-ping rounded-full bg-emerald-500/60' />
              <span className='relative inline-flex size-2 rounded-full bg-emerald-500' />
            </span>
            sahada
          </span>
        )}
      </div>

      {/* Künye alanları: dar kolonda alt alta sarar, genişte tek satırda üç sütun olur. */}
      <dl className='flex flex-wrap gap-x-4 gap-y-1.5 border-t border-dashed px-3 py-2 pl-4'>
        <IdField label='Kart No' value={operator.cardIdRaw} mono />
        <IdField label='İlk okutma' value={formatUtcTime(operator.firstCardAtUtc)} />
        <IdField label='Son okutma' value={formatUtcTime(operator.lastCardAtUtc)} />
      </dl>
    </article>
  );
}

function IdField({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className='min-w-0 flex-1 basis-24'>
      <dt className='text-[10px] tracking-wider text-muted-foreground uppercase'>{label}</dt>
      <dd className={cn('truncate text-xs font-medium', mono ? 'font-mono' : 'tabular-nums')} title={value}>
        {value || '—'}
      </dd>
    </div>
  );
}

/** Avatar görseli yüklenemezse gösterilen baş harfler — en çok iki kelime. */
function initialsOf(fullName: string): string {
  return (
    fullName
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map(part => part[0]?.toUpperCase() ?? '')
      .join('') || '?'
  );
}
