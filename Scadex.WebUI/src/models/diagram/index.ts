// Komutlar: zod YOK — gövdeleri editör üretir, bkz. dosyanın başlığı.
export type {
  CameraDraft,
  ControlCardDraft,
  PcDeviceDraft,
  DeviceUpdateRequest,
  DeviceCreateRequest,
  ConnectionUpdateRequest,
  ConnectionCreateRequest,
  AnnotationUpdateRequest,
  AnnotationCreateRequest,
  DeviceLayoutItem,
  AnnotationLayoutItem,
  DiagramLayoutRequest,
  DiagramDeleteRequest
} from './commands/diagramCommands';
export { SCADA_MIN_TIMEOUT_MS } from './commands/diagramCommands';

// Queries — sunucu ciktisi, saf interface
export type { PointDto } from './queries/pointDto';
export type { DiagramDto } from './queries/diagramDto';
export type { DiagramAutomationDto } from './queries/diagramAutomationDto';
export type { DiagramDeviceDto, DiagramCameraDto, DiagramControlCardDto, DiagramPcDeviceDto } from './queries/diagramDeviceDto';
export type { DiagramTemplateDto } from './queries/diagramTemplateDto';
export type { DiagramPinDto } from './queries/diagramPinDto';
export type { DiagramIoChannelDto } from './queries/diagramIoChannelDto';
export type { DiagramConnectionDto } from './queries/diagramConnectionDto';
export type { DiagramAnnotationItemDto } from './queries/diagramAnnotationItemDto';
export type { DiagramCanvasSettingsDto } from './queries/diagramCanvasSettingsDto';
