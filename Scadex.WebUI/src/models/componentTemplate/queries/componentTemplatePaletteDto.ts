/** Ayna: Scadex.Model/Dtos/ComponentTemplate/Queries/ComponentTemplatePaletteDto.cs — sözleşme: docs/api-contract/10-component-template.md */
import type { DeviceType } from '@/models/enums';
import type { ComponentTemplatePalettePinDto } from './componentTemplatePalettePinDto';

/**
 * Paletteki (stencil kütüphanesi) tek bir şablon kartı — pin şemasıyla birlikte.
 * `GET /api/ComponentTemplate/palette` bunların listesini döner; yalnızca aktif şablonlar.
 *
 * **Pinler neden burada.** Paletten bırakılan cihazın pin ve kanal Id'lerini
 * istemci üretiyor; bunun için şemayı bilmesi şart. Şema olmadan cihaz canvas'ta
 * pinsiz doğar ve kaydedilene kadar kablolanamazdı.
 */
export interface ComponentTemplatePaletteDto {
  id: string;
  name: string;
  deviceTypeId: DeviceType;
  isSystemTemplate: boolean;
  width: number;
  height: number;
  /** `#RRGGBB` renk dizesi. */
  backgroundColor: string;
  backgroundImageUrl: string | null;
  /**
   * Şablonun pin şeması. Boş olabilir: pano çerçevesi gibi dekoratif bir şablonun
   * pini olmayabilir, o zaman cihaz da pinsiz doğar.
   *
   * Ayrı bir `pinCount` alanı YOKTUR — `pins.length` varken ikinci bir sayaç,
   * sessizce ayrışabilecek ikinci bir doğruluk kaynağı olurdu.
   */
  pins: ComponentTemplatePalettePinDto[];
}
