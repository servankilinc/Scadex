/** Ayna: Scadex.Model/Dtos/Diagram/Queries/PointDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */

/**
 * Canvas üzerinde tek bir nokta.
 *
 * Birim, `DiagramDeviceDto.coordinateX/Y` ile AYNI koordinat uzayıdır (React Flow
 * akış koordinatları) — piksel değil, ekran koordinatı değil.
 */
export interface PointDto {
  x: number;
  y: number;
}
