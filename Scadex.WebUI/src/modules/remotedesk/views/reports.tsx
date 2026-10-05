import type { ReactNode } from 'react';
import { useSearchParams } from 'react-router';
import { RotateCcwIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DataTable, DataTablePager, type DataTableColumn } from '@/components/custom/data-table';
import { formatUtcDateTime, toUtcDate } from '@/lib/utils';
import { useControlReport, useReportFilters, useReportSummary, useViewReport } from '../hooks/use-remote-access-report';
import {
  RemoteControlEndReasonLabels,
  type RemoteAccessReportQuery,
  type RemoteControlReportItemDto,
  type ReportPcOptionDto,
  type ReportUserOptionDto,
  type ScreenViewReportItemDto
} from '../models/report';

/** "Hepsi" seçeneği — Base UI Select boş string'i "seçim yok" sayar. */
const ALL = 'all';
const PAGE_SIZE = 25;

type ReportTab = 'views' | 'controls';

/**
 * PC izleme raporu — `/remote-desk/reports` (`RemotePcAudit`).
 *
 * Kim, hangi PC'nin hangi monitörünü ne zaman izledi ve kim hangi PC'yi ne kadar kontrol etti. Salt okunur ve canlı DEĞİL:
 * denetim satırlarını yalnızca izleme/kontrol akışı yazar, ekran yoklama yapmaz. Fare/klavye girdisi gösterilmez (sunucu
 * saklamıyor, sayısı da raporda istenmedi).
 *
 * Filtreler, sekme ve sayfa URL'dedir: PC detayındaki "Kayıtlar" bağlantısı `?deviceId=` ile süzülü açar, yenileme filtreyi korur.
 * Tarih aralığı kaydın BAŞLANGIÇ anına uygulanır.
 */
export default function RemoteDeskReports() {
  const [searchParams, setSearchParams] = useSearchParams();
  const deviceId = searchParams.get('deviceId') ?? ALL;
  const userId = searchParams.get('userId') ?? ALL;
  const from = searchParams.get('from') ?? '';
  const to = searchParams.get('to') ?? '';
  const tab: ReportTab = searchParams.get('tab') === 'controls' ? 'controls' : 'views';
  const page = Math.max(Number(searchParams.get('page')) || 1, 1);

  /** Filtre değişince sayfa başa döner (sayfa açıkça verilmedikçe). `replace`: filtre oynamak geçmişi doldurmasın. */
  const update = (patch: Record<string, string | null>) =>
    setSearchParams(
      current => {
        const next = new URLSearchParams(current);
        if (!('page' in patch)) next.delete('page');
        for (const [key, value] of Object.entries(patch)) {
          if (value == null || value === '' || value === ALL) next.delete(key);
          else next.set(key, value);
        }
        return next;
      },
      { replace: true }
    );

  const query: RemoteAccessReportQuery = {
    deviceId: deviceId === ALL ? null : deviceId,
    userId: userId === ALL ? null : userId,
    fromUtc: localToUtcIso(from),
    toUtc: localToUtcIso(to),
    page,
    pageSize: PAGE_SIZE
  };

  const filters = useReportFilters();
  const summary = useReportSummary(query);
  const views = useViewReport(query, tab === 'views');
  const controls = useControlReport(query, tab === 'controls');

  const isFiltered = deviceId !== ALL || userId !== ALL || from !== '' || to !== '';
  const listError = (tab === 'views' ? views.error : controls.error) ?? summary.error ?? filters.error;

  const pcs = filters.data?.pcs ?? [];
  const users = filters.data?.users ?? [];

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>İzleme Raporu</h1>
        <p className='text-sm text-muted-foreground'>
          PC ekranlarını kimin, ne zaman izlediği ve uzaktan kontrol ettiği. Kayıtlar izleme sırasında otomatik yazılır; buradan değiştirilemez.
        </p>
      </div>

      <div className='flex flex-wrap items-end gap-3'>
        <Field className='w-full max-w-xs'>
          <FieldLabel htmlFor='report-pc'>PC</FieldLabel>
          {/* Etiket açıkça verilir: çocuğu olmayan `SelectValue` ham Guid'i basar. */}
          <Select value={deviceId} onValueChange={value => update({ deviceId: value })}>
            <SelectTrigger id='report-pc'>
              <SelectValue>{deviceId === ALL ? 'Tüm PC\'ler' : pcOptionLabel(pcs.find(pc => pc.deviceId === deviceId))}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>Tüm PC'ler</SelectItem>
              {pcs.map(pc => (
                <SelectItem key={pc.deviceId} value={pc.deviceId}>
                  {pcOptionLabel(pc)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field className='w-full max-w-xs'>
          <FieldLabel htmlFor='report-user'>Kullanıcı</FieldLabel>
          <Select value={userId} onValueChange={value => update({ userId: value })}>
            <SelectTrigger id='report-user'>
              <SelectValue>{userId === ALL ? 'Tüm kullanıcılar' : userOptionLabel(users.find(user => user.userId === userId))}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>Tüm kullanıcılar</SelectItem>
              {users.map(user => (
                <SelectItem key={user.userId} value={user.userId}>
                  {userOptionLabel(user)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field className='w-full max-w-[13rem]'>
          <FieldLabel htmlFor='report-from'>Başlangıç</FieldLabel>
          {/* Yerel saat girilir, sunucuya UTC gider. */}
          <Input id='report-from' type='datetime-local' value={from} onChange={e => update({ from: e.target.value })} />
        </Field>

        <Field className='w-full max-w-[13rem]'>
          <FieldLabel htmlFor='report-to'>Bitiş</FieldLabel>
          <Input id='report-to' type='datetime-local' value={to} onChange={e => update({ to: e.target.value })} />
        </Field>

        {isFiltered && (
          <Button size='sm' variant='ghost' onClick={() => update({ deviceId: null, userId: null, from: null, to: null })}>
            <RotateCcwIcon />
            Filtreyi temizle
          </Button>
        )}
      </div>

      {listError && <p className='text-sm text-destructive'>{listError.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 xl:grid-cols-4'>
        {summary.data ? (
          <>
            <StatCard title='İzleme' value={formatDuration(summary.data.viewSeconds)} detail={`${summary.data.viewCount} oturum`} />
            <StatCard title='Uzaktan kontrol' value={formatDuration(summary.data.controlSeconds)} detail={`${summary.data.controlCount} oturum`} />
            <StatCard
              title='En çok izlenen PC'
              value={summary.data.topPc ? (summary.data.topPc.deviceName ?? 'Silinmiş PC') : '—'}
              detail={
                summary.data.topPc
                  ? [summary.data.topPc.cabinetName, formatDuration(summary.data.topPc.seconds)].filter(Boolean).join(' · ')
                  : 'Kayıt yok'
              }
            />
            <StatCard
              title='En çok izleyen'
              value={summary.data.topUser ? (summary.data.topUser.fullName ?? 'Bilinmeyen kullanıcı') : '—'}
              detail={summary.data.topUser ? formatDuration(summary.data.topUser.seconds) : 'Kayıt yok'}
            />
          </>
        ) : (
          Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className='h-24 w-full rounded-xl' />)
        )}
      </div>

      <Tabs value={tab} onValueChange={value => update({ tab: value === 'controls' ? 'controls' : null })}>
        <TabsList>
          <TabsTrigger value='views'>İzleme kayıtları</TabsTrigger>
          <TabsTrigger value='controls'>Kontrol kayıtları</TabsTrigger>
        </TabsList>

        <TabsContent value='views' className='flex flex-col gap-3'>
          <DataTable
            columns={VIEW_COLUMNS}
            rows={views.data?.data}
            getRowKey={row => String(row.id)}
            isLoading={views.isPending}
            emptyMessage={isFiltered ? 'Bu filtreye uyan izleme kaydı yok.' : 'Henüz izleme kaydı yok.'}
          />
          {views.data && <DataTablePager pagination={views.data} onPageChange={next => update({ page: String(next) })} />}
        </TabsContent>

        <TabsContent value='controls' className='flex flex-col gap-3'>
          <DataTable
            columns={CONTROL_COLUMNS}
            rows={controls.data?.data}
            getRowKey={row => row.id}
            isLoading={controls.isPending}
            emptyMessage={isFiltered ? 'Bu filtreye uyan kontrol kaydı yok.' : 'Henüz uzaktan kontrol kaydı yok.'}
          />
          {controls.data && <DataTablePager pagination={controls.data} onPageChange={next => update({ page: String(next) })} />}
        </TabsContent>
      </Tabs>
    </div>
  );
}

function StatCard({ title, value, detail }: { title: string; value: string; detail: string }) {
  return (
    <Card size='sm'>
      <CardContent className='flex flex-col gap-1'>
        <span className='text-xs text-muted-foreground'>{title}</span>
        <span className='truncate text-lg font-semibold'>{value}</span>
        <span className='truncate text-xs text-muted-foreground'>{detail}</span>
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── kolonlar

/** İki listenin ortak kolonları: başlangıç, bitiş, süre, kullanıcı, PC. */
function commonColumns<T extends ScreenViewReportItemDto | RemoteControlReportItemDto>(): DataTableColumn<T>[] {
  return [
    { id: 'started', header: 'Başlangıç', className: 'font-mono text-xs', cell: row => formatUtcDateTime(row.startedUtc) },
    {
      id: 'ended',
      header: 'Bitiş',
      className: 'font-mono text-xs',
      cell: row => (row.endedUtc ? formatUtcDateTime(row.endedUtc) : <Badge variant='secondary' className='font-sans'>Sürüyor</Badge>)
    },
    // Açık kayıtta süre yok: "şimdi" render'da okunmaz (saf olmayan değer); özet kartı açık kaydı sunucuda zaten sayar.
    { id: 'duration', header: 'Süre', className: 'tabular-nums', cell: row => (row.endedUtc ? formatDuration(secondsBetween(row.startedUtc, row.endedUtc)) : '—') },
    { id: 'user', header: 'Kullanıcı', cell: row => <NameCell name={row.userFullName} detail={row.userName} missing='bilinmeyen kullanıcı' /> },
    { id: 'pc', header: 'PC', cell: row => <NameCell name={row.deviceName} detail={row.cabinetName} missing='silinmiş PC' /> }
  ];
}

const VIEW_COLUMNS: DataTableColumn<ScreenViewReportItemDto>[] = [
  ...commonColumns<ScreenViewReportItemDto>(),
  { id: 'monitor', header: 'Monitör', cell: row => `Monitör ${row.monitorIndex}` }
];

const CONTROL_COLUMNS: DataTableColumn<RemoteControlReportItemDto>[] = [
  ...commonColumns<RemoteControlReportItemDto>(),
  { id: 'reason', header: 'Bitiş nedeni', cell: row => (row.endReason ? RemoteControlEndReasonLabels[row.endReason] : '—') }
];

/** Ad + altında ikincil bilgi. Ad `null` = kayıt çekirdekte artık yok; denetim satırı yine de gösterilir. */
function NameCell({ name, detail, missing }: { name: string | null; detail: string | null; missing: string }): ReactNode {
  return (
    <div className='flex flex-col'>
      {name ? <span>{name}</span> : <span className='text-muted-foreground italic'>{missing}</span>}
      {detail && <span className='text-xs text-muted-foreground'>{detail}</span>}
    </div>
  );
}

// ─────────────────────────────────────────────────────────── yardımcılar

function pcOptionLabel(pc: ReportPcOptionDto | undefined): string {
  if (!pc) return 'Seçili PC';
  const name = pc.deviceName ?? 'Silinmiş PC';
  return pc.cabinetName ? `${name} · ${pc.cabinetName}` : name;
}

function userOptionLabel(user: ReportUserOptionDto | undefined): string {
  if (!user) return 'Seçili kullanıcı';
  const name = user.fullName ?? 'Bilinmeyen kullanıcı';
  return user.userName ? `${name} (${user.userName})` : name;
}

/** İki sunucu damgası arasındaki saniye (damgalarda `Z` yok — `toUtcDate`). */
function secondsBetween(startedUtc: string, endedUtc: string): number {
  const start = toUtcDate(startedUtc)?.getTime() ?? 0;
  const end = toUtcDate(endedUtc)?.getTime() ?? 0;
  return Math.max(Math.round((end - start) / 1000), 0);
}

/** `3725` → "1 sa 2 dk", `95` → "1 dk 35 sn", `12` → "12 sn". */
function formatDuration(totalSeconds: number): string {
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  if (hours > 0) return `${hours} sa ${minutes} dk`;
  if (minutes > 0) return `${minutes} dk ${seconds} sn`;
  return `${seconds} sn`;
}

/** `datetime-local` girdisi (yerel saat) → sunucunun beklediği UTC ISO. Boşsa filtre yok. */
function localToUtcIso(local: string): string | null {
  if (!local) return null;
  const parsed = new Date(local);
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString();
}
