/**
 * Ayna: Scadex.Model/Dtos/User/Commands/UserProfileUpdateDto.cs (+ UserProfileUpdateDtoValidator)
 *
 * Kimlik gövdede YOK — sunucu token'dan okur. Kullanıcı adı, firma ve kart bu formda yazılmaz.
 */
import { z } from 'zod';

export const userProfileUpdateRequestSchema = z.object({
  fullName: z.string().trim().min(4, 'Lütfen geçerli bir ad soyad giriniz'),
  // Boş metin `api/profile.ts`'te null'a çevrilir. Doluysa sistem genelinde tekil (çakışma 400, `errors.Email`).
  email: z.union([z.literal(''), z.email('Geçerli bir e-posta adresi giriniz').max(256, 'E-posta en fazla 256 karakter olabilir')]).optional(),
  phoneNumber: z.string().trim().optional()
});

export type UserProfileUpdateRequest = z.infer<typeof userProfileUpdateRequestSchema>;
