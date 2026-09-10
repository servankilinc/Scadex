import { z } from 'zod';

// deviceId ve clientType formda toplanmaz; api katmani ekler
export const loginRequestSchema = z.object({
  userName: z.string().trim().min(1, 'Kullanici adi zorunludur'),
  password: z.string().min(1, 'Parola zorunludur')
});

export type LoginRequest = z.infer<typeof loginRequestSchema>;
