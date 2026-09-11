/** Ayna: Scadex.Model/Dtos/User/Commands/UserCreateDto.cs (+ UserCreateDtoValidator) */
import { z } from 'zod';

export const userCreateRequestSchema = z.object({
  userName: z.string().trim().min(4, 'Kullanıcı adı en az 4 karakter olmalı'),
  fullName: z.string().trim().min(4, 'Lütfen geçerli bir ad soyad giriniz'),
  // `z.guid()`, `z.uuid()` DEĞİL: EF'in ürettiği sıralı Guid'ler RFC sürüm/varyant
  // bitlerini taşımak zorunda değil; `uuid` onları geçersiz sayabilir.
  companyId: z.guid('Firma bilgisi zorunlu, lütfen kontrol ediniz'),
  // Sunucuda `string?` ve kural yalnızca doluysa işler; boş metin `api/user.ts`'te null'a çevrilir.
  email: z.union([z.literal(''), z.email('Geçerli bir e-posta adresi giriniz')]).optional(),
  phoneNumber: z.string().trim().optional(),
  // Kart okuyucudan gelen ham kimlik; sunucuda aktif kullanıcılar arasında tekil (çakışma 400, `errors.IdentityCardId`).
  identityCardId: z.string().trim().max(64, 'Kart numarası en fazla 64 karakter olabilir').optional(),
  password: z.string().min(4, 'Parola en az 4 karakter olmalı')
});

export type UserCreateRequest = z.infer<typeof userCreateRequestSchema>;
