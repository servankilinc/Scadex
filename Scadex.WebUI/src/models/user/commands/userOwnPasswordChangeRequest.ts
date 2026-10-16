/** Ayna: Scadex.Model/Dtos/User/Commands/UserOwnPasswordChangeDto.cs (+ UserOwnPasswordChangeDtoValidator) */
import { z } from 'zod';

/**
 * Yeni parola kuralı sunucudakinin kopyası (en az 4, en fazla 128) — sunucudaki kural değişirse burası da elle değişmeli.
 * `confirmPassword` yalnızca formdadır, sunucuya gitmez. Yanlış mevcut parola sunucudan 400 (`errors.CurrentPassword`) döner.
 */
export const userOwnPasswordChangeSchema = z
  .object({
    currentPassword: z.string().min(1, 'Mevcut parola boş olamaz'),
    newPassword: z.string().min(4, 'Parola en az 4 karakter olmalı').max(128, 'Parola en fazla 128 karakter olabilir'),
    confirmPassword: z.string()
  })
  .refine(values => values.newPassword !== values.currentPassword, { path: ['newPassword'], message: 'Yeni parola mevcut paroladan farklı olmalı' })
  .refine(values => values.newPassword === values.confirmPassword, { path: ['confirmPassword'], message: 'Parolalar eşleşmiyor' });

export type UserOwnPasswordChangeForm = z.infer<typeof userOwnPasswordChangeSchema>;

export interface UserOwnPasswordChangeRequest {
  currentPassword: string;
  newPassword: string;
  /** Bu cihazın oturumu korunur; diğer cihazlardaki oturumlar yeniden giriş ister. */
  deviceId: string | null;
}
