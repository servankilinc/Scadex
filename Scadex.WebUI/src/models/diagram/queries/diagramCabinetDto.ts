/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramCabinetDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { DeviceStatus } from '@/models/enums';

/**
 * Diyagram başlığındaki kabin özeti.
 * Konum/IP gibi yönetim alanları burada YOK — onlar `CabinetDetailDto`'nun işi.
 */
export interface DiagramCabinetDto {
  id: string;
  name: string;
  companyId: string;
  /** Null = hiç telemetri alınmadı. 0 DEĞİL — 0 `DeviceStatus.Offline`'dır. */
  deviceStatusId: DeviceStatus | null;
  deviceStatusName: string | null;
  lastSeen: string | null;
  isActive: boolean;
  scadaIsEnabled: boolean;
  /** Başlıkta telemetri tazeliği göstergesi için. */
  scadaLastIngestAt: string | null;
}
