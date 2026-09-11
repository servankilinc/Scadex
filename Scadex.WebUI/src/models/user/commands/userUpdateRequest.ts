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
  // Boş bırakmak kartı kaldırır. Sunucuda aktif kullanıcılar arasında tekil (çakışma 400, `errors.IdentityCardId`).
  identityCardId: z.string().trim().max(64, 'Kart numarası en fazla 64 karakter olabilir').optional(),
  // Kullanıcı `IActivatableEntity` — silme yok, pasife alma var.
  isActive: z.boolean()
});

export type UserUpdateRequest = z.infer<typeof userUpdateRequestSchema>;
