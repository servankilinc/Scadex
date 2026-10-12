import { Link } from 'react-router';
import { ChevronRightIcon, ExternalLinkIcon, InfoIcon } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { PanelSection } from '@/components/custom/panel-section';
import { StatusDot } from '@/components/diagram/template-node';
import { useCabinetStatusBreakdown } from '@/hooks/use-cabinet-status-breakdown';
import type { CabinetStatusBreakdownDto, CabinetStatusContributorDto } from '@/models/cabinet';
import { CabinetStatusContributorKind, DeviceStatus, deviceStatusLabel } from '@/models/enums';
import { cn, formatUtcDateTime } from '@/lib/utils';

const RULE_HINT =
  'Kabin durumu, katkıların en kötüsüdür. Kabini Çevrimiçi ya da Çevrimdışı yalnızca kontrol modülü (SCADA kartı) yapar; ' +
  'diğer cihazların ve izlenen kameraların Çevrimdışı olması kabine Uyarı olarak yansır, Çevrimiçi olmaları kabini değiştirmez.';

/**
 * Kabin detay panelinin "Durum" bölümü: kabinin rengi ve NEDENİ — hangi cihaz/kamera ne katkı veriyor.
 *
 * Kural burada TEKRARLANMAZ — katkıyı sunucu, durumu hesaplayan kuralın aynısıyla üretir (`contribution`); ekran yalnızca
 * okur ve anlatır. Liste hesaplamanın gördüğü her kaydı gösterir (kullanıcı kararı): her kayıt tek satırdır, ayrıntısı
 * (tür, yoklama, son görülme, son hata) satıra tıklayınca açılır; kabini düşürenler önde ve açık başlar.
 *
 * Başlıktaki durum `statusId` prop'undan (kabin listesi, canlı tazelenir) okunur — döküm yüklenmeden de görünsün.
 */
export function CabinetStatusSection({ cabinetId, statusId }: { cabinetId: string; statusId: DeviceStatus | null }) {
  const breakdown = useCabinetStatusBreakdown(cabinetId);

  return (
    <PanelSection
      title='Durum'
      summary={
        <>
          <StatusDot statusId={statusId} />
          <span className='font-medium text-foreground'>{deviceStatusLabel(statusId)}</span>
        </>
      }>
      {breakdown.isPending ? (
        <Skeleton className='h-20 w-full rounded-md' />
      ) : breakdown.isError ? (
        <p className='text-xs text-destructive'>{breakdown.error.message}</p>
      ) : (
        <>
          <p className='flex items-start gap-1.5 text-sm'>
            <span className='flex-1'>{summarize(breakdown.data)}</span>
            <InfoIcon className='mt-0.5 size-3.5 shrink-0 text-muted-foreground' aria-label={RULE_HINT}>
              <title>{RULE_HINT}</title>
            </InfoIcon>
          </p>

          {breakdown.data.items.length > 0 && (
            <ul className='divide-y rounded-md border'>
              {breakdown.data.items.map(item => (
                <ContributorRow key={`${item.kind}-${item.id}`} cabinetId={cabinetId} item={item} />
              ))}
            </ul>
          )}
        </>
      )}
    </PanelSection>
  );
}

/** Tek satır (durum noktası, ad, durum, kabine yansıma); tıklayınca ayrıntı açılır. Kabini düşüren satır açık başlar. */
function ContributorRow({ cabinetId, item }: { cabinetId: string; item: CabinetStatusContributorDto }) {
  const degrades = isDegrading(item);
  const isCamera = item.kind === CabinetStatusContributorKind.Camera;

  return (
    <li className={cn(degrades && (item.contribution === DeviceStatus.Warning ? 'bg-amber-500/5' : 'bg-destructive/5'))}>
      <Collapsible defaultOpen={degrades}>
        <CollapsibleTrigger className='group flex w-full cursor-pointer items-center gap-2 px-2.5 py-2 text-left text-xs hover:bg-muted/50'>
          <ChevronRightIcon className='size-3.5 shrink-0 text-muted-foreground transition-transform duration-200 group-data-panel-open:rotate-90' />
          <StatusDot statusId={item.statusId} />
          <span className='min-w-0 flex-1 truncate font-medium'>{item.name}</span>
          {degrades && item.contribution != null ? (
            <span
              className={cn('shrink-0 font-medium', item.contribution === DeviceStatus.Warning ? 'text-amber-600 dark:text-amber-400' : 'text-destructive')}
              title='Kabine yansıyan durum'>
              → {deviceStatusLabel(item.contribution)}
            </span>
          ) : (
            <span className='shrink-0 text-muted-foreground'>{deviceStatusLabel(item.statusId)}</span>
          )}
        </CollapsibleTrigger>

        <CollapsibleContent>
          <dl className='grid grid-cols-[5.5rem_1fr] gap-x-2 gap-y-1 px-2.5 pb-2.5 pl-8 text-xs'>
            <dt className='text-muted-foreground'>Tür</dt>
            <dd>{kindLabel(item)}</dd>
            <dt className='text-muted-foreground'>Durum</dt>
            <dd>{deviceStatusLabel(item.statusId)}</dd>
            <dt className='text-muted-foreground'>Kanıt</dt>
            <dd className='break-words'>{monitoringLabel(item)}</dd>
            <dt className='text-muted-foreground'>Son görülme</dt>
            <dd>{item.lastSeen ? formatUtcDateTime(item.lastSeen) : 'hiç'}</dd>
            {item.lastConnectionError && (
              <>
                <dt className='text-muted-foreground'>Son hata</dt>
                <dd className='break-words text-destructive'>{item.lastConnectionError}</dd>
              </>
            )}
            <dd className='col-start-2'>
              <Link
                to={isCamera ? `/cameras/${item.id}` : `/cabinets/${cabinetId}/diagram`}
                className='inline-flex items-center gap-1 text-primary underline-offset-4 hover:underline'>
                {isCamera ? 'Kamerayı aç' : 'Diyagramda aç'}
                <ExternalLinkIcon className='size-3' />
              </Link>
            </dd>
          </dl>
        </CollapsibleContent>
      </Collapsible>
    </li>
  );
}

// ─────────────────────────────────────────────────────────── anlatım

/** Kabini Çevrimiçi'den aşağı çeken katkı: Online ve "katkı yok" dışındaki her şey. */
function isDegrading(item: CabinetStatusContributorDto): boolean {
  return item.contribution != null && item.contribution !== DeviceStatus.Online;
}

/** Durumun tek cümlelik nedeni — yalnızca sunucunun `contribution`'ını okur, kuralı yeniden kurmaz. */
function summarize(data: CabinetStatusBreakdownDto): string {
  if (data.isMaintenance) return 'Kabin elle bakıma alındı; bakımdan çıkarılana kadar otomatik durum hesabı durur.';

  const controlModules = data.items.filter(i => i.isControlModule);
  const others = data.items.filter(i => !i.isControlModule && isDegrading(i));
  const devices = others.filter(i => i.kind === CabinetStatusContributorKind.Device).length;
  const cameras = others.filter(i => i.kind === CabinetStatusContributorKind.Camera).length;

  const parts: string[] = [];
  if (!data.hasControlModule) parts.push('Kabinde kontrol modülü (SCADA kartı) yok; kabin Çevrimiçi olamaz.');
  else if (controlModules.some(isDegrading)) parts.push('Kontrol modülüne (SCADA kartı) ulaşılamıyor.');
  else if (controlModules.every(i => i.contribution == null)) parts.push('Kontrol modülünden henüz haber alınmadı.');

  if (devices > 0 || cameras > 0) {
    const counts = [devices > 0 && `${devices} cihaz`, cameras > 0 && `${cameras} kamera`].filter(Boolean).join(' ve ');
    parts.push(`${counts} çevrimdışı — kabine Uyarı olarak yansıyor.`);
  }

  return parts.length > 0 ? parts.join(' ') : 'Sorun yok: kontrol modülü çevrimiçi, izlenen cihaz ve kameralar ulaşılabilir.';
}

function kindLabel(item: CabinetStatusContributorDto): string {
  if (item.kind === CabinetStatusContributorKind.Camera) return 'Kamera';
  if (item.isControlModule) return 'Kontrol modülü';
  return item.templateName ?? 'Cihaz';
}

/** Kaydın kanıtı nereden geliyor: SCADA teması (kontrol modülü) ve/veya yoklama (port varsa TCP, yoksa ICMP ping). */
function monitoringLabel(item: CabinetStatusContributorDto): string {
  const target = item.monitoringPort == null ? `ping ${item.ipAddress ?? '?'}` : `TCP ${item.ipAddress ?? '?'}:${item.monitoringPort}`;
  const probe = item.isMonitoringEnabled ? `${target}, ${item.pingIntervalSec} sn'de bir` : null;
  if (item.isControlModule) return probe ? `SCADA teması + ${probe}` : 'SCADA teması';
  return probe ?? 'İzleme kapalı';
}
