/** Ayna: Scadex.Model/Dtos/User/Commands/UserPasswordChangeDto.cs (+ UserPasswordChangeDtoValidator) */
import { z } from 'zod';

/**
 * Sunucudaki kuralın kopyası (en az 4, en fazla 128) — sunucudaki kural değişirse burası da elle değişmeli. `confirmPassword`
 * yalnızca formdadır, sunucuya gitmez.
 */
export const userPasswordChangeSchema = z
  .object({
    newPassword: z.string().min(4, 'Parola en az 4 karakter olmalı').max(128, 'Parola en fazla 128 karakter olabilir'),
    confirmPassword: z.string()
  })
  .refine(values => values.newPassword === values.confirmPassword, { path: ['confirmPassword'], message: 'Parolalar eşleşmiyor' });

export type UserPasswordChangeForm = z.infer<typeof userPasswordChangeSchema>;

export interface UserPasswordChangeRequest {
  newPassword: string;
}
