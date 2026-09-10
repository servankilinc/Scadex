/**
 * Ayna: Scadex.Model/Dtos/CameraCaptureSetting/Commands/CameraCaptureSettingUpdateDto.cs
 *
 * Şema sunucudaki `CameraCaptureSettingUpdateDtoValidator`'ın kopyasıdır; codegen
 * olmadığı için **sunucudaki kural değişirse burası da elle değişmeli.** Son söz
 * her hâlükârda sunucudadır.
 */
import { z } from 'zod';

/**
 * `wwwroot` altında göreli bir yol mu?
 *
 * Kontrol KIRPILMIS değer üzerinde: `"/uploads/captures/"` gibi bir girdi sunucuda
 * zaten `Trim('/')` ediliyor. Sunucudaki aynı kural bir dönem baştaki tek slash'ı
 * "mutlak yol" sanıp geçerli bir girdiyi reddediyordu; kopyada o hatayı tekrarlamamak
 * için kırpma ÖNCE yapılır.
 */
function isRelativeUnderWwwroot(value: string): boolean {
  const trimmed = value.trim().replace(/^\/+|\/+$/g, '');
  if (trimmed.length === 0) return false;
  if (trimmed.includes('..')) return false;

  // Windows sürücü kökü ("C:\...") ve UNC ("\\sunucu\pay") — sunucudaki
  // Path.IsPathRooted'in yakaladıklarının istemci karşılığı.
  return !/^[a-zA-Z]:/.test(trimmed) && !trimmed.startsWith('\\');
}

export const cameraCaptureSettingFormSchema = z.object({
  snapshotTimeoutMs: z
    .number()
    .int()
    .min(500, 'Anlık görüntü zaman aşımı 500-60.000 ms arasında olmalı')
    .max(60000, 'Anlık görüntü zaman aşımı 500-60.000 ms arasında olmalı'),

  snapshotCacheSeconds: z
    .number()
    .int()
    .min(0, 'Anlık görüntü önbelleği 0-300 saniye arasında olmalı')
    .max(300, 'Anlık görüntü önbelleği 0-300 saniye arasında olmalı'),

  captureRoot: z
    .string()
    .trim()
    .min(1, 'Çekim kök dizini zorunlu')
    .refine(isRelativeUnderWwwroot, 'Çekim kök dizini wwwroot altında göreli bir yol olmalı'),

  captureRetentionDays: z.number().int().min(0, 'Saklama süresi 0-3650 gün arasında olmalı').max(3650, 'Saklama süresi 0-3650 gün arasında olmalı'),

  maxClipDurationSec: z
    .number()
    .int()
    .min(1, 'Klip süresi üst sınırı 1-3600 saniye arasında olmalı')
    .max(3600, 'Klip süresi üst sınırı 1-3600 saniye arasında olmalı'),

  clipFinalizeGraceMs: z.number().int().min(0, 'Sonlandırma payı 0-60.000 ms arasında olmalı').max(60000, 'Sonlandırma payı 0-60.000 ms arasında olmalı')
});

export type CameraCaptureSettingFormValues = z.infer<typeof cameraCaptureSettingFormSchema>;

/** Okuma ve yazma DTO'ları alan alan aynı — projeksiyona gerek yok. */
export type CameraCaptureSettingUpdateRequest = CameraCaptureSettingFormValues;
