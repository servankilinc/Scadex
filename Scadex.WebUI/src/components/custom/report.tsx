import { useState, type ReactNode } from 'react';
import { toast } from 'sonner';
import { DownloadIcon, LoaderCircleIcon, RotateCcwIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { toApiError } from '@/lib/axios-helper';
import { REPORT_ALL } from '@/lib/report-params';
import { cn } from '@/lib/utils';

/**
 * Rapor ekranlarının ortak iskeleti (Sinyalizasyon raporu, İzleme raporu). Sıra her raporda aynıdır: başlık + dışa aktar →
 * filtre şeridi → özet kartları → sekmeler → `DataTable` + alt şerit. Tablo da her yerde aynı bileşendir
 * (`@/components/custom/data-table`) ve sayfanın tam genişliğini alır — bir rapor kendi tablo görünümünü icat etmesin.
 */
export function ReportPage({ children }: { children: ReactNode }) {
  return <div className='flex flex-col gap-4 p-4'>{children}</div>;
}

export function ReportHeader({ title, description, actions }: { title: string; description: ReactNode; actions?: ReactNode }) {
  return (
    <div className='flex flex-wrap items-start justify-between gap-3'>
      <div className='min-w-0'>
        <h1 className='text-lg font-semibold'>{title}</h1>
        <p className='text-sm text-muted-foreground'>{description}</p>
      </div>
      {actions && <div className='flex shrink-0 items-center gap-2'>{actions}</div>}
    </div>
  );
}

/** Filtre alanları + "Filtreyi temizle". Alanlar aynı genişlikte: dar ekranda tam satır, genişte sabit. */
export function ReportFilterBar({ children, isFiltered, onReset }: { children: ReactNode; isFiltered: boolean; onReset: () => void }) {
  return (
    <div className='flex flex-wrap items-end gap-3'>
      {children}
      {isFiltered && (
        <Button size='sm' variant='ghost' onClick={onReset}>
          <RotateCcwIcon />
          Filtreyi temizle
        </Button>
      )}
    </div>
  );
}

const FIELD_CLASS = 'w-full sm:w-56';

export interface ReportFilterOption {
  value: string;
  label: string;
}

/** "Hepsi" seçenekli tek seçim. Değer `REPORT_ALL` iken `allLabel` görünür. */
export function ReportSelectFilter({
  id,
  label,
  value,
  onChange,
  allLabel,
  options,
  disabled
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  allLabel: string;
  options: ReportFilterOption[];
  disabled?: boolean;
}) {
  // Etiket açıkça verilir: çocuğu olmayan `SelectValue` ham değeri (Guid) basar.
  const selectedLabel = value === REPORT_ALL ? allLabel : (options.find(o => o.value === value)?.label ?? 'Seçili kayıt');

  return (
    <Field className={FIELD_CLASS}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Select value={value} onValueChange={next => onChange(next ?? REPORT_ALL)}>
        <SelectTrigger id={id} className='w-full' disabled={disabled}>
          <SelectValue>{selectedLabel}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={REPORT_ALL}>{allLabel}</SelectItem>
          {options.map(option => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}

/** Yerel tarih-saat girilir; sunucuya UTC gider (`localToUtcIso`). */
export function ReportDateFilter({ id, label, value, onChange }: { id: string; label: string; value: string; onChange: (value: string) => void }) {
  return (
    <Field className={FIELD_CLASS}>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Input id={id} type='datetime-local' value={value} onChange={e => onChange(e.target.value)} />
    </Field>
  );
}

export function ReportStatGrid({ children }: { children: ReactNode }) {
  return <div className='grid gap-3 sm:grid-cols-2 xl:grid-cols-4'>{children}</div>;
}

export function ReportStat({ title, value, detail, tone }: { title: string; value: string; detail?: string; tone?: 'warning' }) {
  return (
    <Card size='sm'>
      <CardContent className='flex flex-col gap-1'>
        <span className='text-xs text-muted-foreground'>{title}</span>
        <span className={cn('truncate text-lg font-semibold tabular-nums', tone === 'warning' && 'text-amber-600 dark:text-amber-400')}>{value}</span>
        {detail && <span className='truncate text-xs text-muted-foreground'>{detail}</span>}
      </CardContent>
    </Card>
  );
}

/**
 * "Dışa aktar" düğmesi. `onExport` veriyi toplayıp dosyayı indirtir; düğme o sürede meşguldür, hata toast olarak gösterilir.
 * Ne aktarıldığını (etkin sekme, filtreye uyan tüm kayıtlar) `title` söyler.
 */
export function ReportExportButton({ onExport, disabled, title }: { onExport: () => Promise<void> | void; disabled?: boolean; title: string }) {
  const [isExporting, setIsExporting] = useState(false);

  const handleClick = async () => {
    setIsExporting(true);
    try {
      await onExport();
    } catch (error) {
      toast.error(toApiError(error).message || 'Rapor dışa aktarılamadı.');
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <Button size='sm' variant='outline' disabled={disabled || isExporting} onClick={handleClick} title={title}>
      {isExporting ? <LoaderCircleIcon className='animate-spin' /> : <DownloadIcon />}
      {isExporting ? 'Hazırlanıyor…' : 'Dışa aktar (CSV)'}
    </Button>
  );
}
