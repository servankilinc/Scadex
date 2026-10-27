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

const cabinetRoute = (cabinetId: string) => `${DIAGRAM_ROUTE}/cabinet/${cabinetId}`;

/** Editörün açılışı için gereken her şey tek istekte — canvas ayarları dahil, palet HARİÇ. */
export async function getCabinetDiagram(cabinetId: string): Promise<DiagramDto> {
  return http.get<DiagramDto>(cabinetRoute(cabinetId));
}

/** Yanıt, sunucunun şablondan ürettiği pin ve kanalları taşır — tuval cihazı bundan kurar. */
export async function createDevice(cabinetId: string, request: DeviceCreateRequest): Promise<DiagramDeviceDto> {
  return http.post<DiagramDeviceDto>(`${cabinetRoute(cabinetId)}/devices`, request);
}

export async function updateDevice(cabinetId: string, deviceId: string, request: DeviceUpdateRequest): Promise<void> {
  return http.put(`${cabinetRoute(cabinetId)}/devices/${deviceId}`, request);
}

export async function createConnection(cabinetId: string, request: ConnectionCreateRequest): Promise<void> {
  return http.post(`${cabinetRoute(cabinetId)}/connections`, request);
}

export async function updateConnection(cabinetId: string, connectionId: string, request: ConnectionUpdateRequest): Promise<void> {
  return http.put(`${cabinetRoute(cabinetId)}/connections/${connectionId}`, request);
}

export async function createAnnotation(cabinetId: string, request: AnnotationCreateRequest): Promise<void> {
  return http.post(`${cabinetRoute(cabinetId)}/annotations`, request);
}

export async function updateAnnotation(cabinetId: string, annotationId: string, request: AnnotationUpdateRequest): Promise<void> {
  return http.put(`${cabinetRoute(cabinetId)}/annotations/${annotationId}`, request);
}

export async function updateLayout(cabinetId: string, request: DiagramLayoutRequest): Promise<void> {
  return http.put(`${cabinetRoute(cabinetId)}/layout`, request);
}

export async function deleteElements(cabinetId: string, request: DiagramDeleteRequest): Promise<void> {
  return http.post(`${cabinetRoute(cabinetId)}/delete`, request);
}
