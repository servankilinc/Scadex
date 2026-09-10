/**
 * Kabin ekleme/düzenleme formunun tuttuğu şekil ve doğrulaması.
 *
 * Ekleme ve düzenleme AYNI form tipini kullanır, çünkü aradaki fark yalnızca iki
 * alanın görünürlüğü: `companyId` yalnızca eklemede yazılabilir (`CabinetUpdateDto`
 * taşımaz), `isActive` yalnızca düzenlemede. İki ayrı şema yapmak, alanların
 * %90'ını ve kurallarını iki kez yazmak demekti.
 *
 * Sunucuya gönderilen şekle dönüşüm `toCreateRequest` / `toUpdateRequest` ile
 * yapılır; fazla alanlar orada düşer.
 *
 * Kurallar `CabinetCreateDtoValidator` / `CabinetUpdateDtoValidator` ile birebir.
 * Önden yakalamak yalnızca hız değil: 400 dönerse hatalar PascalCase anahtarlardan
 * forma yeniden eşlenmek zorunda kalır.
 */
import { z } from 'zod';
import type { CabinetCreateRequest } from './cabinetCreateRequest';
import type { CabinetUpdateRequest } from './cabinetUpdateRequest';

/** SCADA komut zaman aşımının sunucudaki alt sınırı. */
export const SCADA_MIN_TIMEOUT_MS = 10000;

export const cabinetFormSchema = z
  .object({
    /** Yalnızca düzenlemede dolu; eklemede boş string. Kullanıcı girdisi değil. */
    id: z.string(),
    name: z.string().trim().min(2, 'İsim bilgisi en az 2 karakter içermeli'),
    companyId: z.uuid('Firma bilgisi zorunlu lütfen kontrol ediniz'),
    // Konum haritadan seçilir; seçilmediyse null.
    latitude: z.number().nullable(),
    longitude: z.number().nullable(),
    // Opsiyonel metinler formda '' olarak durur, sunucuya null olarak gider.
    locationDescription: z.string(),
    gsmIp: z.string(),
    networkIp: z.string(),
    scadaBaseUrl: z.string(),
    scadaCommandTimeoutMs: z.number('Sayı giriniz').int('Tam sayı giriniz').nonnegative('Negatif olamaz'),
    scadaIsEnabled: z.boolean(),
    isActive: z.boolean()
  })
  .superRefine((value, ctx) => {
    // Sunucudaki `When(v => v.ScadaIsEnabled, ...)` bloğunun aynısı.
    if (!value.scadaIsEnabled) return;

    if (value.scadaBaseUrl.trim().length === 0) {
      ctx.addIssue({ code: 'custom', path: ['scadaBaseUrl'], message: 'SCADA açıkken adres bilgisi zorunlu' });
    }

    if (value.scadaCommandTimeoutMs < SCADA_MIN_TIMEOUT_MS) {
      ctx.addIssue({ code: 'custom', path: ['scadaCommandTimeoutMs'], message: 'Zaman aşımı en az 10.000ms olabilir' });
    }
  });

export type CabinetFormValues = z.infer<typeof cabinetFormSchema>;

/** Yeni kabin formunun başlangıç değerleri. */
export const emptyCabinetForm: CabinetFormValues = {
  id: '',
  name: '',
  companyId: '',
  latitude: null,
  longitude: null,
  locationDescription: '',
  gsmIp: '',
  networkIp: '',
  scadaBaseUrl: '',
  scadaCommandTimeoutMs: SCADA_MIN_TIMEOUT_MS,
  scadaIsEnabled: false,
  isActive: true
};

/**
 * `<input>` boşken `''` verir, sunucudaki karşılık ise `string?`.
 *
 * Dönüşüm olmadan veritabanına boş string yazılır; `locationDescription` gibi
 * alanlar okuma tarafında `|| '-'` ile kontrol edildiği için sonuç aynı görünse
 * de "girilmiş ama boş" ile "hiç girilmemiş" ayrımı kaybolurdu.
 */
function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}

/** Sunucudan gelen `string?` alanı forma sokar — `null` girdiler React'te uncontrolled input yapar. */
function nullToBlank(value: string | null | undefined): string {
  return value ?? '';
}

export function toCreateRequest(values: CabinetFormValues): CabinetCreateRequest {
  return {
    name: values.name.trim(),
    companyId: values.companyId,
    latitude: values.latitude,
    longitude: values.longitude,
    locationDescription: blankToNull(values.locationDescription),
    gsmIp: blankToNull(values.gsmIp),
    networkIp: blankToNull(values.networkIp),
    scadaBaseUrl: blankToNull(values.scadaBaseUrl),
    scadaCommandTimeoutMs: values.scadaCommandTimeoutMs,
    scadaIsEnabled: values.scadaIsEnabled
  };
}

export function toUpdateRequest(values: CabinetFormValues): CabinetUpdateRequest {
  return {
    id: values.id,
    name: values.name.trim(),
    latitude: values.latitude,
    longitude: values.longitude,
    locationDescription: blankToNull(values.locationDescription),
    gsmIp: blankToNull(values.gsmIp),
    networkIp: blankToNull(values.networkIp),
    scadaBaseUrl: blankToNull(values.scadaBaseUrl),
    scadaCommandTimeoutMs: values.scadaCommandTimeoutMs,
    scadaIsEnabled: values.scadaIsEnabled,
    isActive: values.isActive
  };
}

/**
 * `GET /api/Cabinet/{id}/update` yanıtını forma çevirir.
 *
 * `companyId` sunucudan GELMEZ (`CabinetUpdateDto` taşımıyor) — kabin listesinden
 * ayrıca verilir. Formda salt-okunur durduğu için yalnızca gösterim amaçlıdır,
 * `toUpdateRequest` onu zaten göndermez.
 */
export function toCabinetForm(model: CabinetUpdateRequest, companyId: string): CabinetFormValues {
  return {
    id: model.id,
    name: model.name,
    companyId,
    latitude: model.latitude,
    longitude: model.longitude,
    locationDescription: nullToBlank(model.locationDescription),
    gsmIp: nullToBlank(model.gsmIp),
    networkIp: nullToBlank(model.networkIp),
    scadaBaseUrl: nullToBlank(model.scadaBaseUrl),
    scadaCommandTimeoutMs: model.scadaCommandTimeoutMs,
    scadaIsEnabled: model.scadaIsEnabled,
    isActive: model.isActive
  };
}
