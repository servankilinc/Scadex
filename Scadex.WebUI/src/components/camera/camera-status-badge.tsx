import { Badge } from '@/components/ui/badge';
import { DeviceStatus, deviceStatusLabel } from '@/models/enums/entityEnums';
import type { CameraDto } from '@/models/camera';
import { formatUtcDateTime } from '@/lib/utils';

/**
 * Kameranın izleme durumu rozeti.
 *
 * `deviceStatusId === null` ile `Offline` (0) AYNI ŞEY DEĞİL: ilki "bilinmiyor" (henüz yoklanmadı ya da izleme
 * kapalı), ikincisi "yoklandı ve ulaşılamadı". İkisini tek etikete indirmek, çalışmayan bir yoklamayı ölü bir
 * kameradan ayırt edilemez kılardı. Etiket DB'deki İngilizce lookup adı değil, arayüzün tek etiket kaynağıdır
 * (`deviceStatusLabel`).
 */
export function CameraStatusBadge({ camera }: { camera: CameraDto }) {
  if (camera.deviceStatusId == null) {
    return <Badge variant='outline'>{deviceStatusLabel(null)}</Badge>;
  }

  const status = camera.deviceStatusId as DeviceStatus;
  const variant = status === DeviceStatus.Online ? 'default' : status === DeviceStatus.Offline || status === DeviceStatus.Critical ? 'destructive' : 'secondary';

  return (
    // Sunucu damgası `Z`'siz gelir; çıplak `new Date` onu yerel saat sayıp kaydırırdı.
    <Badge variant={variant} title={camera.lastSeen ? `Son görülme: ${formatUtcDateTime(camera.lastSeen)}` : undefined}>
      {deviceStatusLabel(status)}
    </Badge>
  );
}
