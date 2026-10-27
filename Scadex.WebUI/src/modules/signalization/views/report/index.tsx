import { useMemo, useState } from 'react';
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts';
import { Card, CardContent } from '@/components/ui/card';
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
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
import { useCabinets } from '@/hooks/use-cabinets';
import { downloadCsv } from '@/lib/csv-export';
import { REPORT_ALL, toDateTimeLocal, useReportParams } from '@/lib/report-params';
import { useSessionSummary } from '../../hooks/use-operator-sessions';
import { useSignalAuthorities, useSignalOperators } from '../../hooks/use-signal-config';
import { formatDuration } from '../../lib';
import type { OperatorSessionSummaryRequest, OperatorSessionSummaryRowDto } from '../../models/session';

/** Sunucu kuralı: özet en fazla 366 günlük aralık için alınabilir. */
const MAX_RANGE_DAYS = 366;
/** Grafikte gösterilen satır üst sınırı; tablo hepsini gösterir. */
const CHART_ROWS = 10;

const chartConfig = {
  minutes: { label: 'Toplam süre (dk)', color: 'var(--chart-1)' }
} satisfies ChartConfig;

type BreakdownTab = 'operator' | 'authority' | 'cabinet';

/** Sekme başına kırılım: başlık, açıklama ve dışa aktarılan dosyanın adı. */
const BREAKDOWNS: Record<BreakdownTab, { label: string; nameHeader: string; description: string; fileName: string }> = {
  operator: {
    label: 'Operatör',
    nameHeader: 'Operatör',
    description: 'İşlem süresi, işlemdeki her operatöre ayrı ayrı sayılır — operatör toplamları genel toplamı aşabilir.',
    fileName: 'operator'
  },
  authority: {
    label: 'Kurum',
    nameHeader: 'Kurum',
    description: 'Kurum, işlem anındaki kurum adıdır (enstantane); sonradan yeniden adlandırma geçmişi değiştirmez.',
    fileName: 'kurum'
  },
  cabinet: { label: 'Kabin', nameHeader: 'Kabin', description: 'Kartsız işlemler de sayılır.', fileName: 'kabin' }
};

/** URL'deki filtre anahtarları — "Filtreyi temizle" bunları siler. */
const FILTER_KEYS = ['cabinetId', 'userId', 'authorityId', 'from', 'to'];

/**
 * Operatör işlem raporu — `/signalization/report`.
 *
 * Seçilen dönemde BAŞLAMIŞ ve TAMAMLANMIŞ işlemlerin özeti (açık işlemin süresi henüz yok, sayılmaz). Operatör/kurum seçilince
 * o operatörün (kurumun) KATILDIĞI işlemler özetlenir; kırılımlarda aynı işlemdeki diğer operatörler de görünür.
 *
 * "Bayraklı" sütunu herhangi bir bayrağı olan işlemleri sayar (yalnızca güvenlik uyarılarını değil); uyarıların kendisi
 * işlem listesinde bayrak filtresiyle bulunur. Filtreler ve sekme URL'dedir (`useReportParams`), dönem yoksa son 30 gün.
 */
export default function SessionReport() {
  const params = useReportParams();
  // Varsayılan dönem bir kez hesaplanır: render içinde `Date` okunmasın, saat ilerledikçe sorgu anahtarı kaymasın.
  const [defaults] = useState(defaultRange);

  const cabinetId = params.get('cabinetId');
  const userId = params.get('userId');
  const authorityId = params.get('authorityId');
  const from = params.get('from', defaults.from);
  const to = params.get('to', defaults.to);
  const rawTab = params.get('tab', 'operator');
  const tab: BreakdownTab = rawTab === 'authority' || rawTab === 'cabinet' ? rawTab : 'operator';
  const isFiltered = FILTER_KEYS.some(key => params.has(key));

  const cabinets = useCabinets();
  const operators = useSignalOperators();
  const authorities = useSignalAuthorities();

  const cabinetOptions = useMemo(() => (cabinets.data ?? []).filter(c => c.isActive).map(c => ({ value: c.id, label: c.name })), [cabinets.data]);
  const operatorOptions = useMemo(() => (operators.data ?? []).map(o => ({ value: o.userId, label: o.fullName })), [operators.data]);
  const authorityOptions = useMemo(
    () => (authorities.data ?? []).map(a => ({ value: a.id, label: a.isActive ? a.name : `${a.name} (pasif)` })),
    [authorities.data]
  );

  const { request, rangeError } = buildRequest({ from, to, cabinetId, userId, authorityId });
  const summary = useSessionSummary(request);
  const data = request ? summary.data : undefined;

  const breakdownRows = (key: BreakdownTab) =>
    data ? (key === 'operator' ? data.byOperator : key === 'authority' ? data.byAuthority : data.byCabinet) : undefined;

  const handleExport = () => {
    const rows = breakdownRows(tab) ?? [];
    downloadCsv(`islem-raporu-${BREAKDOWNS[tab].fileName}-${from.slice(0, 10)}_${to.slice(0, 10)}`, exportColumns(BREAKDOWNS[tab].nameHeader), rows);
  };

  return (
    <ReportPage>
      <ReportHeader
        title='Operatör İşlem Raporu'
        description='Seçilen dönemde başlamış ve tamamlanmış işlemler — operatör, kurum ve kabin kırılımında.'
        actions={
          <ReportExportButton
            onExport={handleExport}
            disabled={!data || data.sessionCount === 0}
            title={`${BREAKDOWNS[tab].label} kırılımını filtreyle birlikte CSV olarak indir`}
          />
        }
      />

      <ReportFilterBar isFiltered={isFiltered} onReset={() => params.update(Object.fromEntries(FILTER_KEYS.map(key => [key, null])))}>
        <ReportSelectFilter
          id='report-cabinet'
          label='Kabin'
          value={cabinetId}
          onChange={value => params.update({ cabinetId: value })}
          allLabel='Tüm kabinler'
          options={cabinetOptions}
        />
        <ReportSelectFilter
          id='report-operator'
          label='Operatör'
          value={userId}
          onChange={value => params.update({ userId: value })}
          allLabel='Tüm operatörler'
          options={operatorOptions}
        />
        <ReportSelectFilter
          id='report-authority'
          label='Kurum'
          value={authorityId}
          onChange={value => params.update({ authorityId: value })}
          allLabel='Tüm kurumlar'
          options={authorityOptions}
        />
        <ReportDateFilter id='report-from' label='Başlangıç' value={from} onChange={value => params.update({ from: value })} />
        <ReportDateFilter id='report-to' label='Bitiş' defaultTime='23:59' value={to} onChange={value => params.update({ to: value })} />
      </ReportFilterBar>

      {rangeError && <p className='text-sm text-destructive'>{rangeError}</p>}
      {request && summary.isError && <p className='text-sm text-destructive'>{summary.error.message}</p>}

      {request && (
        <ReportStatGrid>
          {data ? (
            <>
              <ReportStat title='İşlem' value={data.sessionCount.toLocaleString('tr-TR')} detail='Tamamlanmış' />
              <ReportStat title='Toplam süre' value={formatDuration(data.totalDurationSec)} />
              <ReportStat title='Ortalama süre' value={data.sessionCount === 0 ? '—' : formatDuration(data.totalDurationSec / data.sessionCount)} />
              <ReportStat
                title='Bayraklı işlem'
                value={data.warningCount.toLocaleString('tr-TR')}
                detail='Herhangi bir bayrağı olan'
                tone={data.warningCount > 0 ? 'warning' : undefined}
              />
            </>
          ) : (
            Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className='h-20 w-full rounded-xl' />)
          )}
        </ReportStatGrid>
      )}

      {request && (
        <Tabs value={tab} onValueChange={value => params.update({ tab: value === 'operator' ? null : String(value) })}>
          <TabsList>
            {(Object.keys(BREAKDOWNS) as BreakdownTab[]).map(key => (
              <TabsTrigger key={key} value={key}>
                {BREAKDOWNS[key].label}
              </TabsTrigger>
            ))}
          </TabsList>

          {(Object.keys(BREAKDOWNS) as BreakdownTab[]).map(key => (
            <TabsContent key={key} value={key} className='flex flex-col gap-3'>
              <Breakdown tab={key} rows={breakdownRows(key)} isLoading={summary.isPending} isFiltered={isFiltered} />
            </TabsContent>
          ))}
        </Tabs>
      )}
    </ReportPage>
  );
}

function Breakdown({ tab, rows, isLoading, isFiltered }: { tab: BreakdownTab; rows: OperatorSessionSummaryRowDto[] | undefined; isLoading: boolean; isFiltered: boolean }) {
  const { nameHeader, description } = BREAKDOWNS[tab];
  const columns = useMemo(() => breakdownColumns(nameHeader), [nameHeader]);
  const chartRows = (rows ?? []).slice(0, CHART_ROWS).map(row => ({ name: row.name, minutes: Math.round((row.totalDurationSec / 60) * 10) / 10 }));

  return (
    <>
      <p className='text-xs text-muted-foreground'>{description}</p>

      {chartRows.length > 0 && (
        <Card size='sm'>
          <CardContent>
            <ChartContainer config={chartConfig} className='aspect-auto w-full' style={{ height: chartRows.length * 36 + 32 }}>
              <BarChart accessibilityLayer data={chartRows} layout='vertical' margin={{ left: 8, right: 16 }}>
                <CartesianGrid horizontal={false} />
                <YAxis dataKey='name' type='category' tickLine={false} axisLine={false} width={140} tickMargin={6} />
                <XAxis dataKey='minutes' type='number' tickLine={false} axisLine={false} />
                <ChartTooltip cursor={false} content={<ChartTooltipContent />} />
                <Bar dataKey='minutes' fill='var(--color-minutes)' radius={4} />
              </BarChart>
            </ChartContainer>
          </CardContent>
        </Card>
      )}

      <DataTable
        columns={columns}
        rows={rows}
        getRowKey={row => row.key}
        isLoading={isLoading}
        emptyMessage={isFiltered ? 'Bu filtreye uyan tamamlanmış işlem yok.' : 'Bu dönemde tamamlanmış işlem yok.'}
      />
      {rows && <span className='text-sm text-muted-foreground'>{rows.length} kayıt</span>}
    </>
  );
}

// ─────────────────────────────────────────────────────────── kolonlar

const NUMERIC = 'w-28 text-right font-mono tabular-nums';

function breakdownColumns(nameHeader: string): DataTableColumn<OperatorSessionSummaryRowDto>[] {
  return [
    { id: 'name', header: nameHeader, className: 'max-w-[18rem] truncate', cell: row => row.name },
    { id: 'count', header: 'İşlem', className: NUMERIC, cell: row => row.sessionCount.toLocaleString('tr-TR') },
    { id: 'total', header: 'Toplam', className: NUMERIC, cell: row => formatDuration(row.totalDurationSec) },
    { id: 'average', header: 'Ortalama', className: NUMERIC, cell: row => formatDuration(row.averageDurationSec) },
    { id: 'warning', header: 'Bayraklı', className: NUMERIC, cell: row => row.warningCount.toLocaleString('tr-TR') }
  ];
}

/**
 * Süreler SANİYE olarak sayı yazılır: `05:30` gibi bir metni Excel saat sanıp 5 sa 30 dk okurdu. Hesap yapılabilsin diye
 * metin değil sayı.
 */
function exportColumns(nameHeader: string) {
  return [
    { header: nameHeader, value: (row: OperatorSessionSummaryRowDto) => row.name },
    { header: 'İşlem', value: (row: OperatorSessionSummaryRowDto) => row.sessionCount },
    { header: 'Toplam süre (sn)', value: (row: OperatorSessionSummaryRowDto) => row.totalDurationSec },
    { header: 'Ortalama süre (sn)', value: (row: OperatorSessionSummaryRowDto) => row.averageDurationSec },
    { header: 'Bayraklı işlem', value: (row: OperatorSessionSummaryRowDto) => row.warningCount }
  ];
}

// ─────────────────────────────────────────────────────────── tarih aralığı

/** Son 30 gün (bugün dahil): başlangıç gününün 00:00'ı, bugünün 23:59'u — yerel saat. */
function defaultRange(): { from: string; to: string } {
  const end = new Date();
  end.setHours(23, 59, 0, 0);
  const start = new Date(end);
  start.setDate(start.getDate() - 29);
  start.setHours(0, 0, 0, 0);
  return { from: toDateTimeLocal(start), to: toDateTimeLocal(end) };
}

/**
 * Yerel tarih-saat → UTC aralık. Bitiş dakikasının sonuna kadar sayılır (`23:59` → `23:59:59.999`). Sunucu kurallarının
 * (bitiş > başlangıç, en fazla 366 gün) kopyası; ihlalde istek hiç atılmaz, mesaj gösterilir.
 */
function buildRequest(filter: { from: string; to: string; cabinetId: string; userId: string; authorityId: string }): {
  request: OperatorSessionSummaryRequest | null;
  rangeError: string | null;
} {
  if (!filter.from || !filter.to) return { request: null, rangeError: 'Başlangıç ve bitiş seçin — özet bir dönem için alınır.' };

  const start = new Date(filter.from);
  const end = new Date(filter.to);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return { request: null, rangeError: 'Geçersiz tarih.' };
  end.setSeconds(59, 999);
  if (end <= start) return { request: null, rangeError: 'Bitiş başlangıçtan önce olamaz.' };
  if ((end.getTime() - start.getTime()) / 86_400_000 > MAX_RANGE_DAYS) return { request: null, rangeError: `Özet en fazla ${MAX_RANGE_DAYS} günlük aralık için alınabilir.` };

  const orNull = (value: string) => (value === REPORT_ALL ? null : value);
  return {
    request: {
      fromUtc: start.toISOString(),
      toUtc: end.toISOString(),
      cabinetId: orNull(filter.cabinetId),
      userId: orNull(filter.userId),
      authorityId: orNull(filter.authorityId)
    },
    rangeError: null
  };
}
