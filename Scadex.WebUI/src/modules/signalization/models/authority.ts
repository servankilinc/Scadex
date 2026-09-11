/**
 * Ayna: Scadex.Signalization/Dtos/Authority/SignalAuthorityDtos.cs
 *
 * Kurum = iç kapı yetkisi taşıyan ROL. Ayrı bir yetki yapısı yoktur: kullanıcı kurumun rolüne sahipse
 * o kurumun iç kapısını kartıyla açar.
 */
import { z } from 'zod';

export interface SignalAuthorityDto {
  id: string;
  name: string;
  roleId: string;
  /** Rol silinmişse `null`. */
  roleName: string | null;
  /** Pasif rol yetki türetmez: kurum aktif olsa da rolü pasifse kimse bu kurumla kapı açamaz. */
  roleIsActive: boolean;
  isActive: boolean;
}

/** Kurum listesinin TAM hâli; gövdede olmayan kurum pasife alınır. Guid'i istemci üretir. */
export interface SignalAuthoritySaveRequest {
  authorities: { id: string; name: string; roleId: string }[];
}

/**
 * Sunucudaki `SignalAuthoritySaveRequestValidator`'ın kopyası — **sunucudaki kural değişirse burası da elle
 * değişmeli.** Kimlik alanı formda `authorityId` adını taşır: `useFieldArray` kendi `id` anahtarını üretir,
 * aynı adı paylaşmak değerle karışırdı.
 */
export const signalAuthorityFormSchema = z
  .object({
    authorities: z.array(
      z.object({
        authorityId: z.string(),
        name: z.string().trim().min(1, 'Kurum adı zorunlu').max(64, 'Kurum adı en fazla 64 karakter olabilir'),
        roleId: z.string().min(1, 'Rol seçilmeli')
      })
    )
  })
  .superRefine((values, ctx) => {
    const seenRoles = new Set<string>();
    const seenNames = new Set<string>();

    values.authorities.forEach((authority, index) => {
      if (authority.roleId && seenRoles.has(authority.roleId))
        ctx.addIssue({ code: 'custom', path: ['authorities', index, 'roleId'], message: 'Bir rol yalnızca bir kuruma bağlanabilir' });
      seenRoles.add(authority.roleId);

      // `toUpperCase` (yerel ayarsız): sunucu `ToUpperInvariant` ile karşılaştırıyor.
      const nameKey = authority.name.trim().toUpperCase();
      if (nameKey && seenNames.has(nameKey))
        ctx.addIssue({ code: 'custom', path: ['authorities', index, 'name'], message: 'Aynı adda iki kurum olamaz' });
      seenNames.add(nameKey);
    });
  });

export type SignalAuthorityFormValues = z.infer<typeof signalAuthorityFormSchema>;

export function toAuthoritySaveRequest(values: SignalAuthorityFormValues): SignalAuthoritySaveRequest {
  return {
    authorities: values.authorities.map(authority => ({ id: authority.authorityId, name: authority.name.trim(), roleId: authority.roleId }))
  };
}
