import { useMemo, type ReactNode } from 'react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DataTable, DataTablePager, type DataTableColumn } from '@/components/custom/data-table';
import {
  ReportDateFilter,
  ReportExportButton,
  ReportFilterBar,
  ReportHeader,
  ReportPage,
  ReportSelectFilter,
  ReportStat,
  ReportStatGrid
} from '@/components/custom/report';
import { downloadCsv, exportDateStamp, fetchAllPages, type CsvColumn } from '@/lib/csv-export';
import { localToUtcIso, REPORT_ALL, useReportParams } from '@/lib/report-params';
import { formatUtcDateTime, toUtcDate } from '@/lib/utils';
import type { PaginationResponse } from '@/models/channelEvent';
import { getControlReport, getTransferReport, getViewReport } from '../api/remote-desk';
import { useControlReport, useReportFilters, useReportSummary, useTransferReport, useViewReport } from '../hooks/use-remote-access-report';
import {
  RemoteControlEndReasonLabels,
  type RemoteAccessReportQuery,
  type RemoteControlReportItemDto,
  type ReportPcOptionDto,
  type ReportUserOptionDto,
  type ScreenViewReportItemDto
} from '../models/report';
import { FileTransferDirection, type FileTransferReportItemDto } from '../models/fileTransfer';

const PAGE_SIZE = 25;
/** Dışa aktarmada sunucudan sayfa başına çekilen satır — sunucu sayfa boyutunu 100'e kırpar. */
const EXPORT_PAGE_SIZE = 100;
/** Dışa aktarma tavanı: denetim tabloları sınırsız büyür; daha fazlası için filtre daraltılmalı. */
const EXPORT_MAX_ROWS = 10_000;

type ReportTab = 'views' | 'controls' | 'transfers';

/** URL'deki filtre anahtarları — "Filtreyi temizle" bunları siler. */
const FILTER_KEYS = ['cabinetId', 'deviceId', 'userId', 'from', 'to'];

/**
 * PC izleme raporu — `/remote-desk/reports` (`RemotePcAudit`).
 *
 * Kim, hangi PC'nin hangi monitörünü ne zaman izledi, kim hangi PC'yi ne kadar kontrol etti ve hangi dosyayı aktardı. Salt okunur
 * ve canlı DEĞİL: denetim satırlarını yalnızca izleme/kontrol/transfer akışı yazar, ekran yoklama yapmaz. Fare/klavye girdisi
 * gösterilmez (sunucu saklamıyor, sayısı da raporda istenmedi).
 *
 * Filtreler, sekme ve sayfa URL'dedir (`useReportParams`): PC detayındaki "Kayıtlar" bağlantısı `?deviceId=` ile süzülü açar.
 * Tarih aralığı kaydın BAŞLANGIÇ anına uygulanır. Kabin, PC'nin BUGÜN bağlı olduğu kabindir.
 */
export default function RemoteDeskReports() {
  const params = useReportParams();
  const cabinetId = params.get('cabinetId');
  const deviceId = params.get('deviceId');
  const userId = params.get('userId');
  const from = params.get('from', '');
  const to = params.get('to', '');
  const rawTab = params.get('tab', 'views');
  const tab: ReportTab = rawTab === 'controls' || rawTab === 'transfers' ? rawTab : 'views';
  const page = Math.max(Number(params.get('page', '1')) || 1, 1);

  const query: RemoteAccessReportQuery = {
    cabinetId: cabinetId === REPORT_ALL ? null : cabinetId,
    deviceId: deviceId === REPORT_ALL ? null : deviceId,
    userId: userId === REPORT_ALL ? null : userId,
    fromUtc: localToUtcIso(from),
    toUtc: localToUtcIso(to),
    page,
    pageSize: PAGE_SIZE
  };

  const filters = useReportFilters();
  const summary = useReportSummary(query);
  const views = useViewReport(query, tab === 'views');
  const controls = useControlReport(query, tab === 'controls');
  const transfers = useTransferReport(query, tab === 'transfers');

  const isFiltered = FILTER_KEYS.some(key => params.has(key));
  const active = tab === 'views' ? views : tab === 'controls' ? controls : transfers;
  const listError = active.error ?? summary.error ?? filters.error;

  const pcs = useMemo(() => filters.data?.pcs ?? [], [filters.data]);
  const cabinetOptions = useMemo(() => {
    const byId = new Map<string, string>();
    for (const pc of pcs) if (pc.cabinetId) byId.set(pc.cabinetId, pc.cabinetName ?? 'Adı çözülemeyen kabin');
    return [...byId].map(([value, label]) => ({ value, label })).sort((a, b) => a.label.localeCompare(b.label, 'tr'));
  }, [pcs]);
  // Kabin seçiliyse PC listesi o kabinin PC'lerine daralır.
  const pcOptions = useMemo(
    () => pcs.filter(pc => cabinetId === REPORT_ALL || pc.cabinetId === cabinetId).map(pc => ({ value: pc.deviceId, label: pcOptionLabel(pc) })),
    [pcs, cabinetId]
  );
  const userOptions = useMemo(() => (filters.data?.users ?? []).map(user => ({ value: user.userId, label: userOptionLabel(user) })), [filters.data]);

  const pager = (data: PaginationResponse<unknown> | undefined) =>
    data && <DataTablePager pagination={data} onPageChange={next => params.update({ page: String(next) })} />;

  const handleExport = async () => {
    const { label, fileName, run } = EXPORTS[tab];
    const { rowCount, truncated } = await run(query, `izleme-raporu-${fileName}-${exportDateStamp()}`);
    if (truncated) toast.warning(`${label}: ilk ${EXPORT_MAX_ROWS.toLocaleString('tr-TR')} kayıt aktarıldı. Tamamı için filtreyi daraltın.`);
    else if (rowCount === 0) toast.info(`${label}: aktarılacak kayıt yok.`);
  };

  return (
    <ReportPage>
      <ReportHeader
        title='İzleme Raporu'
        description='PC ekranlarını kimin, ne zaman izlediği, uzaktan kontrol ettiği ve hangi dosyaları aktardığı. Kayıtlar otomatik yazılır; buradan değiştirilemez.'
        actions={
          <ReportExportButton
            onExport={handleExport}
            disabled={!active.data || active.data.dataCount === 0}
            title={`${EXPORTS[tab].label} sekmesinin filtreye uyan tüm kayıtlarını CSV olarak indir`}
          />
        }
      />

      <ReportFilterBar isFiltered={isFiltered} onReset={() => params.update(Object.fromEntries(FILTER_KEYS.map(key => [key, null])))}>
        <ReportSelectFilter
          id='report-cabinet'
          label='Kabin'
          value={cabinetId}
          // Önceki PC seçimi yeni kabinde olmayabilir: kabin değişince PC filtresi düşer.
          onChange={value => params.update({ cabinetId: value, deviceId: null })}
          allLabel='Tüm kabinler'
          options={cabinetOptions}
        />
        <ReportSelectFilter
          id='report-pc'
          label='PC'
          value={deviceId}
          onChange={value => params.update({ deviceId: value })}
          allLabel="Tüm PC'ler"
          options={pcOptions}
        />
        <ReportSelectFilter
          id='report-user'
          label='Kullanıcı'
          value={userId}
          onChange={value => params.update({ userId: value })}
          allLabel='Tüm kullanıcılar'
          options={userOptions}
        />
        <ReportDateFilter id='report-from' label='Başlangıç' value={from} onChange={value => params.update({ from: value })} />
        <ReportDateFilter id='report-to' label='Bitiş' defaultTime='23:59' value={to} onChange={value => params.update({ to: value })} />
      </ReportFilterBar>

      {listError && <p className='text-sm text-destructive'>{listError.message}</p>}

      <ReportStatGrid>
        {summary.data ? (
          <>
            <ReportStat title='İzleme' value={formatDuration(summary.data.viewSeconds)} detail={`${summary.data.viewCount} oturum`} />
            <ReportStat title='Uzaktan kontrol' value={formatDuration(summary.data.controlSeconds)} detail={`${summary.data.controlCount} oturum`} />
            <ReportStat
              title='En çok izlenen PC'
              value={summary.data.topPc ? (summary.data.topPc.deviceName ?? 'Silinmiş PC') : '—'}
              detail={
                summary.data.topPc
                  ? [summary.data.topPc.cabinetName, formatDuration(summary.data.topPc.seconds)].filter(Boolean).join(' · ')
                  : 'Kayıt yok'
              }
            />
            <ReportStat
              title='En çok izleyen'
              value={summary.data.topUser ? (summary.data.topUser.fullName ?? 'Bilinmeyen kullanıcı') : '—'}
              detail={summary.data.topUser ? formatDuration(summary.data.topUser.seconds) : 'Kayıt yok'}
            />
          </>
        ) : (
          Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className='h-20 w-full rounded-xl' />)
        )}
      </ReportStatGrid>

      <Tabs value={tab} onValueChange={value => params.update({ tab: value === 'views' ? null : String(value) })}>
        <TabsList>
          <TabsTrigger value='views'>{EXPORTS.views.label}</TabsTrigger>
          <TabsTrigger value='controls'>{EXPORTS.controls.label}</TabsTrigger>
          <TabsTrigger value='transfers'>{EXPORTS.transfers.label}</TabsTrigger>
        </TabsList>

        <TabsContent value='views' className='flex flex-col gap-3'>
          <DataTable
            columns={VIEW_COLUMNS}
            rows={views.data?.data}
            getRowKey={row => String(row.id)}
            isLoading={views.isPending}
            emptyMessage={isFiltered ? 'Bu filtreye uyan izleme kaydı yok.' : 'Henüz izleme kaydı yok.'}
          />
          {pager(views.data)}
        </TabsContent>

        <TabsContent value='controls' className='flex flex-col gap-3'>
          <DataTable
            columns={CONTROL_COLUMNS}
            rows={controls.data?.data}
            getRowKey={row => row.id}
            isLoading={controls.isPending}
            emptyMessage={isFiltered ? 'Bu filtreye uyan kontrol kaydı yok.' : 'Henüz uzaktan kontrol kaydı yok.'}
          />
          {pager(controls.data)}
        </TabsContent>

        <TabsContent value='transfers' className='flex flex-col gap-3'>
          <DataTable
            columns={TRANSFER_COLUMNS}
            rows={transfers.data?.data}
            getRowKey={row => row.id}
            isLoading={transfers.isPending}
            emptyMessage={isFiltered ? 'Bu filtreye uyan dosya transferi yok.' : 'Henüz dosya transferi yok.'}
          />
          {pager(transfers.data)}
        </TabsContent>
      </Tabs>
    </ReportPage>
  );
}

// ─────────────────────────────────────────────────────────── kolonlar

const DATE_COLUMN = 'w-44 font-mono text-xs whitespace-nowrap';

/** Üç listenin ortak kolonları: başlangıç, bitiş, süre, kullanıcı, PC. */
function commonColumns<T extends ScreenViewReportItemDto | RemoteControlReportItemDto>(): DataTableColumn<T>[] {
  return [
    { id: 'started', header: 'Başlangıç', className: DATE_COLUMN, cell: row => formatUtcDateTime(row.startedUtc) },
    {
      id: 'ended',
      header: 'Bitiş',
      className: DATE_COLUMN,
      cell: row => (row.endedUtc ? formatUtcDateTime(row.endedUtc) : <Badge variant='secondary' className='font-sans'>Sürüyor</Badge>)
    },
    // Açık kayıtta süre yok: "şimdi" render'da okunmaz (saf olmayan değer); özet kartı açık kaydı sunucuda zaten sayar.
    {
      id: 'duration',
      header: 'Süre',
      className: 'w-28 tabular-nums',
      cell: row => (row.endedUtc ? formatDuration(secondsBetween(row.startedUtc, row.endedUtc)) : '—')
    },
    { id: 'user', header: 'Kullanıcı', cell: row => <NameCell name={row.userFullName} detail={row.userName} missing='bilinmeyen kullanıcı' /> },
    { id: 'pc', header: 'PC', cell: row => <NameCell name={row.deviceName} detail={row.cabinetName} missing='silinmiş PC' /> }
  ];
}

const VIEW_COLUMNS: DataTableColumn<ScreenViewReportItemDto>[] = [
  ...commonColumns<ScreenViewReportItemDto>(),
  { id: 'monitor', header: 'Monitör', className: 'w-28', cell: row => `Monitör ${row.monitorIndex}` }
];

const CONTROL_COLUMNS: DataTableColumn<RemoteControlReportItemDto>[] = [
  ...commonColumns<RemoteControlReportItemDto>(),
  { id: 'reason', header: 'Bitiş nedeni', cell: row => (row.endReason ? RemoteControlEndReasonLabels[row.endReason] : '—') }
];

const TRANSFER_COLUMNS: DataTableColumn<FileTransferReportItemDto>[] = [
  { id: 'started', header: 'Başlangıç', className: DATE_COLUMN, cell: row => formatUtcDateTime(row.startedUtc) },
  { id: 'user', header: 'Kullanıcı', cell: row => <NameCell name={row.userFullName} detail={row.userName} missing='bilinmeyen kullanıcı' /> },
  { id: 'pc', header: 'PC', cell: row => <NameCell name={row.deviceName} detail={row.cabinetName} missing='silinmiş PC' /> },
  { id: 'direction', header: 'Yön', cell: row => directionLabel(row.direction) },
  { id: 'file', header: 'Dosya', className: 'max-w-[20rem] truncate', cell: row => <NameCell name={row.fileName} detail={row.path} missing='—' /> },
  { id: 'size', header: 'Boyut', className: 'w-24 tabular-nums', cell: row => formatBytes(row.sizeBytes) },
  {
    id: 'result',
    header: 'Sonuç',
    className: 'w-28',
    cell: row =>
      row.success == null ? (
        <Badge variant='secondary'>Sürüyor</Badge>
      ) : row.success ? (
        <Badge variant='outline'>Başarılı</Badge>
      ) : (
        <span className='text-amber-600 dark:text-amber-400' title={row.error ?? undefined}>
          Başarısız
        </span>
      )
  }
];

// ─────────────────────────────────────────────────────────── dışa aktarma

/**
 * Sekme başına dışa aktarma: ekrandaki sayfa değil, filtreye uyan TÜM kayıtlar (`fetchAllPages`, `EXPORT_MAX_ROWS` tavanı).
 * Süreler ve boyut sayı olarak (saniye, bayt) yazılır — Excel'de hesap yapılabilsin; tarihler yerel saatle.
 */
function exporter<T>(fetchPage: (query: RemoteAccessReportQuery) => Promise<PaginationResponse<T>>, columns: CsvColumn<T>[]) {
  return async (query: RemoteAccessReportQuery, fileName: string) => {
    const { rows, truncated } = await fetchAllPages(page => fetchPage({ ...query, page, pageSize: EXPORT_PAGE_SIZE }), EXPORT_MAX_ROWS);
    if (rows.length > 0) downloadCsv(fileName, columns, rows);
    return { rowCount: rows.length, truncated };
  };
}

type AuditRow = ScreenViewReportItemDto | RemoteControlReportItemDto | FileTransferReportItemDto;

function commonCsvColumns<T extends AuditRow>(): CsvColumn<T>[] {
  return [
    { header: 'Başlangıç', value: row => formatUtcDateTime(row.startedUtc) },
    { header: 'Bitiş', value: row => (row.endedUtc ? formatUtcDateTime(row.endedUtc) : 'Sürüyor') },
    { header: 'Süre (sn)', value: row => (row.endedUtc ? secondsBetween(row.startedUtc, row.endedUtc) : null) },
    { header: 'Kullanıcı', value: row => row.userFullName ?? 'Bilinmeyen kullanıcı' },
    { header: 'Kullanıcı adı', value: row => row.userName },
    { header: 'PC', value: row => row.deviceName ?? 'Silinmiş PC' },
    { header: 'Kabin', value: row => row.cabinetName }
  ];
}

const EXPORTS: Record<ReportTab, { label: string; fileName: string; run: (query: RemoteAccessReportQuery, fileName: string) => Promise<{ rowCount: number; truncated: boolean }> }> = {
  views: {
    label: 'İzleme kayıtları',
    fileName: 'izleme',
    run: exporter(getViewReport, [...commonCsvColumns<ScreenViewReportItemDto>(), { header: 'Monitör', value: row => row.monitorIndex }])
  },
  controls: {
    label: 'Kontrol kayıtları',
    fileName: 'kontrol',
    run: exporter(getControlReport, [
      ...commonCsvColumns<RemoteControlReportItemDto>(),
      { header: 'Bitiş nedeni', value: row => (row.endReason ? RemoteControlEndReasonLabels[row.endReason] : null) }
    ])
  },
  transfers: {
    label: 'Dosya transferi',
    fileName: 'dosya-transferi',
    run: exporter(getTransferReport, [
      ...commonCsvColumns<FileTransferReportItemDto>(),
      { header: 'Yön', value: row => directionLabel(row.direction) },
      { header: 'Dosya', value: row => row.fileName },
      { header: 'Yol', value: row => row.path },
      { header: 'Boyut (bayt)', value: row => row.sizeBytes },
      { header: 'Sonuç', value: row => (row.success == null ? 'Sürüyor' : row.success ? 'Başarılı' : 'Başarısız') },
      { header: 'Hata', value: row => row.error }
    ])
  }
};

// ─────────────────────────────────────────────────────────── yardımcılar

function directionLabel(direction: FileTransferReportItemDto['direction']): string {
  return direction === FileTransferDirection.ToPc ? "PC'ye yükleme" : "PC'den indirme";
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB', 'TB'];
  let value = bytes / 1024;
  let i = 0;
  while (value >= 1024 && i < units.length - 1) {
    value /= 1024;
    i++;
  }
  return `${value.toFixed(value >= 10 || i === 0 ? 0 : 1)} ${units[i]}`;
}

/** Ad + altında ikincil bilgi. Ad `null` = kayıt çekirdekte artık yok; denetim satırı yine de gösterilir. */
function NameCell({ name, detail, missing }: { name: string | null; detail: string | null; missing: string }): ReactNode {
  return (
    <div className='flex min-w-0 flex-col'>
      {name ? <span className='truncate'>{name}</span> : <span className='text-muted-foreground italic'>{missing}</span>}
      {detail && <span className='truncate text-xs text-muted-foreground'>{detail}</span>}
    </div>
  );
}

function pcOptionLabel(pc: ReportPcOptionDto): string {
  const name = pc.deviceName ?? 'Silinmiş PC';
  return pc.cabinetName ? `${name} · ${pc.cabinetName}` : name;
}

function userOptionLabel(user: ReportUserOptionDto): string {
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
