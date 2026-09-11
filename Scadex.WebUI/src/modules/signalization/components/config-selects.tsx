import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { SignalAuthorityDto } from '../models/authority';
import type { SignalCameraOptionDto, SignalChannelOptionDto } from '../models/cabinet';

/** "Seçim yok" — Base UI Select boş string'i seçimsiz sayar; formda `''` olarak tutulur. */
const NONE = 'none';

/** Kanal → onu formda kullanan alanlar. Etiket, aynı kanalı başka bir kapının da seçtiğini göstermek için. */
export type ChannelUsage = Map<string, { path: string; label: string }[]>;

interface ChannelSelectProps {
  id: string;
  /** Formdaki alan yolu — kullanım etiketinde alanın KENDİSİ "başkası" sayılmasın diye. */
  path: string;
  channels: SignalChannelOptionDto[];
  usage: ChannelUsage;
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  /** Seçimsiz bırakılabilir mi (kabin sireni). */
  allowNone?: boolean;
  invalid?: boolean;
}

/**
 * Kanal seçici. Kapı sanaldır: seçilen şey kartın bir KANALIDIR (`IN3`, `OUT1`), cihaz değil. Etiket
 * "adres — pine kabloyla bağlı saha cihazı (yoksa kanal adı) · kart" biçimindedir; anlamı taşıyan adrestir.
 *
 * Kullanımdaki kanal devre dışı BIRAKILMAZ (iki kapı arasında kanal değiş tokuşu ara adım gerektirirdi);
 * yalnızca işaretlenir, çakışmayı form doğrulaması ve sunucu reddeder.
 */
export function ChannelSelect({ id, path, channels, usage, value, onChange, placeholder, allowNone, invalid }: ChannelSelectProps) {
  const selected = channels.find(channel => channel.id === value);
  const selectedLabel = selected
    ? `${selected.address} — ${selected.wiredDeviceName ?? selected.channelName}`
    : value
      ? // Kayıtlı yapılandırma artık seçeneklerde olmayan bir kanalı gösteriyor (kart silinmiş / kanal kapatılmış).
        'Kanal bulunamadı — yeniden seçin'
      : undefined;

  return (
    <Select value={value || (allowNone ? NONE : null)} onValueChange={next => onChange(!next || next === NONE ? '' : next)}>
      <SelectTrigger id={id} className='w-full' aria-invalid={invalid || undefined}>
        <SelectValue placeholder={placeholder}>{value ? selectedLabel : allowNone ? 'Yok' : undefined}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {allowNone && <SelectItem value={NONE}>Yok</SelectItem>}
        {channels.map(channel => {
          const others = (usage.get(channel.id) ?? []).filter(owner => owner.path !== path).map(owner => owner.label);
          return (
            <SelectItem key={channel.id} value={channel.id}>
              <span className='font-mono text-xs'>{channel.address}</span>
              <span>{channel.wiredDeviceName ?? channel.channelName}</span>
              {channel.deviceName && <span className='text-xs text-muted-foreground'>{channel.deviceName}</span>}
              {channel.currentValue != null && <span className='font-mono text-xs text-muted-foreground'>= {channel.currentValue}</span>}
              {others.length > 0 && <span className='text-xs text-amber-600 dark:text-amber-400'>· {others.join(', ')}</span>}
            </SelectItem>
          );
        })}
        {channels.length === 0 && (
          <div className='px-2 py-1.5 text-xs text-muted-foreground'>Bu kabinde uygun yönde kanal yok — önce diyagramda kartı ekleyin.</div>
        )}
      </SelectContent>
    </Select>
  );
}

export function AuthoritySelect({
  id,
  authorities,
  value,
  onChange,
  invalid
}: {
  id: string;
  authorities: SignalAuthorityDto[];
  value: string;
  onChange: (value: string) => void;
  invalid?: boolean;
}) {
  const selected = authorities.find(authority => authority.id === value);

  return (
    <Select value={value || null} onValueChange={next => onChange(next ?? '')}>
      <SelectTrigger id={id} className='w-full' aria-invalid={invalid || undefined}>
        <SelectValue placeholder='Kurum seçin'>{selected ? selected.name : value ? 'Pasif/silinmiş kurum — yeniden seçin' : undefined}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {authorities.map(authority => (
          <SelectItem key={authority.id} value={authority.id}>
            {authority.name}
            {!authority.roleIsActive && <span className='text-xs text-amber-600 dark:text-amber-400'>rol pasif</span>}
          </SelectItem>
        ))}
        {authorities.length === 0 && <div className='px-2 py-1.5 text-xs text-muted-foreground'>Aktif kurum yok — önce Kurumlar ekranından ekleyin.</div>}
      </SelectContent>
    </Select>
  );
}

export function CameraSelect({
  id,
  cameras,
  value,
  onChange,
  invalid
}: {
  id: string;
  cameras: SignalCameraOptionDto[];
  value: string;
  onChange: (value: string) => void;
  invalid?: boolean;
}) {
  const selected = cameras.find(camera => camera.id === value);

  return (
    <Select value={value || NONE} onValueChange={next => onChange(!next || next === NONE ? '' : next)}>
      <SelectTrigger id={id} className='w-full' aria-invalid={invalid || undefined}>
        <SelectValue>{value ? (selected ? `${selected.name}${selected.isActive ? '' : ' (pasif)'}` : 'Kamera bulunamadı') : 'Kamera yok'}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={NONE}>Kamera yok</SelectItem>
        {cameras.map(camera => (
          <SelectItem key={camera.id} value={camera.id}>
            {camera.name}
            {!camera.isActive && <span className='text-xs text-muted-foreground'>pasif</span>}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
