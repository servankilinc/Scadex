import { useState } from 'react';
import { tr } from 'react-day-picker/locale';
import { CalendarIcon, XIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

/**
 * Tarih + saat seçici (shadcn Calendar + Popover). Değer sözleşmesi tarayıcının `datetime-local` girdisiyle AYNIDIR:
 * `"YYYY-MM-DDTHH:mm"` yerel saat, `""` = seçim yok — çağıranlar UTC'ye kendi dönüştürür (`localToUtcIso`).
 *
 * Gün seçilince saat korunur; henüz değer yoksa `defaultTime` kullanılır (aralığın sonu için `23:59` verin ki seçilen
 * gün sonuca dahil olsun). Tarih seçilmeden saat değiştirilirse bugün varsayılır.
 */
export function DateTimePicker({
  id,
  value,
  onChange,
  defaultTime = '00:00',
  placeholder = 'Tarih seçin',
  disabled,
  className
}: {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  defaultTime?: string;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const parsed = parseLocalDateTime(value);
  const time = parsed?.time ?? defaultTime;

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <Button
            id={id}
            variant='outline'
            disabled={disabled}
            className={cn('w-full justify-start font-normal', !parsed && 'text-muted-foreground', className)}>
            <CalendarIcon />
            <span className='truncate'>{parsed ? formatDisplay(parsed.date, parsed.time) : placeholder}</span>
          </Button>
        }
      />
      <PopoverContent align='start' className='w-auto'>
        <Calendar
          mode='single'
          required
          locale={tr}
          selected={parsed?.date}
          defaultMonth={parsed?.date}
          onSelect={date => onChange(composeLocalDateTime(date, time))}
        />

        <div className='flex items-end gap-2 border-t px-2 pt-2.5'>
          <div className='flex flex-col gap-1.5'>
            <Label htmlFor={id ? `${id}-time` : undefined}>Saat</Label>
            {/* Takvim ikonu gizlenir: saat alanı yalnızca bölmeli giriş olarak kalır, tarayıcının açılır seçicisi çıkmaz. */}
            <Input
              id={id ? `${id}-time` : undefined}
              type='time'
              className='w-28 appearance-none [&::-webkit-calendar-picker-indicator]:hidden'
              value={time}
              onChange={e => {
                // Bölmelerden biri silinince tarayıcı `""` verir — yarım saat yazılmasın.
                if (!e.target.value) return;
                onChange(composeLocalDateTime(parsed?.date ?? new Date(), e.target.value));
              }}
            />
          </div>

          {parsed && (
            <Button
              variant='ghost'
              size='sm'
              className='ml-auto'
              onClick={() => {
                onChange('');
                setOpen(false);
              }}>
              <XIcon />
              Temizle
            </Button>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

const LOCAL_DATE_TIME = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}:\d{2})/;

function parseLocalDateTime(value: string): { date: Date; time: string } | null {
  const match = LOCAL_DATE_TIME.exec(value);
  if (!match) return null;

  const [, year, month, day, time = '00:00'] = match;
  return { date: new Date(Number(year), Number(month) - 1, Number(day)), time };
}

function composeLocalDateTime(date: Date, time: string): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${time}`;
}

function formatDisplay(date: Date, time: string): string {
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()} ${time}`;
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}
