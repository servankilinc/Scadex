/** Ayna: Scadex.Model/Dtos/Role/Commands/RoleCreateDto.cs (+ RoleCreateDtoValidator) */
import { z } from 'zod';

export const roleCreateRequestSchema = z.object({
  name: z.string().trim().min(4, 'İsim bilgisi en az 4 karakter içermeli')
});

export type RoleCreateRequest = z.infer<typeof roleCreateRequestSchema>;
