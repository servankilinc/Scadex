import { useMemo } from 'react';
import { Link } from 'react-router';
import { MonitorIcon, SirenIcon, SlidersHorizontalIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
import {
  ReportExportButton,
  ReportFilterBar,
  ReportHeader,
  ReportPage,
  ReportSearchFilter,
  ReportSelectFilter,
  ReportStat,
  ReportStatGrid
} from '@/components/custom/report';
import { usePermission } from '@/lib/auth-session';
import { downloadCsv, exportDateStamp, type CsvColumn } from '@/lib/csv-export';
import { REPORT_ALL, useReportParams } from '@/lib/report-params';
import { cn, formatUtcDateTime, toSearchKey } from '@/lib/utils';
import { useOpenSessions } from '../../hooks/use-operator-sessions';
import { useSignalCabinetOverview } from '../../hooks/use-signal-config';
import type { SignalCabinetOverviewDto } from '../../models/cabinet';

/** Kabindeki açık işlemler — canlı paneldeki açık oturum sorgusundan türetilir (sunucu tablosu bunu taşımaz). */
interface CabinetLive {
  openCount: number;
  hasAlert: boolean;
}

type LiveState = 'alert' | 'busy' | 'idle';
type ConfigState = 'enabled' | 'disabled' | 'unconfigured';

/** Ekranın satırı: sunucu satırı + canlı durum. */
type Row = SignalCabinetOverviewDto & { live: CabinetLive };

const IDLE: CabinetLive = { openCount: 0, hasAlert: false };

const CONFIG_LABELS: Record<ConfigState, string> = { enabled: 'Etkin', disabled: 'Devre dışı', unconfigured: 'Yapılandırılmamış' };
const LIVE_LABELS: Record<LiveState, string> = { alert: 'Alarm', busy: 'İşlemde', idle: 'Boşta' };

/** URL'deki filtre anahtarları — "Filtreyi temizle" bunları siler. */
const FILTER_KEYS = ['q', 'config', 'authority', 'live'];

/**
 * Sinyalizasyon kabinleri — `/signalization/cabinets`.
 *
 * Çekirdekteki Kabinler ekranından AYRIDIR: orada kabin künyesi düzenlenir ve diyagrama girilir; burada kabinin modüldeki hâli
 * okunur — kapı yapılandırması, canlı işlem/alarm, siren, zamanlama ayarları, son 30 günün işlemleri. Çekirdekteki her kabin
 * listelenir (pasifler soluk); modülde satırı olmayan "Yapılandırılmamış"tır ve ilk kurulumu da "Yapılandır" ile buradan yapılır —
 * Kapı Yapılandırması ekranına başka giriş yoktur.
 *
 * Canlılık: açık işlem/alarm, uyarı izleyicisiyle ORTAK açık oturum sorgusundan gelir (canlı yayınla tazelenir); tablo verisi
 * (siren, son işlem, sayılar) oturum yayınında ve yapılandırma kaydında tazelenir.
 */
export default function SignalCabinets() {
  const params = useReportParams();
  const search = params.get('q', '');
  const config = params.get('config');
  const authority = params.get('authority');
  const live = params.get('live');
  const isFiltered = FILTER_KEYS.some(key => params.has(key));

  const overview = useSignalCabinetOverview();
  // Kapı yapılandırması sistem yapılandırmasıdır (sunucuda `ConfigureSystem`); sanal kabin ayrı izindir
  // (`SignalVirtualCabinet`). Tablo herkese açık.
  const can = usePermission();
  const canConfigure = can('ConfigureSystem');
  const canOpenVirtual = can('SignalVirtualCabinet');
  const openSessions = useOpenSessions();

  const rows = useMemo<Row[] | undefined>(() => {
    if (!overview.data) return undefined;

    const liveByCabinet = new Map<string, CabinetLive>();
    for (const session of openSessions.data ?? []) {
      const current = liveByCabinet.get(session.cabinetId) ?? IDLE;
      liveByCabinet.set(session.cabinetId, { openCount: current.openCount + 1, hasAlert: current.hasAlert || session.hasAlert });
    }

    return overview.data
      .map(cabinet => ({ ...cabinet, live: liveByCabinet.get(cabinet.cabinetId) ?? IDLE }))
      .sort((a, b) => a.cabinetName.localeCompare(b.cabinetName, 'tr'));
  }, [overview.data, openSessions.data]);

  const authorityOptions = useMemo(
    () =>
      [...new Set((overview.data ?? []).flatMap(c => c.authorityNames))]
        .sort((a, b) => a.localeCompare(b, 'tr'))
        .map(name => ({ value: name, label: name })),
    [overview.data]
  );

  const visibleRows = useMemo(() => {
    if (!rows) return undefined;
    const term = toSearchKey(search.trim());
    return rows.filter(
      row =>
        (!term || toSearchKey(row.cabinetName).includes(term)) &&
        (config === REPORT_ALL || configState(row) === config) &&
        (authority === REPORT_ALL || row.authorityNames.includes(authority)) &&
        (live === REPORT_ALL || liveState(row) === live)
    );
  }, [rows, search, config, authority, live]);

  // Özet kartları SÜZÜLMÜŞ satırları sayar — rapor ekranlarındaki gibi filtreyi izler.
  const stats = useMemo(() => {
    const list = visibleRows ?? [];
    return {
      total: list.length,
      configured: list.filter(r => r.isConfigured).length,
      enabled: list.filter(r => r.isConfigured && r.isEnabled).length,
      openSessions: list.reduce((sum, r) => sum + r.live.openCount, 0),
      busyCabinets: list.filter(r => r.live.openCount > 0).length,
      alertCabinets: list.filter(r => r.live.hasAlert).length,
      sessions30d: list.reduce((sum, r) => sum + r.sessionCount30d, 0),
      flagged30d: list.reduce((sum, r) => sum + r.flaggedSessionCount30d, 0)
    };
  }, [visibleRows]);

  return (
    <ReportPage>
      <ReportHeader
        title='Sinyalizasyon Kabinleri'
        description='Kabinlerin sinyalizasyon yapılandırması, canlı durumu ve son 30 günün işlemleri. Kabin bilgisi ve diyagram çekirdeğin Kabinler ekranındadır.'
        actions={
          <ReportExportButton
            onExport={() => downloadCsv(`sinyalizasyon-kabinleri-${exportDateStamp()}`, CSV_COLUMNS, visibleRows ?? [])}
            disabled={!visibleRows?.length}
            title='Filtreye uyan kabinleri CSV olarak indir'
          />
        }
      />

      <ReportFilterBar isFiltered={isFiltered} onReset={() => params.update(Object.fromEntries(FILTER_KEYS.map(key => [key, null])))}>
        <ReportSearchFilter id='cabinet-search' label='Kabin' value={search} onChange={value => params.update({ q: value })} placeholder='Kabin ara…' />
        <ReportSelectFilter
          id='cabinet-config'
          label='Yapılandırma'
          value={config}
          onChange={value => params.update({ config: value })}
          allLabel='Hepsi'
          options={(Object.keys(CONFIG_LABELS) as ConfigState[]).map(key => ({ value: key, label: CONFIG_LABELS[key] }))}
        />
        <ReportSelectFilter
          id='cabinet-authority'
          label='Kurum'
          value={authority}
          onChange={value => params.update({ authority: value })}
          allLabel='Tüm kurumlar'
          options={authorityOptions}
        />
        <ReportSelectFilter
          id='cabinet-live'
          label='Canlı durum'
          value={live}
          onChange={value => params.update({ live: value })}
          allLabel='Hepsi'
          options={(Object.keys(LIVE_LABELS) as LiveState[]).map(key => ({ value: key, label: LIVE_LABELS[key] }))}
        />
      </ReportFilterBar>

      {overview.isError && <p className='text-sm text-destructive'>{overview.error.message}</p>}
      {openSessions.isError && <p className='text-sm text-destructive'>Canlı durum alınamadı: {openSessions.error.message}</p>}

      <ReportStatGrid>
        {visibleRows ? (
          <>
            <ReportStat title='Yapılandırılmış kabin' value={stats.configured.toLocaleString('tr-TR')} detail={`${stats.enabled} etkin · ${stats.total} kabin`} />
            <ReportStat title='Devam eden işlem' value={stats.openSessions.toLocaleString('tr-TR')} detail={`${stats.busyCabinets} kabinde`} />
            <ReportStat
              title='Alarmlı kabin'
              value={stats.alertCabinets.toLocaleString('tr-TR')}
              detail='Kartsız giriş / zorla açma'
              tone={stats.alertCabinets > 0 ? 'warning' : undefined}
            />
            <ReportStat title='Son 30 gün işlem' value={stats.sessions30d.toLocaleString('tr-TR')} detail={`${stats.flagged30d} bayraklı`} />
          </>
        ) : (
          Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className='h-20 w-full rounded-xl' />)
        )}
      </ReportStatGrid>

      <DataTable
        columns={COLUMNS}
        rows={visibleRows}
        getRowKey={row => row.cabinetId}
        isLoading={overview.isPending}
        isRowMuted={row => !row.cabinetIsActive}
        emptyMessage={rows?.length ? 'Bu filtreye uyan kabin yok.' : 'Henüz kabin yok — kabinler çekirdeğin Kabinler ekranında oluşturulur.'}
        actions={row => (
          <>
            {row.isConfigured && canOpenVirtual && (
              <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/signalization/virtual-cabinet/${row.cabinetId}`} />}>
                <MonitorIcon />
                Sanal kabin
              </Button>
            )}
            {canConfigure && (
              <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/signalization/cabinets/${row.cabinetId}`} />}>
                <SlidersHorizontalIcon />
                Yapılandır
              </Button>
            )}
          </>
        )}
      />
      {visibleRows && <span className='text-sm text-muted-foreground'>{visibleRows.length} kabin</span>}
    </ReportPage>
  );
}

// ─────────────────────────────────────────────────────────── durum

function configState(row: SignalCabinetOverviewDto): ConfigState {
  return !row.isConfigured ? 'unconfigured' : row.isEnabled ? 'enabled' : 'disabled';
}

/** Öncelik alarm > işlem > boşta (harita ikonlarıyla aynı). */
function liveState(row: Row): LiveState {
  return row.live.hasAlert ? 'alert' : row.live.openCount > 0 ? 'busy' : 'idle';
}

// ─────────────────────────────────────────────────────────── kolonlar

const MISSING = <span className='text-muted-foreground'>—</span>;

const COLUMNS: DataTableColumn<Row>[] = [
  {
    id: 'name',
    header: 'Kabin',
    cell: row => (
      <div className='flex flex-wrap items-center gap-1.5'>
        <span className='font-medium'>{row.cabinetName}</span>
        {!row.isConfigured && <Badge variant='outline'>Yapılandırılmamış</Badge>}
        {row.isConfigured && !row.isEnabled && (
          <Badge variant='outline' className='text-amber-600 dark:text-amber-400' title='Motor bu kabinin olaylarını yok sayar'>
            Devre dışı
          </Badge>
        )}
        {!row.cabinetIsActive && <Badge variant='secondary'>Pasif</Badge>}
      </div>
    )
  },
  {
    id: 'live',
    header: 'Canlı',
    cell: row => {
      if (!row.isConfigured) return MISSING;
      const state = liveState(row);
      return (
        <div className='flex flex-wrap items-center gap-1.5'>
          {state === 'alert' ? (
            <Badge variant='destructive'>Alarm</Badge>
          ) : state === 'busy' ? (
            <Badge>İşlemde{row.live.openCount > 1 && ` (${row.live.openCount})`}</Badge>
          ) : (
            <Badge variant='secondary'>Boşta</Badge>
          )}
          {row.sirenIsOn && (
            <Badge variant='destructive'>
              <SirenIcon className='animate-pulse' />
              Siren
            </Badge>
          )}
        </div>
      );
    }
  },
  {
    id: 'doors',
    header: 'Kapılar',
    className: 'whitespace-nowrap',
    cell: row =>
      row.isConfigured ? (
        <TwoLine primary={`${row.outerDoorCount} dış · ${row.innerDoorCount} iç`} secondary={`${row.cameraCount} kameralı dış kapı`} />
      ) : (
        MISSING
      )
  },
  {
    id: 'authorities',
    header: 'Kurumlar',
    className: 'max-w-[16rem] truncate',
    cell: row => (row.authorityNames.length ? <span title={row.authorityNames.join(', ')}>{row.authorityNames.join(', ')}</span> : MISSING)
  },
  {
    id: 'siren',
    header: 'Siren',
    className: 'whitespace-nowrap',
    cell: row => (!row.isConfigured ? MISSING : row.hasSiren ? `${row.sirenDurationSec} sn` : <span className='text-muted-foreground'>Tanımsız</span>)
  },
  {
    id: 'timing',
    header: 'Zamanlama',
    className: 'whitespace-nowrap',
    cell: row => (row.isConfigured ? <TwoLine primary={timingSummary(row)} secondary={snapshotSummary(row)} /> : MISSING)
  },
  {
    id: 'last',
    header: 'Son işlem',
    className: 'w-44 font-mono text-xs whitespace-nowrap',
    cell: row => (row.lastSessionStartedAtUtc ? formatUtcDateTime(row.lastSessionStartedAtUtc) : MISSING)
  },
  {
    id: 'recent',
    header: 'Son 30 gün',
    className: 'whitespace-nowrap',
    cell: row => (
      <div className='flex flex-col'>
        <span className='tabular-nums'>{row.sessionCount30d} işlem</span>
        <span className={cn('text-xs tabular-nums', row.flaggedSessionCount30d > 0 ? 'text-amber-600 dark:text-amber-400' : 'text-muted-foreground')}>
          {row.flaggedSessionCount30d} bayraklı
        </span>
      </div>
    )
  }
];

function TwoLine({ primary, secondary }: { primary: string; secondary: string }) {
  return (
    <div className='flex flex-col'>
      <span>{primary}</span>
      <span className='text-xs text-muted-foreground'>{secondary}</span>
    </div>
  );
}

/** "Kart 120 sn · En uzun 240 dk"; kart bekleme 0 ise kontrol yapılmaz. */
function timingSummary(row: SignalCabinetOverviewDto): string {
  const card = row.awaitingCardTimeoutSec ? `Kart ${row.awaitingCardTimeoutSec} sn` : 'Kart kontrolü yok';
  return `${card} · En uzun ${row.sessionMaxDurationMin} dk`;
}

/** Dış kapı açılınca alınan görüntüler: "5 görüntü × 1 sn". */
function snapshotSummary(row: SignalCabinetOverviewDto): string {
  if (!row.entrySnapshotCount) return 'Giriş görüntüsü yok';
  return `${row.entrySnapshotCount} görüntü × ${formatInterval(row.entrySnapshotIntervalMs ?? 0)}`;
}

function formatInterval(ms: number): string {
  return ms >= 1000 ? `${(ms / 1000).toLocaleString('tr-TR')} sn` : `${ms} ms`;
}

// ─────────────────────────────────────────────────────────── dışa aktarma

/** Süreler ve sayılar SAYI olarak (Excel'de hesap yapılabilsin); yapılandırılmamış kabinde modül alanları boş. */
const CSV_COLUMNS: CsvColumn<Row>[] = [
  { header: 'Kabin', value: row => row.cabinetName },
  { header: 'Çekirdekte aktif', value: row => (row.cabinetIsActive ? 'Evet' : 'Hayır') },
  { header: 'Yapılandırma', value: row => CONFIG_LABELS[configState(row)] },
  { header: 'Canlı durum', value: row => (row.isConfigured ? LIVE_LABELS[liveState(row)] : null) },
  { header: 'Açık işlem', value: row => row.live.openCount },
  { header: 'Siren çalıyor', value: row => (row.sirenIsOn == null ? null : row.sirenIsOn ? 'Evet' : 'Hayır') },
  { header: 'Dış kapı', value: row => (row.isConfigured ? row.outerDoorCount : null) },
  { header: 'İç kapı', value: row => (row.isConfigured ? row.innerDoorCount : null) },
  { header: 'Kameralı dış kapı', value: row => (row.isConfigured ? row.cameraCount : null) },
  { header: 'Kurumlar', value: row => row.authorityNames.join(', ') },
  { header: 'Siren süresi (sn)', value: row => (row.hasSiren ? row.sirenDurationSec : null) },
  { header: 'Kart bekleme (sn)', value: row => row.awaitingCardTimeoutSec },
  { header: 'En uzun işlem (dk)', value: row => row.sessionMaxDurationMin },
  { header: 'Giriş görüntüsü (adet)', value: row => row.entrySnapshotCount },
  { header: 'Görüntü aralığı (ms)', value: row => row.entrySnapshotIntervalMs },
  { header: 'Son işlem', value: row => (row.lastSessionStartedAtUtc ? formatUtcDateTime(row.lastSessionStartedAtUtc) : null) },
  { header: 'Son 30 gün işlem', value: row => row.sessionCount30d },
  { header: 'Son 30 gün bayraklı', value: row => row.flaggedSessionCount30d }
];
