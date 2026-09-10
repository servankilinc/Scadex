import { z } from 'zod';

// passwordConfirm yalnizca istemci tarafi bir alandir, backend'e gonderilmez.
export const signUpRequestSchema = z
  .object({
    userName: z.string().trim().min(3, 'En az 3 karakter olmalidir'),
    email: z.email('Gecerli bir e-posta adresi girin'),
    fullName: z.string().trim().min(1, 'Ad soyad zorunludur').max(150, 'En fazla 150 karakter'),
    companyId: z.uuid('Firma bilgisi zorunludur'),
    phoneNumber: z.string().trim().optional(),
    password: z.string().min(4, 'En az 4 karakter olmalidir'),
    passwordConfirm: z.string()
  })
  .refine(v => v.password === v.passwordConfirm, {
    message: 'Parolalar eslesmiyor',
    path: ['passwordConfirm']
  });

export type SignUpRequest = z.infer<typeof signUpRequestSchema>;
