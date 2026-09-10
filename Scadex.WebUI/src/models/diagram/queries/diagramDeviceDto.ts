/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramDeviceDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { DeviceStatus } from '@/models/enums';
import type { DiagramIoChannelDto } from './diagramIoChannelDto';
import type { DiagramPinDto } from './diagramPinDto';
import type { DiagramTemplateDto } from './diagramTemplateDto';

/**
 * Canvas'ta bir React Flow node'u olarak render edilen cihaz.
 * `position` = (coordinateX, coordinateY), `draggable` = !isLocked,
 * `hidden` = !isVisible, boyut `template`'ten gelir.
 */
export interface DiagramDeviceDto {
  id: string;
  name: string;
  coordinateX: number;
  coordinateY: number;
  /** Derece. React Flow'da rotation prop'u yok — node kökünde CSS transform. */
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
  isActive: boolean;
  componentTemplateId: string;
  /** SCADA tarafındaki kimlik; ingest bu kodla cihaz çözümler. */
  externalCode: string | null;
  /** Null = hiç telemetri alınmadı. 0 DEĞİL — 0 `DeviceStatus.Offline`'dır. */
  deviceStatusId: DeviceStatus | null;
  deviceStatusName: string | null;
  lastSeen: string | null;
  template: DiagramTemplateDto;
  pins: DiagramPinDto[];
  ioChannels: DiagramIoChannelDto[];
}
