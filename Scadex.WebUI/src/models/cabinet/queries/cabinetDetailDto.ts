/** Ayna: Scadex.Model/Dtos/Cabinet/Queries/CabinetDetailDto.cs */
import type { DeviceStatus } from '@/models/enums';

export interface CabinetDetailDto {
  id: string;
  name: string;
  companyId: string;
  companyName: string;
  latitude: number | null;
  longitude: number | null;
  locationDescription: string | null;
  gsmIp: string | null;
  networkIp: string | null;
  /**
   * Null = kabin hiç telemetri almadı. 0 DEĞİL.
   * Alan bir zamanlar non-nullable'dı ve NULL 0'a düşüyordu; 0 =
   * `DeviceStatus.Offline` olduğu için "bilinmiyor" durumu "ÇEVRİMDIŞI" diye
   * raporlanıyordu. Bunu `docs/api-contract/samples/CabinetDetailDto.NoTelemetry.json`
   * golden'ı koruyor.
   */
  deviceStatusId: DeviceStatus | null;
  deviceStatusName: string | null;
  createdBy: string | null;
  updatedBy: string | null;
  createDateUtc: string | null;
  updateDateUtc: string | null;
  isActive: boolean;
}
