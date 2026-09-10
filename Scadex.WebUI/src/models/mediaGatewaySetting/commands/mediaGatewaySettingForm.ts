/**
 * Ayna: Scadex.Model/Dtos/MediaGatewaySetting/Commands/MediaGatewaySettingUpdateDto.cs
 *
 * Şema sunucudaki `MediaGatewaySettingUpdateDtoValidator`'ın kopyasıdır. İkisi ayrı
 * ayrı yazılıyor çünkü codegen yok; **sunucudaki kuralı değiştirirseniz burayı da
 * değiştirin.** Sunucu yine de tek yetkili doğrulayıcıdır — buradaki kopya yalnızca
 * kullanıcıyı bir tur ağa çıkmadan uyarmak için.
 */
import { z } from 'zod';

/** MediaMTX'in `rtspTransport` için tanıdığı TEK küme; serbest metin yol kurulmasını bozar. */
export const RTSP_TRANSPORTS = ['tcp', 'udp', 'multicast', 'automatic'] as const;

export type RtspTransport = (typeof RTSP_TRANSPORTS)[number];

/**
 * Kolon `nvarchar`; kümeyi zorlayan tek şey sunucudaki yazma doğrulaması. Elle
 * düzenlenmiş ya da kötü tohumlanmış bir satır tanınmayan bir değer taşıyabilir —
 * o durumda seçim kutusu boş görünüp kullanıcı neyi düzelttiğini anlamaz. Bu yüzden
 * okuma tarafında `tcp`'ye düşülür: dördü arasında en toleranslı taşıma odur.
 */
export function toRtspTransport(value: string): RtspTransport {
  return (RTSP_TRANSPORTS as readonly string[]).includes(value) ? (value as RtspTransport) : 'tcp';
}

/** Sunucudaki `BeAbsoluteHttpUrl` karşılığı: mutlak ve şeması http(s). */
function isAbsoluteHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

export const mediaGatewaySettingFormSchema = z.object({
  apiTimeoutMs: z.number().int().min(1000, 'Zaman aşımı 1.000-300.000 ms arasında olmalı').max(300000, 'Zaman aşımı 1.000-300.000 ms arasında olmalı'),

  apiBaseUrl: z
    .string()
    .trim()
    .min(1, 'Control API adresi zorunlu')
    .refine(isAbsoluteHttpUrl, 'Control API adresi geçerli bir http(s) adresi olmalı'),

  webRtcPublicBaseUrl: z
    .string()
    .trim()
    .min(1, 'WebRTC adresi zorunlu')
    .refine(isAbsoluteHttpUrl, 'WebRTC adresi geçerli bir http(s) adresi olmalı'),

  tokenTtlSeconds: z.number().int().min(10, 'Bilet ömrü 10-3600 saniye arasında olmalı').max(3600, 'Bilet ömrü 10-3600 saniye arasında olmalı'),

  sourceOnDemandCloseAfterSec: z
    .number()
    .int()
    .min(0, 'Oturum kapanma süresi 0-3600 saniye arasında olmalı')
    .max(3600, 'Oturum kapanma süresi 0-3600 saniye arasında olmalı'),

  rtspTransport: z.enum(RTSP_TRANSPORTS, 'RTSP taşıma katmanı tcp, udp, multicast veya automatic olmalı'),

  recordRoot: z.string().trim().min(1, 'Kayıt kök dizini zorunlu')
});

export type MediaGatewaySettingFormValues = z.infer<typeof mediaGatewaySettingFormSchema>;

/**
 * Okuma ve yazma DTO'ları alan alan AYNI olduğu için ayrı bir projeksiyon yok —
 * form değerleri doğrudan gövdedir. Ayrışırlarsa dönüşüm buraya gelir.
 */
export type MediaGatewaySettingUpdateRequest = MediaGatewaySettingFormValues;
