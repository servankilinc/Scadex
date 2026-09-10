/** Ayna: Scadex.Model/Dtos/Diagram/Queries/DiagramTemplateDto.cs — sözleşme: docs/api-contract/02-diagram-read.md */
import type { DeviceType } from '@/models/enums';

/**
 * Cihazın çözümlenmiş şablon ÖZETİ — node'un görsel spec'i.
 *
 * Her cihaza gömülü gelir; böylece şablon pasife alınsa bile kabin doğru boyut
 * ve renkle render olur ve graf, palet çağrısına bağımlı olmaz.
 * Şablonun pinleri burada YOK — cihazın gerçek pinleri `DiagramDeviceDto.pins`'te.
 */
export interface DiagramTemplateDto {
  id: string;
  name: string;
  deviceTypeId: DeviceType;
  width: number;
  height: number;
  /** `#RRGGBB` renk dizesi. */
  backgroundColor: string;
  backgroundImageUrl: string | null;
}
