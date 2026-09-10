/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramPinDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { HandleSide, PinDirection, PinFunction, VoltageLevel } from '@/models/enums';

/**
 * Canvas'ta bir React Flow `<Handle>` olarak render edilen pin.
 * Handle id'si doğrudan `id`'dir (1:1, sonek yok).
 */
export interface DiagramPinDto {
  id: string;
  name: string;
  /** Şablonun genişliğine göre 0..1 normalize kesir → CSS `left: ${x * 100}%`. */
  relativeX: number;
  /** Şablonun yüksekliğine göre 0..1 normalize kesir → CSS `top: ${y * 100}%`. */
  relativeY: number;
  /**
   * Handle'ın hangi kenarda duracağı. `relativeX/Y` tek başına bunu belirleyemez:
   * (0.5, 0) hem üst kenarın ortası hem sol kenarın başlangıcı olarak okunabilir.
   */
  side: HandleSide;
  function: PinFunction;
  direction: PinDirection;
  /** Null = belirtilmemiş. Bağlantı doğrulaması yalnızca İKİ UÇ da doluysa seviye karşılaştırır. */
  voltageLevel: VoltageLevel | null;
  channelNumber: number | null;
  componentTemplatePinId: string | null;
  /** Bağlı telemetri kanalı. Canlı DEĞER burada değil — SignalR'dan akar. */
  ioChannelId: string | null;
}
