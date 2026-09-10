/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramDeviceDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { DeviceStatus } from '@/models/enums';
import type { DiagramIoChannelDto } from './diagramIoChannelDto';
import type { DiagramPinDto } from './diagramPinDto';
import type { DiagramTemplateDto } from './diagramTemplateDto';

/**
 * Canvas'ta bir React Flow node'u olarak render edilen cihaz.
 * `position` = (coordinateX, coordinateY), `draggable` = !isLocked,
 * `hidden` = !isVisible, boyut `width ?? template.width` (bkz. `deviceSize`).
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
  /**
   * Cihaz bazlı boyut override'ı. `null` = şablonun ölçüsü geçerli — bir varsayılan
   * kopyası DEĞİL: şablon sonradan büyürse override'sız cihazlar onunla birlikte
   * büyür. Opsiyonel (`?`) değil, çünkü sunucu null alanları gövdeden düşürmüyor.
   */
  width: number | null;
  height: number | null;
  componentTemplateId: string;
  /** SCADA tarafındaki kimlik; YALNIZCA GÖSTERİM içindir, çözümlemede kullanılmaz. */
  externalCode: string | null;
  /**
   * Kontrol modüllerinde SCADA ingest'inin kabini çözdüğü adres; kanal ise
   * `(direction, channelNumber)` çiftinden çözülür.
   */
  macAddress: string | null;
  ipAddress: string | null;
  /** Null = hiç telemetri alınmadı. 0 DEĞİL — 0 `DeviceStatus.Offline`'dır. */
  deviceStatusId: DeviceStatus | null;
  deviceStatusName: string | null;
  lastSeen: string | null;
  template: DiagramTemplateDto;
  pins: DiagramPinDto[];
  ioChannels: DiagramIoChannelDto[];
}
