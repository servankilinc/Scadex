import { useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { ArrowLeftIcon, CameraIcon, DoorClosedIcon, DoorOpenIcon, HelpCircleIcon, LightbulbIcon, LockIcon, LockOpenIcon, SirenIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { cn, formatUtcDateTime, toUtcDate } from '@/lib/utils';
import { CabinetShell } from '../../components/virtual-cabinet/cabinet-shell';
import { CameraFigure } from '../../components/virtual-cabinet/camera-figure';
import { CommandDialog, type CommandTarget } from '../../components/virtual-cabinet/command-dialog';
import { IndoorGrid } from '../../components/virtual-cabinet/indoor-grid';
import { LedFigure } from '../../components/virtual-cabinet/led-figure';
import { SirenFigure } from '../../components/virtual-cabinet/siren-figure';
import { useNow } from '../../hooks/use-now';
import { useSignalCabinetCommand, useSignalCabinetLive, useVirtualCabinetLive } from '../../hooks/use-virtual-cabinet';
import { formatDuration } from '../../lib';
import { SignalCabinetOutput, type SignalInnerDoorLiveDto, type SignalOuterDoorLiveDto } from '../../models/virtual-cabinet';

/**
 * Sanal kabin — `/signalization/virtual-cabinet/:cabinetId/:outerDoorId`.
 *
 * Kabinin içini çizer ve cihazlara operatörün dilinde komut verdirir: pin numarası değil "sireni çal", "kilidi aç".
 * İzleme tek kaynaktan beslenir ve **hiçbir yoklama yoktur** (bkz. `useVirtualCabinetLive`): modülün yayını, çekirdeğin kanal
 * değişimlerini kapı/siren/aydınlatma/kilide çevirip gönderir.
 *
 * Komutlar motorun otomatik davranışını bastırmaz — motor da durumu aynı kanaldan okur. Manuel açılan siren bir sonraki
 * uzlaştırmada (açık oturum talebi yoksa) susabilir; bu bilinçli bir sonuçtur, manuel komut "şu an sahaya git" demektir.
 *
 * Durum alanlarında `null` "bilinmiyor"dur (kanal hiç değer/başarılı komut görmemiş) — kapalı/kilitli gibi gösterilmez.
 */
export default function VirtualCabinetDetail() {
  const { cabinetId = '', outerDoorId = '' } = useParams<{ cabinetId: string; outerDoorId: string }>();
  const navigate = useNavigate();

  const { data, isPending, error } = useSignalCabinetLive(cabinetId);
  useVirtualCabinetLive(cabinetId);
  const command = useSignalCabinetCommand(cabinetId);

  const [target, setTarget] = useState<CommandTarget | null>(null);

  const outer = data?.outerDoors.find(door => door.id === outerDoorId);

  const send = (turnOn: boolean) => {
    if (!target) return;
    command.mutate({ target: target.output, targetId: target.targetId, turnOn }, { onSettled: () => setTarget(null) });
  };

  const selectSiren = () => {
    if (!data) return;
    setTarget({
      output: SignalCabinetOutput.Siren,
      targetId: null,
      title: 'Kabin sireni',
      isOn: data.sirenIsOn,
      changedAtUtc: data.sirenChangedAtUtc,
      stateLabel: onOffLabel(data.sirenIsOn, 'çalıyor', 'kapalı'),
      onLabel: 'Çal',
      offLabel: 'Sustur'
    });
  };

  const selectLight = () => {
    if (!outer) return;
    setTarget({
      output: SignalCabinetOutput.OuterDoorLight,
      targetId: outer.id,
      title: `${outer.name} aydınlatması`,
      isOn: outer.lightIsOn,
      changedAtUtc: outer.lightChangedAtUtc,
      stateLabel: onOffLabel(outer.lightIsOn, 'yanıyor', 'sönük'),
      onLabel: 'Yak',
      offLabel: 'Söndür'
    });
  };

  const selectInnerDoor = (door: SignalInnerDoorLiveDto) => {
    setTarget({
      output: SignalCabinetOutput.InnerDoorLock,
      targetId: door.id,
      title: `${door.name} kilidi`,
      isOn: door.isUnlocked,
      changedAtUtc: door.lockChangedAtUtc,
      stateLabel: onOffLabel(door.isUnlocked, 'açık', 'kilitli'),
      onLabel: 'Kilidi Aç',
      offLabel: 'Kilitle',
      warning: door.isOpen === true ? 'Kapı şu an açık görünüyor; kilit dili açık kapıya sürülürse mandal zorlanabilir.' : undefined
    });
  };

  if (error) {
    return (
      <div className='flex flex-col gap-4 p-4'>
        <BackLink cabinetId={cabinetId} />
        <p className='text-sm text-destructive'>{error.message}</p>
      </div>
    );
  }

  if (isPending) {
    return (
      <div className='flex flex-col gap-4 p-4'>
        <BackLink cabinetId={cabinetId} />
        <Skeleton className='h-[70vh] w-full rounded-xl' />
      </div>
    );
  }

  if (!data || !outer) {
    return (
      <div className='flex flex-col gap-4 p-4'>
        <BackLink cabinetId={cabinetId} />
        <p className='text-sm text-muted-foreground'>Bu dış kapı bulunamadı; yapılandırmadan çıkarılmış olabilir.</p>
      </div>
    );
  }

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <BackLink cabinetId={cabinetId} />
          <h1 className='text-lg font-semibold'>
            {data.cabinetName} · {outer.name}
          </h1>
          <p className='text-sm text-muted-foreground'>Cihaza tıklayarak komut gönderin. Durumlar sahadan canlı gelir.</p>
        </div>

        {!data.isEnabled && (
          <Badge variant='outline' className='text-amber-700 dark:text-amber-400'>
            Sinyalizasyon pasif
          </Badge>
        )}
      </div>

      <div className='flex flex-col gap-4 lg:flex-row'>
        <div className='mx-auto aspect-square w-full max-w-[min(100%,calc(100vh-14rem))] lg:mx-0'>
          <CabinetShell
            className='h-full w-full'
            camera={<CameraFigure cameraName={outer.cameraName} onOpen={outer.cameraId ? () => navigate(`/cameras/${outer.cameraId}`) : undefined} />}
            led={<LedFigure isOn={outer.lightIsOn === true} onActivate={outer.lightIoChannelId ? selectLight : undefined} />}
            siren={<SirenFigure isOn={data.sirenIsOn === true} onActivate={data.sirenIoChannelId ? selectSiren : undefined} />}
            indoors={<IndoorGrid doors={outer.innerDoors} onSelect={selectInnerDoor} />}
          />
        </div>

        <aside className='flex w-full shrink-0 flex-col gap-3 lg:w-80'>
          <OuterDoorPanel door={outer} />

          <StateRow
            icon={<SirenIcon className={cn('size-4', data.sirenIsOn === true && 'text-destructive')} />}
            title='Siren'
            value={data.sirenIoChannelId ? onOffLabel(data.sirenIsOn, 'Çalıyor', 'Kapalı') : 'Tanımlı değil'}
            changedAtUtc={data.sirenChangedAtUtc}
            emphasize={data.sirenIsOn === true}
          />

          <StateRow
            icon={<LightbulbIcon className={cn('size-4', outer.lightIsOn === true && 'text-amber-500')} />}
            title='Aydınlatma'
            value={outer.lightIoChannelId ? onOffLabel(outer.lightIsOn, 'Yanıyor', 'Sönük') : 'Tanımlı değil'}
            changedAtUtc={outer.lightChangedAtUtc}
          />

          <StateRow
            icon={<CameraIcon className='size-4' />}
            title='Kamera'
            value={outer.cameraName ?? 'Tanımlı değil'}
            action={
              outer.cameraId ? (
                <Button size='xs' variant='outline' nativeButton={false} render={<Link to={`/cameras/${outer.cameraId}`} />}>
                  Canlı izle
                </Button>
              ) : undefined
            }
          />

          <div className='space-y-2'>
            <p className='text-xs font-medium tracking-wide text-muted-foreground uppercase'>İç kapılar</p>
            {outer.innerDoors.length === 0 ? (
              <p className='text-xs text-muted-foreground italic'>Tanımlı iç kapı yok.</p>
            ) : (
              outer.innerDoors.map(door => <InnerDoorPanel key={door.id} door={door} />)
            )}
          </div>
        </aside>
      </div>

      <CommandDialog target={target} isPending={command.isPending} onClose={() => setTarget(null)} onSend={send} />
    </div>
  );
}

function BackLink({ cabinetId }: { cabinetId: string }) {
  return (
    <Link to={`/signalization/virtual-cabinet/${cabinetId}`} className='inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground'>
      <ArrowLeftIcon className='size-3.5' />
      Dış kapılar
    </Link>
  );
}

/** Dış kapının kilidi yoktur: yalnızca izlenir. */
function OuterDoorPanel({ door }: { door: SignalOuterDoorLiveDto }) {
  return (
    <StateRow
      icon={<DoorIcon isOpen={door.isOpen} />}
      title='Dış kapı'
      value={doorStateLabel(door.isOpen)}
      changedAtUtc={door.switchChangedAtUtc}
      emphasize={door.isOpen === true}
      hint={door.name}
    />
  );
}

function InnerDoorPanel({ door }: { door: SignalInnerDoorLiveDto }) {
  // Kapı açık ama kilit kanalı "kilitli": kilide komut gitmeden açılmış.
  const isForced = door.isOpen === true && door.isUnlocked === false;

  return (
    <div className='space-y-2 rounded-xl border p-3'>
      <div className='flex items-center justify-between gap-2'>
        <span className='truncate text-sm font-medium' title={door.name}>
          {door.name}
        </span>
        {door.authorityName && (
          <Badge variant='outline' className='shrink-0'>
            {door.authorityName}
          </Badge>
        )}
      </div>

      <StateLine icon={<DoorIcon isOpen={door.isOpen} />} label={doorStateLabel(door.isOpen)} changedAtUtc={door.switchChangedAtUtc} />
      <StateLine icon={<LockStateIcon isUnlocked={door.isUnlocked} />} label={lockStateLabel(door.isUnlocked)} changedAtUtc={door.lockChangedAtUtc} />
      {isForced && <p className='text-xs text-destructive'>Zorlanmış açılış: kapı kilide komut gitmeden açık.</p>}
    </div>
  );
}

function LockStateIcon({ isUnlocked }: { isUnlocked: boolean | null }) {
  if (isUnlocked === null) return <HelpCircleIcon className='size-4 text-muted-foreground' />;
  return isUnlocked ? <LockOpenIcon className='size-4 text-amber-600' /> : <LockIcon className='size-4 text-emerald-600' />;
}

function lockStateLabel(isUnlocked: boolean | null): string {
  if (isUnlocked === null) return 'Kilit durumu bilinmiyor';
  return isUnlocked ? 'Kilit açık' : 'Kilitli';
}

/** Çıkış durumunun okunuşu; `null` = hiç başarılı komut görmemiş. */
function onOffLabel(isOn: boolean | null, onLabel: string, offLabel: string): string {
  if (isOn === null) return 'bilinmiyor';
  return isOn ? onLabel : offLabel;
}

function StateRow({
  icon,
  title,
  value,
  changedAtUtc,
  emphasize,
  hint,
  action
}: {
  icon: ReactNode;
  title: string;
  value: string;
  changedAtUtc?: string | null;
  emphasize?: boolean;
  hint?: string;
  action?: ReactNode;
}) {
  return (
    <div className={cn('flex items-start justify-between gap-2 rounded-xl border p-3', emphasize && 'ring-1 ring-destructive/40')}>
      <div className='min-w-0 space-y-1'>
        <p className='text-xs font-medium tracking-wide text-muted-foreground uppercase'>{title}</p>
        <p className='flex items-center gap-1.5 text-sm'>
          {icon}
          <span className='truncate'>{value}</span>
        </p>
        {hint && <p className='truncate text-xs text-muted-foreground'>{hint}</p>}
        {changedAtUtc !== undefined && <Since changedAtUtc={changedAtUtc} />}
      </div>
      {action}
    </div>
  );
}

function StateLine({ icon, label, changedAtUtc }: { icon: ReactNode; label: string; changedAtUtc: string | null }) {
  return (
    <div className='flex items-center justify-between gap-2 text-xs'>
      <span className='flex items-center gap-1.5'>
        {icon}
        {label}
      </span>
      <Since changedAtUtc={changedAtUtc} />
    </div>
  );
}

/**
 * "Ne zamandır bu durumda". Saniyede bir ilerler; mutlak damga `title`'dadır.
 *
 * `toUtcDate` zorunlu: sunucunun `...Utc` damgalarında `Z` soneki yok, çıplak `new Date(damga)` değeri saat farkı
 * kadar kaydırırdı.
 */
function Since({ changedAtUtc }: { changedAtUtc: string | null }) {
  const now = useNow();
  const date = toUtcDate(changedAtUtc);

  if (!date) return <span className='text-xs text-muted-foreground'>—</span>;

  return (
    <span className='font-mono text-xs tabular-nums text-muted-foreground' title={formatUtcDateTime(changedAtUtc)}>
      {formatDuration((now - date.getTime()) / 1000)}
    </span>
  );
}

function DoorIcon({ isOpen }: { isOpen: boolean | null }) {
  if (isOpen === null) return <HelpCircleIcon className='size-4 text-muted-foreground' />;
  return isOpen ? <DoorOpenIcon className='size-4 text-destructive' /> : <DoorClosedIcon className='size-4' />;
}

function doorStateLabel(isOpen: boolean | null): string {
  if (isOpen === null) return 'Anahtar okunamıyor';
  return isOpen ? 'Açık' : 'Kapalı';
}
