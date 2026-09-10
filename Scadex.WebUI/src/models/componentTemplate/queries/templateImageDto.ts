/** Ayna: Scadex.Model/Dtos/ComponentTemplate/Queries/TemplateImageDto.cs — sözleşme: docs/api-contract/10-component-template.md */

/**
 * Yüklenen şablon görselinin sonucu.
 *
 * `url` GÖRELİDİR (`/uploads/templates/x.png`). Mutlak adres saklamak, uygulama
 * taşındığı gün (localhost → IIS → ters vekil) bütün şablonların görselini
 * kırardı. Tarayıcıya vermeden önce `templateImageSrc()` ile API adresiyle
 * birleştirilir.
 */
export interface TemplateImageDto {
  url: string;
}
