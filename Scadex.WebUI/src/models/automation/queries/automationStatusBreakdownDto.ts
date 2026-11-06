/** Ayna: Scadex.Model/Dtos/Automation/Queries/AutomationStatusBreakdownDto.cs — `GET /api/Automation/{id}/status-breakdown`. */
import type { DeviceStatus } from '@/models/enums';

/**
 * Otomasyon durumunun dökümü: otomasyon bu renkte ÇÜNKÜ hangi cihaz (kameralar dahil) ne katkı veriyor. Katkılar sunucuda durumu hesaplayan
 * kuralın AYNISIYLA üretilir — ön yüz kuralı kopyalamaz, yalnızca `contribution`'ı okur. `...` damgalarında `Z` yok.
 */
export interface AutomationStatusBreakdownDto {
  automationId: string;
  /** Otomasyonun saklı durumu (haritadaki renk). `null` = bilinmiyor. */
  statusId: DeviceStatus | null;
  /** Bakım elle verilmiş yapışkan karardır; hesaplama bu otomasyona dokunmaz. */
  isMaintenance: boolean;
  /** Aktif kontrol modülü yoksa otomasyon hiçbir zaman Çevrimiçi olamaz. */
  hasControlModule: boolean;
  lastSeen: string | null;
  scadaLastIngestAt: string | null;
  /** Katkı şiddeti azalan, kontrol modülü önce, ada göre. */
  items: AutomationStatusContributorDto[];
}

export interface AutomationStatusContributorDto {
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
  /** Otomasyona YANSIYAN durum (kuralın çıktısı). `null` = otomasyona katkısı yok. */
  contribution: DeviceStatus | null;
}
