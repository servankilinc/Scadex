/** Ayna: Scadex.Model/Dtos/Cabinet/Queries/CabinetStatusBreakdownDto.cs — `GET /api/Cabinet/{id}/status-breakdown`. */
import type { DeviceStatus } from '@/models/enums';

/**
 * Kabin durumunun dökümü: kabin bu renkte ÇÜNKÜ hangi cihaz (kameralar dahil) ne katkı veriyor. Katkılar sunucuda durumu hesaplayan
 * kuralın AYNISIYLA üretilir — ön yüz kuralı kopyalamaz, yalnızca `contribution`'ı okur. `...` damgalarında `Z` yok.
 */
export interface CabinetStatusBreakdownDto {
  cabinetId: string;
  /** Kabinin saklı durumu (haritadaki renk). `null` = bilinmiyor. */
  statusId: DeviceStatus | null;
  /** Bakım elle verilmiş yapışkan karardır; hesaplama bu kabine dokunmaz. */
  isMaintenance: boolean;
  /** Aktif kontrol modülü yoksa kabin hiçbir zaman Çevrimiçi olamaz. */
  hasControlModule: boolean;
  lastSeen: string | null;
  scadaLastIngestAt: string | null;
  /** Katkı şiddeti azalan, kontrol modülü önce, ada göre. */
  items: CabinetStatusContributorDto[];
}

export interface CabinetStatusContributorDto {
  id: string;
  name: string;
  /** Cihazın şablon adı. */
  templateName: string | null;
  isControlModule: boolean;
  isMonitoringEnabled: boolean;
  ipAddress: string | null;
  monitoringPort: number | null;
  pingIntervalSec: number;
  /** Kaydın KENDİ durumu. `null` = bilinmiyor. */
  statusId: DeviceStatus | null;
  lastSeen: string | null;
  lastConnectionError: string | null;
  /** Kabine YANSIYAN durum (kuralın çıktısı). `null` = kabine katkısı yok. */
  contribution: DeviceStatus | null;
}
