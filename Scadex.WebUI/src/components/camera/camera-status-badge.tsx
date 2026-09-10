import { Badge } from '@/components/ui/badge';
import { DeviceStatus, DeviceStatusLabels } from '@/models/enums/entityEnums';
import type { CameraDto } from '@/models/camera';

/**
 * Kameranın izleme durumu rozeti.
 *
 * `deviceStatusId === null` ile `Offline` (0) AYNI ŞEY DEĞİL: ilki "hiç
 * yoklanmadı", ikincisi "yoklandı ve ulaşılamadı". İkisini tek etikete
 * indirmek, çalışmayan bir yoklayıcıyı ölü bir kameradan ayırt edilemez kılardı
 * — nitekim bugün yoklamayı yapan servis henüz yazılmadığı için TÜM kameralar
 * "Yoklanmadı" görünür ve bu doğru bilgidir.
 */
export function CameraStatusBadge({ camera }: { camera: CameraDto }) {
  if (camera.deviceStatusId == null) {
    return <Badge variant='outline'>Yoklanmadı</Badge>;
  }

  const status = camera.deviceStatusId as DeviceStatus;
  const label = camera.deviceStatusName ?? DeviceStatusLabels[status] ?? 'Bilinmiyor';
  const variant = status === DeviceStatus.Online ? 'default' : status === DeviceStatus.Offline || status === DeviceStatus.Critical ? 'destructive' : 'secondary';

  return (
    <Badge variant={variant} title={camera.lastSeen ? `Son görülme: ${new Date(camera.lastSeen).toLocaleString('tr-TR')}` : undefined}>
      {label}
    </Badge>
  );
}
