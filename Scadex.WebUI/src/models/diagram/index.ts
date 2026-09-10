// Bulk save gövdesi: zod YOK — makine tarafından üretilir, bkz. dosyanın başlığı.
export {
  emptyDelta,
  isDeltaEmpty,
  isSaveRequestEmpty,
  type EntityDelta,
  type DiagramSaveRequest,
  type DeviceDraft,
  type DevicePinDraft,
  type DeviceIoChannelDraft,
  type ConnectionDraft,
  type AnnotationDraft
} from './commands/diagramSaveRequest';

// Queries — sunucu ciktisi, saf interface
export type { PointDto } from './queries/pointDto';
export type { DiagramDto } from './queries/diagramDto';
export type { DiagramCabinetDto } from './queries/diagramCabinetDto';
export type { DiagramDeviceDto } from './queries/diagramDeviceDto';
export type { DiagramTemplateDto } from './queries/diagramTemplateDto';
export type { DiagramPinDto } from './queries/diagramPinDto';
export type { DiagramIoChannelDto } from './queries/diagramIoChannelDto';
export type { DiagramConnectionDto } from './queries/diagramConnectionDto';
export type { DiagramAnnotationItemDto } from './queries/diagramAnnotationItemDto';
export type { DiagramCanvasSettingsDto } from './queries/diagramCanvasSettingsDto';
