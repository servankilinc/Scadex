/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramAutomationDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { DeviceStatus } from '@/models/enums';

/**
 * Diyagram başlığındaki otomasyon özeti.
 * Konum/IP gibi yönetim alanları burada YOK — onlar `AutomationDetailDto`'nun işi.
 */
export interface DiagramAutomationDto {
  id: string;
  name: string;
  companyId: string;
  /** Null = bilinmiyor (canlılık kanıtı yok; arayüzde `deviceStatusLabel`). 0 DEĞİL — 0 `DeviceStatus.Offline`'dır. */
  deviceStatusId: DeviceStatus | null;
  deviceStatusName: string | null;
  lastSeen: string | null;
  isActive: boolean;
  scadaIsEnabled: boolean;
  /** Başlıkta telemetri tazeliği göstergesi için. */
  scadaLastIngestAt: string | null;
}
