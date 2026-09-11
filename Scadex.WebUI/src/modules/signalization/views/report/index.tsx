import { useMemo, useState } from 'react';
import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from 'recharts';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from '@/components/ui/chart';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useCabinets } from '@/hooks/use-cabinets';
import { useSessionSummary } from '../../hooks/use-operator-sessions';
import { formatDuration } from '../../lib';
import type { OperatorSessionSummaryRequest, OperatorSessionSummaryRowDto } from '../../models/session';

const ALL = 'all';
/** Sunucu kuralı: özet en fazla 366 günlük aralık için alınabilir. */
const MAX_RANGE_DAYS = 366;
/** Grafikte gösterilen satır üst sınırı; tablo hepsini gösterir. */
const CHART_ROWS = 10;

const chartConfig = {
  minutes: { label: 'Toplam süre (dk)', color: 'var(--chart-1)' }
} satisfies ChartConfig;

/**
 * Operatör işlem raporu — `/signalization/report`.
 *
 * Seçilen dönemde BAŞLAMIŞ ve TAMAMLANMIŞ işlemlerin özeti (açık işlemin süresi henüz yok, sayılmaz).
 * Operatör kırılımında bir işlemin süresi o işlemdeki HER operatöre sayılır — aynı anda iki operatör
 * çalışabildiği için operatör toplamları genel toplamı aşabilir.
 *
 * "Bayraklı" sütunu herhangi bir bayrağı olan işlemleri sayar (yalnızca güvenlik uyarılarını değil);
 * uyarıların kendisi işlem listesinde bayrak filtresiyle bulunur.
 */
export default function SessionReport() {
  const cabinets = useCabinets();
  const [range, setRange] = useState(defaultRange);
  const [cabinetId, setCabinetId] = useState(ALL);

  const cabinetOptions = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);

  const { request, rangeError } = buildRequest(range.from, range.to, cabinetId);
  const summary = useSessionSummary(request);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Operatör İşlem Raporu</h1>
        <p className='text-sm text-muted-foreground'>Seçilen dönemde başlamış ve tamamlanmış işlemler — operatör, kurum ve kabin kırılımında.</p>
      </div>

      <div className='flex flex-wrap items-end gap-3'>
        <Field className='w-full max-w-[11rem]'>
          <FieldLabel htmlFor='report-from'>Başlangıç günü</FieldLabel>
          <Input id='report-from' type='date' value={range.from} onChange={e => setRange(r => ({ ...r, from: e.target.value }))} />
        </Field>
        <Field className='w-full max-w-[11rem]'>
          <FieldLabel htmlFor='report-to'>Bitiş günü</FieldLabel>
          <Input id='report-to' type='date' value={range.to} onChange={e => setRange(r => ({ ...r, to: e.target.value }))} />
        </Field>
        <Field className='w-full max-w-xs'>
          <FieldLabel htmlFor='report-cabinet'>Kabin</FieldLabel>
          <Select value={cabinetId} onValueChange={value => setCabinetId(value ?? ALL)}>
            <SelectTrigger id='report-cabinet' className='w-full'>
              <SelectValue>{cabinetId === ALL ? 'Tüm kabinler' : cabinetOptions.find(c => c.id === cabinetId)?.name}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL}>Tüm kabinler</SelectItem>
              {cabinetOptions.map(cabinet => (
                <SelectItem key={cabinet.id} value={cabinet.id}>
                  {cabinet.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      </div>

      {rangeError && <p className='text-sm text-destructive'>{rangeError}</p>}
      {summary.isError && <p className='text-sm text-destructive'>{summary.error.message}</p>}
      {request && summary.isPending && <Skeleton className='h-80 w-full rounded-xl' />}

      {request && summary.data && (
        <>
          <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-4'>
            <Kpi label='İşlem' value={summary.data.sessionCount.toLocaleString('tr-TR')} />
            <Kpi label='Toplam süre' value={formatDuration(summary.data.totalDurationSec)} />
            <Kpi
              label='Ortalama süre'
              value={summary.data.sessionCount === 0 ? '—' : formatDuration(summary.data.totalDurationSec / summary.data.sessionCount)}
            />
            <Kpi label='Bayraklı işlem' value={summary.data.warningCount.toLocaleString('tr-TR')} tone={summary.data.warningCount > 0 ? 'warning' : undefined} />
          </div>

          {summary.data.sessionCount === 0 ? (
            <Card>
              <CardContent className='py-8 text-center text-sm text-muted-foreground'>Bu dönemde tamamlanmış işlem yok.</CardContent>
            </Card>
          ) : (
            <Tabs defaultValue='operator'>
              <TabsList>
                <TabsTrigger value='operator'>Operatör</TabsTrigger>
                <TabsTrigger value='authority'>Kurum</TabsTrigger>
                <TabsTrigger value='cabinet'>Kabin</TabsTrigger>
              </TabsList>
              <TabsContent value='operator'>
                <BreakdownCard
                  title='Operatör kırılımı'
                  description='İşlem süresi, işlemdeki her operatöre ayrı ayrı sayılır.'
                  nameHeader='Operatör'
                  rows={summary.data.byOperator}
                />
              </TabsContent>
              <TabsContent value='authority'>
                <BreakdownCard
                  title='Kurum kırılımı'
                  description='Kurum, işlem anındaki kurum adıdır (enstantane); sonradan yeniden adlandırma geçmişi değiştirmez.'
                  nameHeader='Kurum'
                  rows={summary.data.byAuthority}
                />
              </TabsContent>
              <TabsContent value='cabinet'>
                <BreakdownCard title='Kabin kırılımı' description='Kartsız işlemler de sayılır.' nameHeader='Kabin' rows={summary.data.byCabinet} />
              </TabsContent>
            </Tabs>
          )}
        </>
      )}
    </div>
  );
}

function Kpi({ label, value, tone }: { label: string; value: string; tone?: 'warning' }) {
  return (
    <div className='rounded-xl border p-3'>
      <div className='text-xs text-muted-foreground'>{label}</div>
      <div className={tone === 'warning' ? 'mt-1 font-mono text-xl font-semibold text-amber-600 tabular-nums dark:text-amber-400' : 'mt-1 font-mono text-xl font-semibold tabular-nums'}>
        {value}
      </div>
    </div>
  );
}

function BreakdownCard({ title, description, nameHeader, rows }: { title: string; description: string; nameHeader: string; rows: OperatorSessionSummaryRowDto[] }) {
  const chartRows = rows.slice(0, CHART_ROWS).map(row => ({ name: row.name, minutes: Math.round((row.totalDurationSec / 60) * 10) / 10 }));

  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className='flex flex-col gap-4'>
        {rows.length === 0 ? (
          <p className='text-sm text-muted-foreground'>Kayıt yok.</p>
        ) : (
          <>
            <ChartContainer config={chartConfig} className='aspect-auto w-full' style={{ height: chartRows.length * 36 + 32 }}>
              <BarChart accessibilityLayer data={chartRows} layout='vertical' margin={{ left: 8, right: 16 }}>
                <CartesianGrid horizontal={false} />
                <YAxis dataKey='name' type='category' tickLine={false} axisLine={false} width={140} tickMargin={6} />
                <XAxis dataKey='minutes' type='number' tickLine={false} axisLine={false} />
                <ChartTooltip cursor={false} content={<ChartTooltipContent />} />
                <Bar dataKey='minutes' fill='var(--color-minutes)' radius={4} />
              </BarChart>
            </ChartContainer>

            <div className='overflow-x-auto rounded-lg border'>
              <table className='w-full min-w-[32rem] text-sm'>
                <thead className='bg-muted/50 text-muted-foreground'>
                  <tr className='[&>th]:px-3 [&>th]:py-2 [&>th]:text-left [&>th]:font-medium'>
                    <th>{nameHeader}</th>
                    <th className='text-right'>İşlem</th>
                    <th className='text-right'>Toplam</th>
                    <th className='text-right'>Ortalama</th>
                    <th className='text-right'>Bayraklı</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map(row => (
                    <tr key={row.key} className='border-t [&>td]:px-3 [&>td]:py-2'>
                      <td className='max-w-[18rem] truncate'>{row.name}</td>
                      <td className='text-right font-mono tabular-nums'>{row.sessionCount}</td>
                      <td className='text-right font-mono tabular-nums'>{formatDuration(row.totalDurationSec)}</td>
                      <td className='text-right font-mono tabular-nums'>{formatDuration(row.averageDurationSec)}</td>
                      <td className='text-right font-mono tabular-nums'>{row.warningCount}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── tarih aralığı

/** Son 30 gün (bugün dahil), yerel takvim günleri olarak. Modül düzeyinde: render içinde `Date` çağrılmasın. */
function defaultRange(): { from: string; to: string } {
  const today = new Date();
  const start = new Date(today);
  start.setDate(start.getDate() - 29);
  return { from: toDateInput(start), to: toDateInput(today) };
}

function toDateInput(date: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/**
 * Yerel takvim günleri → UTC aralık: başlangıç gününün 00:00'ı, bitiş gününün 23:59:59.999'u. Sunucu kurallarının
 * (bitiş > başlangıç, en fazla 366 gün) kopyası; ihlalde istek hiç atılmaz, mesaj gösterilir.
 */
function buildRequest(from: string, to: string, cabinetId: string): { request: OperatorSessionSummaryRequest | null; rangeError: string | null } {
  if (!from || !to) return { request: null, rangeError: 'Başlangıç ve bitiş günü seçin.' };

  const start = new Date(`${from}T00:00:00`);
  const end = new Date(`${to}T23:59:59.999`);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return { request: null, rangeError: 'Geçersiz tarih.' };
  if (end <= start) return { request: null, rangeError: 'Bitiş günü başlangıçtan önce olamaz.' };
  if ((end.getTime() - start.getTime()) / 86_400_000 > MAX_RANGE_DAYS) return { request: null, rangeError: `Özet en fazla ${MAX_RANGE_DAYS} günlük aralık için alınabilir.` };

  return {
    request: { fromUtc: start.toISOString(), toUtc: end.toISOString(), cabinetId: cabinetId === ALL ? null : cabinetId },
    rangeError: null
  };
}
