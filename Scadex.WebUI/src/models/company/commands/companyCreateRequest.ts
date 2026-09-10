/** Ayna: Scadex.Model/Dtos/Company/Commands/CompanyCreateDto.cs (+ CompanyCreateDtoValidator) */
import { z } from 'zod';

export const companyCreateRequestSchema = z.object({
  name: z.string().trim().min(2, 'Firma ismi en az 2 karakter olmalı'),
  // Sunucuda `string?`; boş bırakılırsa null gider, boş string değil.
  description: z.string().trim().optional()
});

export type CompanyCreateRequest = z.infer<typeof companyCreateRequestSchema>;
