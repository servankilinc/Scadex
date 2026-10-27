/** Ayna: Scadex.Model/Dtos/ComponentTemplate/Queries/ComponentTemplatePalettePinDto.cs — sözleşme: docs/api-contract/10-component-template.md */
import type { HandleSide, PinDirection, PinFunction, VoltageLevel } from '@/models/enums';

/**
 * Palet şablonunun pin şemasındaki tek bir pin.
 *
 * Cihaz bırakılınca gerçek pinleri SUNUCU bu şemadan üretir ve oluşturma
 * yanıtında döndürür (`POST …/devices`, 2026-10-27); istemci pin üretmez.
 */
export interface ComponentTemplatePalettePinDto {
  /**
   * ŞABLON pininin Id'si — cihaz pininin Id'si değildir. Üretilen pin bunu
   * `componentTemplatePinId` olarak taşır ve gönderide sunucuya bu eşleme gider.
   */
  id: string;
  name: string;
  /** Şablonun genişliğine göre 0..1 normalize kesir. */
  relativeX: number;
  /** Şablonun yüksekliğine göre 0..1 normalize kesir. */
  relativeY: number;
  side: HandleSide;
  function: PinFunction;
  direction: PinDirection;
  /** Null = belirtilmemiş. Bağlantı doğrulaması yalnızca İKİ UÇ da doluysa karşılaştırır. */
  voltageLevel: VoltageLevel | null;
  /** Null = bu pinin telemetri kanalı yok; dolu olanlar cihazda `IoChannel` üretir. */
  channelNumber: number | null;
}
