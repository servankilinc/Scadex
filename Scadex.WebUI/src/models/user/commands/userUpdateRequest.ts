/**
 * Ayna: Scadex.Model/Dtos/User/Commands/UserUpdateDto.cs (+ UserUpdateDtoValidator)
 *
 * `userName`, `email` ve `companyId` BİLEREK yok: `UserUpdateDto` bu alanları
 * taşımıyor, gönderilse de sunucu okumaz.
 */
import { z } from 'zod';

export const userUpdateRequestSchema = z.object({
  id: z.guid(),
  fullName: z.string().trim().min(4, 'Lütfen geçerli bir ad soyad giriniz'),
  phoneNumber: z.string().trim().optional(),
  // Kullanıcı `IActivatableEntity` — silme yok, pasife alma var.
  isActive: z.boolean()
});

export type UserUpdateRequest = z.infer<typeof userUpdateRequestSchema>;
