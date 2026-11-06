/**
 * Otomasyon ekleme/düzenleme formunun tuttuğu şekil ve doğrulaması.
 *
 * Ekleme ve düzenleme AYNI form tipini kullanır, çünkü aradaki fark yalnızca birkaç
 * alanın görünürlüğü: `companyId` yalnızca eklemede yazılabilir (`AutomationUpdateDto`
 * taşımaz), `isActive` ve `isUnderMaintenance` yalnızca düzenlemede (yeni otomasyon
 * bakımda doğmaz). İki ayrı şema yapmak, alanların %90'ını ve kurallarını iki kez
 * yazmak demekti.
 *
 * Sunucuya gönderilen şekle dönüşüm `toCreateRequest` / `toUpdateRequest` ile
 * yapılır; fazla alanlar orada düşer.
 *
 * Kurallar `AutomationCreateDtoValidator` / `AutomationUpdateDtoValidator` ile birebir.
 * Önden yakalamak yalnızca hız değil: 400 dönerse hatalar PascalCase anahtarlardan
 * forma yeniden eşlenmek zorunda kalır.
 */
import { z } from 'zod';
import type { AutomationCreateRequest } from './automationCreateRequest';
import type { AutomationUpdateRequest } from './automationUpdateRequest';

// SCADA adresi / zaman aşımı / açık-kapalı 2026-11-04'ten beri kontrol kartındadır (diyagramın kart formu).
export const automationFormSchema = z.object({
  /** Yalnızca düzenlemede dolu; eklemede boş string. Kullanıcı girdisi değil. */
  id: z.string(),
  name: z.string().trim().min(2, 'İsim bilgisi en az 2 karakter içermeli'),
  companyId: z.uuid('Firma bilgisi zorunlu lütfen kontrol ediniz'),
  // Konum haritadan seçilir; seçilmediyse null.
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
  // Opsiyonel metinler formda '' olarak durur, sunucuya null olarak gider.
  locationDescription: z.string(),
  isActive: z.boolean(),
  isUnderMaintenance: z.boolean()
});

export type AutomationFormValues = z.infer<typeof automationFormSchema>;

/** Yeni otomasyon formunun başlangıç değerleri. */
export const emptyAutomationForm: AutomationFormValues = {
  id: '',
  name: '',
  companyId: '',
  latitude: null,
  longitude: null,
  locationDescription: '',
  isActive: true,
  isUnderMaintenance: false
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

export function toCreateRequest(values: AutomationFormValues): AutomationCreateRequest {
  return {
    name: values.name.trim(),
    companyId: values.companyId,
    latitude: values.latitude,
    longitude: values.longitude,
    locationDescription: blankToNull(values.locationDescription)
  };
}

export function toUpdateRequest(values: AutomationFormValues): AutomationUpdateRequest {
  return {
    id: values.id,
    name: values.name.trim(),
    latitude: values.latitude,
    longitude: values.longitude,
    locationDescription: blankToNull(values.locationDescription),
    isActive: values.isActive,
    isUnderMaintenance: values.isUnderMaintenance
  };
}

/**
 * `GET /api/Automation/{id}/update` yanıtını forma çevirir.
 *
 * `companyId` sunucudan GELMEZ (`AutomationUpdateDto` taşımıyor) — otomasyon listesinden
 * ayrıca verilir. Formda salt-okunur durduğu için yalnızca gösterim amaçlıdır,
 * `toUpdateRequest` onu zaten göndermez.
 */
export function toAutomationForm(model: AutomationUpdateRequest, companyId: string): AutomationFormValues {
  return {
    id: model.id,
    name: model.name,
    companyId,
    latitude: model.latitude,
    longitude: model.longitude,
    locationDescription: nullToBlank(model.locationDescription),
    isActive: model.isActive,
    isUnderMaintenance: model.isUnderMaintenance
  };
}
