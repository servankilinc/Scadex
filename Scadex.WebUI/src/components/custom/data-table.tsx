import type { ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';

/** Tek bir kolonun tanımı. `className` hem başlığa hem hücreye uygulanır (genişlik, hizalama, kırpma). */
export interface DataTableColumn<T> {
  id: string;
  header: ReactNode;
  cell: (row: T) => ReactNode;
  className?: string;
}

interface DataTableProps<T> {
  columns: DataTableColumn<T>[];
  /** `undefined` = veri henüz yok; `isLoading` ile birlikte iskelet satırları çizilir. */
  rows: T[] | undefined;
  getRowKey: (row: T) => string;
  isLoading?: boolean;
  /** Satır yokken tablonun içinde gösterilen metin — "henüz yok" ile "aramaya uyan yok" ayrımını sayfa yapar. */
  emptyMessage: ReactNode;
  /** Satırın düğmeleri. Verilirse HER ZAMAN son kolonda, sağa yaslı çizilir. */
  actions?: (row: T) => ReactNode;
  /**
   * Satır soluk mu (pasif kayıt). Pasif kayıtlar listede GÖRÜNÜR — `IsActive` üzerinde global filtre yok,
   * geri alınabilsinler diye. `opacity` değil yalnızca metin rengi: düğmeler devre dışı gibi görünmesin.
   */
  isRowMuted?: (row: T) => boolean;
  skeletonRows?: number;
}

/**
 * Liste ekranlarının (kabinler, kameralar, firmalar, kullanıcılar, roller, şablonlar) ortak tablosu.
 *
 * Bilerek ince: sıralama/sayfalama/filtre YOK — bu listeler küçük ve tamamı tek istekte geliyor; arama
 * gereken ekran satırları kendisi süzüp verir. Hata metni de sayfanın işidir, tablo yalnızca
 * yükleniyor / boş / dolu hâllerini çizer.
 */
export function DataTable<T>({
  columns,
  rows,
  getRowKey,
  isLoading = false,
  emptyMessage,
  actions,
  isRowMuted,
  skeletonRows = 5
}: DataTableProps<T>) {
  const columnCount = columns.length + (actions ? 1 : 0);

  return (
    <div className='overflow-hidden rounded-xl bg-card ring-1 ring-foreground/10'>
      <Table>
        <TableHeader className='bg-muted/50'>
          <TableRow className='hover:bg-transparent'>
            {columns.map(column => (
              <TableHead key={column.id} className={cn('px-3 text-xs text-muted-foreground', column.className)}>
                {column.header}
              </TableHead>
            ))}
            {/* `w-px`: kolon düğmeleri kadar daralır, kalan genişlik veri kolonlarına kalır. */}
            {actions && <TableHead className='w-px px-3 text-right text-xs text-muted-foreground'>İşlemler</TableHead>}
          </TableRow>
        </TableHeader>

        <TableBody>
          {isLoading &&
            Array.from({ length: skeletonRows }, (_, i) => (
              <TableRow key={i} className='hover:bg-transparent'>
                {Array.from({ length: columnCount }, (_, j) => (
                  <TableCell key={j} className='px-3 py-3'>
                    <Skeleton className='h-4 w-full' />
                  </TableCell>
                ))}
              </TableRow>
            ))}

          {!isLoading && rows?.length === 0 && (
            <TableRow className='hover:bg-transparent'>
              <TableCell colSpan={columnCount} className='py-8 text-center whitespace-normal text-muted-foreground'>
                {emptyMessage}
              </TableCell>
            </TableRow>
          )}

          {!isLoading &&
            rows?.map(row => (
              <TableRow key={getRowKey(row)} className={cn(isRowMuted?.(row) && 'text-muted-foreground')}>
                {columns.map(column => (
                  <TableCell key={column.id} className={cn('px-3', column.className)}>
                    {column.cell(row)}
                  </TableCell>
                ))}
                {actions && (
                  <TableCell className='px-3'>
                    <div className='flex items-center justify-end gap-2'>{actions(row)}</div>
                  </TableCell>
                )}
              </TableRow>
            ))}
        </TableBody>
      </Table>
    </div>
  );
}

/** Aktif/pasif kolonunun rozeti. */
export function ActiveBadge({ isActive }: { isActive: boolean }) {
  return <Badge variant={isActive ? 'outline' : 'secondary'}>{isActive ? 'Aktif' : 'Pasif'}</Badge>;
}
