/**
 * Ayna: Scadex.Model/Dtos/Camera/Commands/CameraUpdateDto.cs (+ CameraUpdateDtoValidator)
 *
 * Yalnızca DÜZENLEME formu: kamera bir cihazdır ve oluşturma/kaldırma (pasife
 * alma) yalnızca diyagram kaydetmeden geçer (2026-10-25). Sunucuya gönderilen
 * şekle dönüşüm `toUpdateRequest` ile yapılır.
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
  isMonitoringEnabled: z.boolean()
});

export type CameraFormValues = z.infer<typeof cameraFormSchema>;

/** Düzenlenecek kayıt gelene kadar formun boş başlangıcı (alanlar kontrollü kalsın diye). */
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
  isMonitoringEnabled: true
};

/**
 * Sunucudaki `CameraUpdateDto`. `cabinetId` YOKTUR — kamera kabin değiştiremez
 * (geçmiş çekimlerini yanlış kabine bağlardı). `isActive` YOKTUR — pasife alma
 * diyagramdan silmektir.
 */
export interface CameraUpdateRequest {
  id: string;
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

/** Boş metni `undefined`'a çevirir — sunucuda `string?`, boş string değil null gitsin. */
const orUndefined = (value: string) => (value.trim().length > 0 ? value.trim() : undefined);

export function toUpdateRequest(values: CameraFormValues, id: string): CameraUpdateRequest {
  return {
    id,
    name: values.name,
    description: orUndefined(values.description),
    brand: values.brand,
    model: orUndefined(values.model),
    ipAddress: values.ipAddress,
    username: orUndefined(values.username),
    monitoringPort: values.monitoringPort,
    pingIntervalSec: values.pingIntervalSec,
    isMonitoringEnabled: values.isMonitoringEnabled,
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
    isMonitoringEnabled: camera.isMonitoringEnabled
  };
}
