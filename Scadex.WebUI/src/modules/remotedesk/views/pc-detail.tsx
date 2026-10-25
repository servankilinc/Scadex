import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { ArrowLeftIcon, ArrowLeftRightIcon, ChevronDownIcon, ChevronUpIcon, ClipboardCopyIcon, ClipboardListIcon, ClipboardPasteIcon, EyeIcon, LoaderIcon, LockIcon, MaximizeIcon, MonitorIcon, MonitorOffIcon, MousePointerClickIcon, TriangleAlertIcon, VideoOffIcon } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useCurrentUser, usePermission } from '@/lib/auth-session';
import { cn, formatUtcDateTime } from '@/lib/utils';
import type { Permission } from '@/models/enums/entityEnums';
import { remoteDeskKeys } from '../api/query-keys';
import { usePc } from '../hooks/use-pcs';
import { usePcStream } from '../hooks/use-pc-stream';
import { useRemoteControl } from '../hooks/use-remote-control';
import { useFileTransfer } from '../hooks/use-file-transfer';
import { FileTransferDialog } from '../components/file-transfer-dialog';
import { CLIPBOARD_MAX_CHARS, ClipboardDialog } from '../components/clipboard-dialog';
import { ControlInputState, ControlInputStateLabels, type PcControlDto } from '../models/control';
import { ScreenStreamStateLabels, type PcDetailDto, type PcMonitorDto } from '../models/pc';

/**
 * PC ekranı — `/remote-desk/pcs/:deviceId`: monitör seçici + oynatıcı.
 *
 * Aynı PC'yi (aynı ya da farklı monitörlerini) istenen sayıda kullanıcı aynı anda izleyebilir; aynı monitörün izleyicileri PC'deki
 * tek yayını paylaşır. Monitör değiştirmek eski kiralamayı bırakıp yenisini açar (oynatıcı `key` ile yeniden kurulur).
 *
 * Uzaktan kontrol (fare Faz 8, klavye Faz 9): `RemotePcControl` izni olan kullanıcı izlerken "Kontrolü al" der; kontrol PC başına tek
 * kullanıcıdadır, diğerleri izlemeye devam eder. Monitör değiştirmek ya da ekrandan çıkmak kontrolü bırakır.
 */

/** Tarayıcının yakalayamadığı tuşlar (işletim sistemi/tarayıcı önce alır) — `KeyboardEvent.code` dizileri. Ctrl+Alt+Del gönderilemez. */
const COMBOS: { label: string; codes: string[] }[] = [
  { label: 'Win', codes: ['MetaLeft'] },
  { label: 'Win+R', codes: ['MetaLeft', 'KeyR'] },
  { label: 'Win+D', codes: ['MetaLeft', 'KeyD'] },
  { label: 'Win+E', codes: ['MetaLeft', 'KeyE'] },
  { label: 'Alt+Tab', codes: ['AltLeft', 'Tab'] },
  { label: 'Alt+F4', codes: ['AltLeft', 'F4'] },
  { label: 'Ctrl+Esc', codes: ['ControlLeft', 'Escape'] },
  { label: 'Ctrl+Shift+Esc', codes: ['ControlLeft', 'ShiftLeft', 'Escape'] }
];

/** Keyboard Lock API (yalnızca Chromium, yalnızca tam ekranda) — TS DOM kütüphanesinde yok. */
type KeyboardLock = { lock?: (codes?: string[]) => Promise<void>; unlock?: () => void };
const keyboardLock = () => (navigator as Navigator & { keyboard?: KeyboardLock }).keyboard;

/** Tek tık pano (async Clipboard API) yalnızca güvenli bağlamda vardır; yoksa iki adımlı pencere açılır. */
const hasAsyncClipboard = () => window.isSecureContext && navigator.clipboard !== undefined;

const CONTROL_PERMISSION: keyof typeof Permission = 'RemotePcControl';
const FILE_PERMISSION: keyof typeof Permission = 'RemotePcFileTransfer';
const AUDIT_PERMISSION: keyof typeof Permission = 'RemotePcAudit';
export default function RemoteDeskPcDetail() {
  const { deviceId } = useParams();
  const pc = usePc(deviceId);
  const [selected, setSelected] = useState<number | null>(null);

  if (pc.error) return <p className='p-4 text-sm text-destructive'>{pc.error.message}</p>;
  if (!pc.data) return <Skeleton className='m-4 h-96 rounded-xl' />;

  const monitors = pc.data.monitors;
  // Seçim yoksa birincil monitör; seçilen monitör çıkarıldıysa da birincile düş.
  const monitorIndex =
    selected !== null && monitors.some(m => m.index === selected) ? selected : (monitors.find(m => m.isPrimary) ?? monitors[0])?.index;

  return (
    <div className='flex flex-col gap-4 p-4'>
      <PcHeader pc={pc.data} />

      {!pc.data.isConnected ? (
        <div className='flex flex-col items-center gap-2 rounded-xl border border-dashed p-10 text-center text-sm text-muted-foreground'>
          <MonitorOffIcon className='size-8' />
          <p>Bu PC merkeze bağlı değil.</p>
          <p className='max-w-lg'>
            {pc.data.hasCredential
              ? "PC'de Scadex RemoteDesk istemcisinin çalıştığından ve ayarlarındaki cihaz kimliğinin (DeviceId + DeviceSecret) diyagramda üretilenle aynı olduğundan emin olun. İstemci ekranı kimliğin reddedildiğini gösterir."
              : 'Bu PC için kimlik bilgisi üretilmemiş: kabin diyagramında cihazı seçip özellik panelinden sır üretin, kaydedin ve PC’deki istemciye girin.'}
          </p>
        </div>
      ) : (
        <>
          <MonitorPicker monitors={monitors} selected={monitorIndex} onSelect={setSelected} />
          {monitorIndex !== undefined && (
            <PcPlayer key={monitorIndex} deviceId={pc.data.deviceId} monitorIndex={monitorIndex} control={pc.data.control} />
          )}
        </>
      )}
    </div>
  );
}

function PcHeader({ pc }: { pc: PcDetailDto }) {
  const can = usePermission();

  return (
    <div className='flex flex-wrap items-start justify-between gap-3'>
      <div className='flex items-start gap-3'>
        <Button size='icon-sm' variant='ghost' nativeButton={false} render={<Link to='/remote-desk/pcs' title='PC listesine dön' />}>
          <ArrowLeftIcon />
        </Button>
        <div>
          <h1 className='flex items-center gap-2 text-lg font-semibold'>
            {pc.deviceName}
            {pc.isConnected ? <Badge variant='secondary'>Bağlı</Badge> : <Badge variant='outline'>Bağlı değil</Badge>}
          </h1>
          <p className='text-sm text-muted-foreground'>
            {pc.cabinetName}
            {pc.isConnected && (
              <>
                {' · '}
                {pc.machineName} ({pc.userName}) · istemci {pc.clientVersion} · bağlandı {formatUtcDateTime(pc.connectedUtc)}
              </>
            )}
          </p>
        </div>
      </div>
      <div className='flex flex-wrap gap-2'>
        {pc.control && (
          <Badge variant='outline'>
            <MousePointerClickIcon />
            {pc.control.userName} kontrol ediyor
          </Badge>
        )}
        {pc.viewerCount > 0 && (
          <Badge variant='outline'>
            <EyeIcon />
            Bu PC'yi şu an {pc.viewerCount} izleyici izliyor
          </Badge>
        )}
        {/* Rapor ayrı izindir: izleyen herkes bu PC'nin geçmişini görmez. */}
        {can(AUDIT_PERMISSION) && (
          <Button size='sm' variant='outline' nativeButton={false} render={<Link to={`/remote-desk/reports?deviceId=${pc.deviceId}`} />}>
            <ClipboardListIcon />
            Kayıtlar
          </Button>
        )}
      </div>
    </div>
  );
}

function MonitorPicker({ monitors, selected, onSelect }: { monitors: PcMonitorDto[]; selected: number | undefined; onSelect: (index: number) => void }) {
  if (monitors.length === 0) return <p className='text-sm text-muted-foreground'>PC monitör bildirmedi.</p>;

  return (
    <div className='flex flex-wrap gap-2'>
      {monitors.map(m => (
        <button
          key={m.index}
          type='button'
          onClick={() => onSelect(m.index)}
          className={cn(
            'flex min-w-44 flex-col items-start gap-0.5 rounded-lg border px-3 py-2 text-left text-sm transition-colors hover:bg-muted',
            m.index === selected && 'border-primary bg-primary/5 ring-1 ring-primary'
          )}>
          <span className='flex items-center gap-1.5 font-medium'>
            <MonitorIcon className='size-4' />
            Monitör {m.index}
            {m.isPrimary && <span className='text-xs font-normal text-muted-foreground'>(birincil)</span>}
          </span>
          <span className='text-xs text-muted-foreground'>
            {m.width}×{m.height}
            {m.streamState !== null && ` · ${ScreenStreamStateLabels[m.streamState]}`}
            {m.viewerCount > 0 && ` · ${m.viewerCount} izleyici`}
          </span>
        </button>
      ))}
    </div>
  );
}

function PcPlayer({ deviceId, monitorIndex, control }: { deviceId: string; monitorIndex: number; control: PcControlDto | null }) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const surfaceRef = useRef<HTMLDivElement | null>(null);
  const playerRef = useRef<HTMLDivElement | null>(null);
  const stream = usePcStream(videoRef, deviceId, monitorIndex);
  const remote = useRemoteControl(surfaceRef, videoRef, deviceId, monitorIndex);
  const can = usePermission();
  const queryClient = useQueryClient();

  // Dosya transferi izlemeye bağlıdır, kontrole değil (RemoteDesk.md § 12.8): yayın bağlıyken + izinle açılır, kontrol ALMAK gerekmez.
  const viewing = stream.state === 'connected';
  const canFiles = viewing && can(FILE_PERMISSION);
  const files = useFileTransfer(deviceId, canFiles);
  const [filesOpen, setFilesOpen] = useState(false);
  // Üst-orta araç çubuğu (kontrol + kısayol + dosya + pano): açılır/kapanır katman.
  const [toolbarOpen, setToolbarOpen] = useState(true);

  // Panolar arası aktarım (RemoteDesk.md § 12.9). Güvenli bağlamda tek tık navigator.clipboard; HTTPS'siz kurulumda (navigator.clipboard
  // yok) iki adımlı pencere — Ctrl+V / Ctrl+C her bağlamda çalışır (2026-10-26).
  const [clipboardDialog, setClipboardDialog] = useState<{ mode: 'export' } | { mode: 'import'; text: string } | null>(null);

  const exportClipboard = useCallback(async () => {
    if (!hasAsyncClipboard()) {
      setClipboardDialog({ mode: 'export' });
      return;
    }
    try {
      const text = await navigator.clipboard.readText();
      if (text.length > CLIPBOARD_MAX_CHARS) {
        toast.error('Pano metni çok uzun (en fazla ~1 milyon karakter).');
        return;
      }
      const res = await files.setRemoteClipboard(text);
      if (res.success) toast.success('Pano uzak PC’ye aktarıldı.');
      else toast.error(res.message ?? 'Pano aktarılamadı.');
    } catch {
      // Tarayıcı izni reddedildi: iki adımlı yola düş.
      setClipboardDialog({ mode: 'export' });
    }
  }, [files]);

  const importClipboard = useCallback(async () => {
    let res;
    try {
      res = await files.getRemoteClipboard();
    } catch {
      toast.error('Uzak pano alınamadı.');
      return;
    }
    if (!res.success) {
      toast.error(res.message ?? 'Uzak pano alınamadı.');
      return;
    }
    if (!hasAsyncClipboard()) {
      setClipboardDialog({ mode: 'import', text: res.text ?? '' });
      return;
    }
    try {
      await navigator.clipboard.writeText(res.text ?? '');
      toast.success(res.text ? 'Uzak pano yerele alındı.' : 'Uzak panoda metin yok.');
    } catch {
      setClipboardDialog({ mode: 'import', text: res.text ?? '' });
    }
  }, [files]);

  // Görüntü gelince monitör kartındaki yayın durumu/izleyici sayısı 10 sn'lik yoklamayı beklemesin.
  useEffect(() => {
    if (stream.state === 'connected') void queryClient.invalidateQueries({ queryKey: remoteDeskKeys.pc(deviceId) });
  }, [stream.state, deviceId, queryClient]);

  const controlling = remote.status === 'active';
  const canControlBar = can(CONTROL_PERMISSION) && (viewing || remote.status !== 'idle');

  // Tam ekran + Keyboard Lock: Win, Alt+Tab, Ctrl+W gibi tuşlar da sayfaya gelir (çıkış: Esc basılı tutulur).
  const toggleFullscreen = useCallback(async () => {
    const player = playerRef.current;
    if (!player) return;
    if (document.fullscreenElement) {
      await document.exitFullscreen().catch(() => undefined);
      return;
    }
    await player.requestFullscreen().catch(() => undefined);
    await keyboardLock()?.lock?.().catch(() => undefined);
    surfaceRef.current?.focus({ preventScroll: true });
  }, []);

  // Tam ekrandan çıkınca ya da kontrol bitince kilit kalkar (kilitliyken Esc tek basışta çıkmaz).
  useEffect(() => {
    const unlock = () => {
      if (!document.fullscreenElement) keyboardLock()?.unlock?.();
    };
    document.addEventListener('fullscreenchange', unlock);
    return () => document.removeEventListener('fullscreenchange', unlock);
  }, []);
  useEffect(() => {
    if (!controlling) keyboardLock()?.unlock?.();
  }, [controlling]);

  return (
    <div className='flex flex-col gap-2'>
      <div
        ref={playerRef}
        className={cn(
          'relative aspect-video max-h-[calc(100vh-14rem)] overflow-hidden rounded-xl border bg-black',
          '[&:fullscreen]:max-h-none [&:fullscreen]:rounded-none [&:fullscreen]:border-0',
          controlling && 'border-primary ring-2 ring-primary'
        )}>
        {/* `contain`: ekranın tamamı görünmeli; fare koordinatı çizilen görüntü dikdörtgenine göre hesaplanır (§ 12.3). */}
        <video ref={videoRef} autoPlay playsInline muted className='size-full object-contain' />

        {/* Kontrol katmanı: her zaman var (ref kontrol istenmeden önce hazır olsun), olayları yalnızca kontrol sizdeyken yakalar. */}
        <div
          ref={surfaceRef}
          // Klavye olayları için odak alabilmeli (tıklayınca ve kontrol verilince odaklanır).
          tabIndex={-1}
          className={cn(
            'absolute inset-0 touch-none outline-none select-none',
            controlling ? 'pointer-events-auto' : 'pointer-events-none'
          )}
        />

        {stream.state !== 'connected' && (
          <div className='pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-2 bg-black/60 px-6 text-center'>
            {stream.state === 'failed' ? (
              <>
                <VideoOffIcon className='size-6 text-white/70' />
                <p className='text-sm text-white/80'>{stream.error ?? 'Yayın açılamadı.'}</p>
              </>
            ) : (
              <>
                <LoaderIcon className='size-6 animate-spin text-white/70' />
                <p className='text-sm text-white/70'>
                  {stream.state === 'reconnecting'
                    ? (stream.error ?? 'Bağlantı koptu, yeniden deneniyor…')
                    : 'PC yayını başlatıyor… (ilk izlemede PC kodlayıcıları sınar, 20 sn kadar sürebilir)'}
                </p>
              </>
            )}
          </div>
        )}

        {/* Üst-orta araç çubuğu: kontrol + kısayol + dosya aktarımı; açılır/kapanır, yarı saydam zemin. Sarmalayıcı olayları geçirmez,
            yalnızca panel/düğme tıklanabilir (kontrol sırasında görüntünün üstünü kaplamasın). */}
        {(canControlBar || canFiles) && (
          <div className='pointer-events-none absolute inset-x-0 top-2 z-20 flex justify-center px-2'>
            {toolbarOpen ? (
              <div className='pointer-events-auto flex max-w-full flex-wrap items-center justify-center gap-2 rounded-xl border bg-background/80 px-3 py-2 text-sm shadow-lg backdrop-blur-sm'>
                {canControlBar && (
                  <ControlBar
                    status={remote.status}
                    message={remote.message}
                    inputState={remote.inputState}
                    control={control}
                    onRequest={remote.request}
                    onRelease={remote.release}
                    onCombo={remote.sendCombo}
                    onFullscreen={() => void toggleFullscreen()}
                  />
                )}
                {canFiles && (
                  <Button size='sm' variant='outline' onClick={() => setFilesOpen(true)}>
                    <ArrowLeftRightIcon />
                    Dosya aktarımı
                  </Button>
                )}
                {canFiles && (
                  <>
                    <Button size='sm' variant='outline' onClick={() => void exportClipboard()} title='Yerel panodaki metni uzak PC panosuna aktar'>
                      <ClipboardCopyIcon />
                      Dışa aktar
                    </Button>
                    <Button size='sm' variant='outline' onClick={() => void importClipboard()} title='Uzak PC panosundaki metni yerel panoya al'>
                      <ClipboardPasteIcon />
                      İçe aktar
                    </Button>
                  </>
                )}
                <Button size='icon-sm' variant='ghost' onClick={() => setToolbarOpen(false)} title='Araç çubuğunu gizle'>
                  <ChevronUpIcon />
                </Button>
              </div>
            ) : (
              <Button
                size='sm'
                variant='secondary'
                className='pointer-events-auto rounded-full border bg-background/80 shadow-lg backdrop-blur-sm'
                onClick={() => setToolbarOpen(true)}
                title='Araç çubuğunu göster'>
                <ChevronDownIcon />
                {controlling ? 'Kontrol sizde' : 'Araç çubuğu'}
              </Button>
            )}
          </div>
        )}
      </div>

      {stream.state === 'failed' && (
        <Button variant='outline' size='sm' onClick={stream.retry} className='self-start'>
          Tekrar dene
        </Button>
      )}

      {canFiles && filesOpen && <FileTransferDialog deviceId={deviceId} files={files} onClose={() => setFilesOpen(false)} />}
      {canFiles && clipboardDialog?.mode === 'export' && (
        <ClipboardDialog mode='export' onSend={files.setRemoteClipboard} onClose={() => setClipboardDialog(null)} />
      )}
      {canFiles && clipboardDialog?.mode === 'import' && (
        <ClipboardDialog mode='import' text={clipboardDialog.text} onClose={() => setClipboardDialog(null)} />
      )}
    </div>
  );
}

function ControlBar({
  status,
  message,
  inputState,
  control,
  onRequest,
  onRelease,
  onCombo,
  onFullscreen
}: {
  status: 'idle' | 'requesting' | 'active';
  message?: string;
  inputState: ControlInputState;
  control: PcControlDto | null;
  onRequest: () => void;
  onRelease: () => void;
  onCombo: (codes: string[]) => void;
  onFullscreen: () => void;
}) {
  const me = useCurrentUser();
  // Aynı kullanıcının başka sekmesi kontrol ediyorsa buradan devralınabilir; başka kullanıcıysa beklenir.
  const otherUser = status === 'idle' && control !== null && control.userId !== me?.id ? control.userName : null;

  return (
    <div className='flex flex-wrap items-center gap-3 text-sm'>
      {status === 'active' ? (
        <>
          <Button size='sm' variant='destructive' onClick={onRelease}>
            <MousePointerClickIcon />
            Kontrolü bırak
          </Button>
          {COMBOS.map(combo => (
            <Button key={combo.label} size='sm' variant='outline' onClick={() => onCombo(combo.codes)}>
              {combo.label}
            </Button>
          ))}
          <Button size='sm' variant='outline' onClick={onFullscreen} title='Tam ekranda Win, Alt+Tab gibi tuşlar da gider (çıkış: Esc basılı tutun)'>
            <MaximizeIcon />
            Tam ekran
          </Button>
          <span className='text-xs text-muted-foreground' title='Klavye için görüntüye bir kez tıklayın. Ctrl+Alt+Del gönderilemez.'>
            Klavye için tıklayın · Ctrl+Alt+Del yok
          </span>
          {inputState !== ControlInputState.Ok && <InputStateNote state={inputState} />}
        </>
      ) : (
        <>
          <Button size='sm' variant='outline' onClick={onRequest} disabled={status === 'requesting' || otherUser !== null}>
            {status === 'requesting' ? <LoaderIcon className='animate-spin' /> : <MousePointerClickIcon />}
            {status === 'requesting' ? 'Kontrol isteniyor…' : 'Kontrolü al'}
          </Button>
          {otherUser && <span className='text-muted-foreground'>{otherUser} kontrol ediyor; o bırakınca alabilirsiniz.</span>}
          {!otherUser && message && <span className='text-muted-foreground'>{message}</span>}
        </>
      )}
    </div>
  );
}

/** Kontrol sürerken PC'deki girdinin durumu (readiness P1.5): operatör "tıklamam neden gitmiyor"u görür. */
function InputStateNote({ state }: { state: Exclude<ControlInputState, 1> }) {
  const { text, tone } = ControlInputStateLabels[state];
  const Icon = tone === 'info' ? LockIcon : TriangleAlertIcon;
  return (
    <span
      role='status'
      className={cn(
        'flex basis-full items-center gap-1.5 rounded-md px-2 py-1 text-xs',
        tone === 'info' ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300' : 'bg-destructive/15 text-destructive'
      )}>
      <Icon className='size-3.5 shrink-0' />
      {text}
    </span>
  );
}
