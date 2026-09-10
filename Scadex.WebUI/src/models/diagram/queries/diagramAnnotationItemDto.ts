/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramAnnotationItemDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { AnnotationShape } from '@/models/enums';

/**
 * Cihaz olmayan diyagram elemanı (serbest metin, kutu, not).
 * `cabinetId`/`cabinetName` taşımaz — aggregate zaten tek bir kabine ait.
 */
export interface DiagramAnnotationItemDto {
  id: string;
  name: string;
  coordinateX: number;
  coordinateY: number;
  width: number;
  height: number;
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
  text: string;
  shape: AnnotationShape;
  /** CSS renk dizesi. */
  backgroundColor: string;
  fontColor: string;
  fontSize: number;
  isBold: boolean;
  borderColor: string;
}
