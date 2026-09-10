/** Ayna: Scadex.Model/Dtos/Role/Commands/RoleUpdateDto.cs (+ RoleUpdateDtoValidator) */
import { z } from 'zod';

export const roleUpdateRequestSchema = z.object({
  // `z.guid()`: seed dışındaki roller EF'in sıralı Guid'iyle doğar (bkz. userCreateRequest).
  id: z.guid(),
  name: z.string().trim().min(4, 'İsim bilgisi en az 4 karakter içermeli'),
  // Rol `IActivatableEntity` — silme yok. Pasif rol izin türetmez.
  isActive: z.boolean()
});

export type RoleUpdateRequest = z.infer<typeof roleUpdateRequestSchema>;
