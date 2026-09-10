import { useSyncExternalStore } from 'react';
import { AlertTriangleIcon, LoaderCircleIcon, RadioIcon, WifiOffIcon } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { HubStatus } from '@/lib/signalr/diagram-hub';

/**
 * Canlı bağlantının durumu ve telemetrinin TAZELİĞİ.
 *
 * İkisi ayrı sorulardır ve ikisi de gösterilmek zorunda:
 *   - **Bağlantı**: tarayıcı hub'a bağlı mı? Değilse ekrandaki her sayı donmuştur.
 *   - **Tazelik**: bağlıyız ama SCADA ne zamandır veri göndermiyor?
 *
 * Yalnızca birincisi gösterilseydi, hub'a bağlı ama SCADA'sı susmuş bir kabin
 * "canlı" görünürdü — operatörün göreceği en yanıltıcı durum.
 */

const STATUS_TEXT: Record<HubStatus, string> = {
  connected: 'Canlı',
  connecting: 'Bağlanıyor…',
  reconnecting: 'Yeniden bağlanıyor…',
  disconnected: 'Bağlantı yok'
};

export function LiveIndicator({ status, lastIngestAt }: { status: HubStatus; lastIngestAt: string | null }) {
  const age = useAge(lastIngestAt, status === 'connected');

  const Icon = status === 'connected' ? RadioIcon : status === 'disconnected' ? WifiOffIcon : LoaderCircleIcon;

  return (
    <span
      className={cn(
        'flex items-center gap-1.5 text-xs',
        status === 'connected' ? 'text-muted-foreground' : status === 'disconnected' ? 'text-destructive' : 'text-amber-600 dark:text-amber-500'
      )}
      title={lastIngestAt ? `Son telemetri: ${new Date(lastIngestAt).toLocaleString('tr-TR')}` : 'Bu kabinden hiç telemetri alınmadı'}>
      <Icon className={cn('size-3.5', status !== 'connected' && status !== 'disconnected' && 'animate-spin')} />
      {status === 'connected' ? age : STATUS_TEXT[status]}
    </span>
  );
}

/** Kopukluk şeridi — yalnızca gerçekten kopukken çizilir. */
export function LiveDisconnectedBanner({ status }: { status: HubStatus }) {
  if (status === 'connected' || status === 'connecting') return null;

  return (
    <div
      className={cn(
        'flex items-center gap-2 border-b px-3 py-1.5 text-xs',
        status === 'reconnecting' ? 'bg-amber-500/10 text-amber-700 dark:text-amber-400' : 'bg-destructive/10 text-destructive'
      )}>
      <AlertTriangleIcon className='size-3.5 shrink-0' />
      {status === 'reconnecting'
        ? 'Canlı bağlantı koptu, yeniden kuruluyor. Ekrandaki değerler güncel olmayabilir.'
        : 'Canlı bağlantı yok. Diyagram düzenlenebilir ama telemetri güncellenmiyor.'}
    </div>
  );
}

/**
 * 5 saniyede bir ilerleyen ortak saat.
 *
 * `Date.now()` RENDER SIRASINDA ÇAĞRILAMAZ — saf olmayan bir çağrıdır ve aynı
 * girdiyle farklı çıktı üretir (lint de reddediyor). Bunun yerine zaman, dışarıda
 * ilerleyen ve değiştiğinde haber veren bir değere dönüştürülür; `getSnapshot`
 * yalnızca son okunan sayıyı döndürdüğü için saf kalır.
 *
 * Zamanlayıcı ABONE VARKEN işler: diyagram kapalıyken arka planda sayaç
 * döndürmenin anlamı yok.
 */
let clockNow = Date.now();
const clockListeners = new Set<() => void>();
let clockTimer: number | null = null;

function subscribeClock(listener: () => void): () => void {
  clockListeners.add(listener);

  if (clockTimer === null) {
    clockNow = Date.now();
    clockTimer = window.setInterval(() => {
      clockNow = Date.now();
      for (const l of clockListeners) l();
    }, 5000);
  }

  return () => {
    clockListeners.delete(listener);
    if (clockListeners.size === 0 && clockTimer !== null) {
      window.clearInterval(clockTimer);
      clockTimer = null;
    }
  };
}

/**
 * "12 sn önce" metni. Saat ilerlediği için bileşenin kendiliğinden yeniden render
 * olması gerekir — veri değişmese bile metin eskir.
 */
function useAge(timestamp: string | null, enabled: boolean): string {
  // Kopukken sayaç durur: banner zaten görünür ve saniye saymak yalnızca gürültü.
  const subscribe = enabled && timestamp ? subscribeClock : NEVER;
  const now = useSyncExternalStore(subscribe, getClock, getClock);

  if (!timestamp) return 'Telemetri yok';

  const seconds = Math.max(0, Math.round((now - new Date(timestamp).getTime()) / 1000));
  if (seconds < 10) return 'Canlı';
  if (seconds < 60) return `${seconds} sn önce`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)} dk önce`;
  return `${Math.floor(seconds / 3600)} sa önce`;
}

function getClock(): number {
  return clockNow;
}

/** Sayacın istenmediği durum. Sabit referans: her render yenisi üretilmesin. */
const NEVER = () => () => {};
