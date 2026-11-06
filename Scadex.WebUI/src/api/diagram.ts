import http from '@/lib/axios-helper';
import type {
  AnnotationCreateRequest,
  AnnotationUpdateRequest,
  ConnectionCreateRequest,
  ConnectionUpdateRequest,
  DeviceCreateRequest,
  DeviceUpdateRequest,
  DiagramDeleteRequest,
  DiagramDeviceDto,
  DiagramDto,
  DiagramLayoutRequest
} from '@/models/diagram';

const DIAGRAM_ROUTE = '/api/Diagram';

const automationRoute = (automationId: string) => `${DIAGRAM_ROUTE}/automation/${automationId}`;

/** Editörün açılışı için gereken her şey tek istekte — canvas ayarları dahil, palet HARİÇ. */
export async function getAutomationDiagram(automationId: string): Promise<DiagramDto> {
  return http.get<DiagramDto>(automationRoute(automationId));
}

/** Yanıt, sunucunun şablondan ürettiği pin ve kanalları taşır — tuval cihazı bundan kurar. */
export async function createDevice(automationId: string, request: DeviceCreateRequest): Promise<DiagramDeviceDto> {
  return http.post<DiagramDeviceDto>(`${automationRoute(automationId)}/devices`, request);
}

export async function updateDevice(automationId: string, deviceId: string, request: DeviceUpdateRequest): Promise<void> {
  return http.put(`${automationRoute(automationId)}/devices/${deviceId}`, request);
}

export async function createConnection(automationId: string, request: ConnectionCreateRequest): Promise<void> {
  return http.post(`${automationRoute(automationId)}/connections`, request);
}

export async function updateConnection(automationId: string, connectionId: string, request: ConnectionUpdateRequest): Promise<void> {
  return http.put(`${automationRoute(automationId)}/connections/${connectionId}`, request);
}

export async function createAnnotation(automationId: string, request: AnnotationCreateRequest): Promise<void> {
  return http.post(`${automationRoute(automationId)}/annotations`, request);
}

export async function updateAnnotation(automationId: string, annotationId: string, request: AnnotationUpdateRequest): Promise<void> {
  return http.put(`${automationRoute(automationId)}/annotations/${annotationId}`, request);
}

export async function updateLayout(automationId: string, request: DiagramLayoutRequest): Promise<void> {
  return http.put(`${automationRoute(automationId)}/layout`, request);
}

export async function deleteElements(automationId: string, request: DiagramDeleteRequest): Promise<void> {
  return http.post(`${automationRoute(automationId)}/delete`, request);
}
