/**
 * Ayna: Scadex.Model/Dtos/Company/Commands/CompanyUpdateDto.cs (+ CompanyUpdateDtoValidator)
 *
 * `description` BİLEREK yok: `CompanyUpdateDto` bu alanı taşımıyor, gönderilse de
 * sunucu okumaz. Forma koymak açıklamanın kaydedildiği izlenimini verirdi.
 *
 * `GET /api/Company/{id}/update` yanıtı da bu şekildedir.
 */
import { z } from 'zod';

export const companyUpdateRequestSchema = z.object({
  id: z.uuid(),
  name: z.string().trim().min(2, 'Firma ismi en az 2 karakter olmalı'),
  // Firma `IActivatableEntity` — silme yok, pasife alma var.
  isActive: z.boolean()
});

export type CompanyUpdateRequest = z.infer<typeof companyUpdateRequestSchema>;
