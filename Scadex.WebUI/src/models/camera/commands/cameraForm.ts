/**
 * Ayna: Scadex.Model/Dtos/Camera/Commands/CameraCreateDto.cs + CameraUpdateDto.cs
 *       (+ CameraCreateDtoValidator / CameraUpdateDtoValidator / CameraRules)
 * Sözleşme: docs/api-contract/11-camera.md
 *
 * `cabinetForm.ts` ile aynı desen: ekleme ve düzenlemenin ORTAK şekli tek bir
 * şemadır, sunucuya gönderilen şekle dönüşüm `toCreateRequest` /
 * `toUpdateRequest` ile yapılır. Sunucuda da ortak kurallar tek bir
 * `CameraRules` sınıfında toplanmış durumda — ikisi ayrışırsa create'te
 * reddedilen bir değer update ile içeri girebilirdi.
 *
 * Port ve kanal alanı YOK: markaya aittirler, sunucuda `ICameraProtocolProfile`
 * `brand`'e göre üretir.
 */
import { z } from 'zod';
import { CameraBrand } from '@/models/enums/entityEnums';
import type { CameraDto } from '../queries/cameraDto';

// Sayısal alanlarda `z.coerce.number()` KULLANILMIYOR: zod v4'te coerce girdi
// tipini `unknown` yapıyor ve `zodResolver` ile birlikte formun tipi çıktı
// tipinden ayrışıyor (derleme hatası). Kod tabanının mevcut konvansiyonu düz
// `z.number()` + formda `register(..., { valueAsNumber: true })` —
// `cabinetForm.ts`'teki `scadaCommandTimeoutMs` aynı şekilde tanımlı.
const port = (label: string) =>
  z.number(`${label} sayı olmalı`).int(`${label} tam sayı olmalı`).min(1, `${label} 1-65535 arasında olmalı`).max(65535, `${label} 1-65535 arasında olmalı`);

/**
 * Boş bırakılabilen port alanı.
 *
 * Formda `setValueAs` ile boş girdi `null`'a çevriliyor; `valueAsNumber`
 * kullanılsaydı boş input `NaN` üretir ve kullanıcı "sayı olmalı" diye anlamsız
 * bir hata görürdü — oysa alanı boş bırakmak geçerli.
 */
const optionalPort = (label: string) => port(label).nullable();

export const cameraFormSchema = z.object({
  name: z.string().trim().min(1, 'İsim girilmeli').max(150, 'İsim en fazla 150 karakter olabilir'),
  description: z.string().trim().max(512, 'Açıklama en fazla 512 karakter olabilir'),
  /** Sunucudaki `IsInEnum` kuralının aynası: adres ve portları marka belirler. */
  brand: z.enum(CameraBrand, 'Kamera markası seçilmeli'),
  model: z.string().trim().max(64, 'Model en fazla 64 karakter olabilir'),

  ipAddress: z.string().trim().min(1, 'IP adresi girilmeli').max(64, 'IP adresi en fazla 64 karakter olabilir'),

  username: z.string().trim().max(128, 'Kullanıcı adı en fazla 128 karakter olabilir'),
  /**
   * Formda HER ZAMAN boş başlar ve boş bırakılırsa gövdeden tümden çıkarılır
   * ("dokunma").
   *
   * Okuma DTO'su artık parolayı döndürüyor, yani `toCameraForm` onu önceden
   * DOLDURABİLİRDİ; bilerek doldurulmuyor. Doldurulsaydı "alanı temizle"
   * hareketi `''` üretir, `orUndefined` onu `undefined`'a çevirir ve kullanıcı
   * parolayı SİLEMEZ hale gelirdi. Doldurmak istenirse `toUpdateRequest`'in
   * üç durumlu eşlemesi de birlikte değişmelidir.
   */
  password: z.string(),

  monitoringPort: optionalPort('İzleme portu'),
  pingIntervalSec: z
    .number('Yoklama aralığı sayı olmalı')
    .int('Yoklama aralığı tam sayı olmalı')
    .min(10, 'Yoklama aralığı en az 10 saniye olmalı')
    .max(86400, 'Yoklama aralığı en fazla 24 saat olabilir'),
  isMonitoringEnabled: z.boolean(),

  /** Yalnızca düzenlemede anlamlı; ekleme her zaman aktif doğurur. */
  isActive: z.boolean()
});

export type CameraFormValues = z.infer<typeof cameraFormSchema>;

/** Yeni kamera formunun başlangıç değerleri — sunucudaki DTO varsayılanlarıyla aynı. */
export const emptyCameraForm: CameraFormValues = {
  name: '',
  description: '',
  brand: CameraBrand.Hikvision,
  model: '',
  ipAddress: '',
  username: '',
  password: '',
  monitoringPort: null,
  pingIntervalSec: 300,
  isMonitoringEnabled: true,
  isActive: true
};

/** Sunucudaki `CameraCreateDto`. `isActive` GÖNDERİLMEZ — yeni kamera aktif doğar. */
export interface CameraCreateRequest {
  cabinetId: string;
  name: string;
  description?: string;
  brand: CameraBrand;
  model?: string;
  ipAddress: string;
  username?: string;
  password?: string;
  /** `null` ise sunucu markanın RTSP portunu yazar. */
  monitoringPort: number | null;
  pingIntervalSec: number;
  isMonitoringEnabled: boolean;
}

/** Sunucudaki `CameraUpdateDto`. `cabinetId` YOKTUR — kamera kabin değiştiremez. */
export interface CameraUpdateRequest extends Omit<CameraCreateRequest, 'cabinetId'> {
  id: string;
  isActive: boolean;
}

/** Boş metni `undefined`'a çevirir — sunucuda `string?`, boş string değil null gitsin. */
const orUndefined = (value: string) => (value.trim().length > 0 ? value.trim() : undefined);

export function toCreateRequest(values: CameraFormValues, cabinetId: string): CameraCreateRequest {
  return {
    cabinetId,
    name: values.name,
    description: orUndefined(values.description),
    brand: values.brand,
    model: orUndefined(values.model),
    ipAddress: values.ipAddress,
    username: orUndefined(values.username),
    password: orUndefined(values.password),
    monitoringPort: values.monitoringPort,
    pingIntervalSec: values.pingIntervalSec,
    isMonitoringEnabled: values.isMonitoringEnabled
  };
}

export function toUpdateRequest(values: CameraFormValues, id: string): CameraUpdateRequest {
  // `cabinetId` bilerek DÜŞÜRÜLÜYOR: `CameraUpdateDto` onu taşımaz, kamera
  // kabin değiştiremez (geçmiş çekimlerini yanlış kabine bağlardı).
  const { cabinetId, ...rest } = toCreateRequest(values, '');
  void cabinetId;
  return {
    ...rest,
    id,
    isActive: values.isActive,
    /**
     * ÜÇ DURUMLU PAROLA — sunucudaki kuralın birebir karşılığı.
     *
     * Kullanıcı alana dokunmadıysa alan gövdeden TÜMDEN ÇIKAR (`undefined`):
     * sunucuda `null` "dokunma" demektir. Boş string göndermek "parolayı sil"
     * anlamına gelirdi ve form her açılışta boş geldiği için her düzenleme
     * parolayı sessizce uçururdu.
     *
     * Formun önceden doldurulmama gerekçesi `cameraFormSchema.password`'da.
     */
    password: orUndefined(values.password)
  };
}

/** Sunucudan gelen kaydı form şekline çevirir. Parola BİLEREK boş bırakılır. */
export function toCameraForm(camera: CameraDto): CameraFormValues {
  return {
    name: camera.name,
    description: camera.description ?? '',
    brand: camera.brand,
    model: camera.model ?? '',
    ipAddress: camera.ipAddress,
    username: camera.username ?? '',
    password: '',
    monitoringPort: camera.monitoringPort,
    pingIntervalSec: camera.pingIntervalSec,
    isMonitoringEnabled: camera.isMonitoringEnabled,
    isActive: camera.isActive
  };
}
