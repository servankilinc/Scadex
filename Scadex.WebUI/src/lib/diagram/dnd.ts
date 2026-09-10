import type { ComponentTemplatePaletteDto } from '@/models/componentTemplate';

/**
 * Paletten canvas'a sürükle-bırak sözleşmesi.
 *
 * Özel bir MIME tipi kullanılır (`text/plain` değil): tarayıcı dışından
 * sürüklenen rastgele bir metin böylece canvas'a cihaz bırakamaz.
 */
export const TEMPLATE_MIME = 'application/x-scadex-template';

export function setTemplateDragData(dataTransfer: DataTransfer, template: ComponentTemplatePaletteDto): void {
  dataTransfer.setData(TEMPLATE_MIME, JSON.stringify(template));
  dataTransfer.effectAllowed = 'copy';
}

/**
 * Bırakılan veriyi çözer. Bozuk ya da yabancı bir yük `null` döner — tarayıcı
 * dışından gelen bir sürüklemenin editörü çökertmemesi gerekiyor.
 */
export function readTemplateDragData(dataTransfer: DataTransfer): ComponentTemplatePaletteDto | null {
  const raw = dataTransfer.getData(TEMPLATE_MIME);
  if (!raw) return null;

  try {
    const parsed: unknown = JSON.parse(raw);
    if (!isTemplate(parsed)) return null;
    return parsed;
  } catch {
    return null;
  }
}

function isTemplate(value: unknown): value is ComponentTemplatePaletteDto {
  if (typeof value !== 'object' || value === null) return false;
  const candidate = value as Partial<ComponentTemplatePaletteDto>;
  return typeof candidate.id === 'string' && typeof candidate.width === 'number' && typeof candidate.height === 'number';
}
