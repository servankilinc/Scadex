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
 */
import { z } from 'zod';
import type { CameraDto } from '../queries/cameraDto';

// Sayısal alanlarda `z.coerce.number()` KULLANILMIYOR: zod v4'te coerce girdi
// tipini `unknown` yapıyor ve `zodResolver` ile birlikte formun tipi çıktı
// tipinden ayrışıyor (derleme hatası). Kod tabanının mevcut konvansiyonu düz
// `z.number()` + formda `register(..., { valueAsNumber: true })` —
// `cabinetForm.ts`'teki `scadaCommandTimeoutMs` aynı şekilde tanımlı.
const port = (label: string) =>
  z.number(`${label} sayı olmalı`).int(`${label} tam sayı olmalı`).min(1, `${label} 1-65535 arasında olmalı`).max(65535, `${label} 1-65535 arasında olmalı`);

const channel = (label: string) => z.number(`${label} sayı olmalı`).int(`${label} tam sayı olmalı`).positive(`${label} sıfırdan büyük olmalı`);

/**
 * Boş bırakılabilen port alanı.
 *
 * Formda `setValueAs` ile boş girdi `null`'a çevriliyor; `valueAsNumber`
 * kullanılsaydı boş input `NaN` üretir ve kullanıcı "sayı olmalı" diye anlamsız
 * bir hata görürdü — oysa alanı boş bırakmak geçerli.
 */
const optionalPort = (label: string) => port(label).nullable();

export const cameraFormSchema = z
  .object({
    name: z.string().trim().min(1, 'İsim girilmeli').max(150, 'İsim en fazla 150 karakter olabilir'),
    description: z.string().trim().max(512, 'Açıklama en fazla 512 karakter olabilir'),
    manufacturer: z.string().trim().max(64, 'Üretici en fazla 64 karakter olabilir'),
    model: z.string().trim().max(64, 'Model en fazla 64 karakter olabilir'),

    ipAddress: z.string().trim().min(1, 'IP adresi girilmeli').max(64, 'IP adresi en fazla 64 karakter olabilir'),
    rtspPort: port('RTSP portu'),
    httpPort: port('HTTP portu'),
    httpsPort: optionalPort('HTTPS portu'),

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

    mainStreamChannel: channel('Ana akım kanalı'),
    subStreamChannel: channel('Tali akım kanalı'),
    mainStreamEnabled: z.boolean(),
    subStreamEnabled: z.boolean(),
    snapshotChannel: channel('Anlık görüntü kanalı'),

    monitoringPort: optionalPort('İzleme portu'),
    pingIntervalSec: z
      .number('Yoklama aralığı sayı olmalı')
      .int('Yoklama aralığı tam sayı olmalı')
      .min(10, 'Yoklama aralığı en az 10 saniye olmalı')
      .max(86400, 'Yoklama aralığı en fazla 24 saat olabilir'),
    isMonitoringEnabled: z.boolean(),

    /** Yalnızca düzenlemede anlamlı; ekleme her zaman aktif doğurur. */
    isActive: z.boolean()
  })
  // Sunucudaki kuralın aynısı: ikisi de kapalıysa kamera hiç izlenemez ve
  // arayüz sebebini gösteremez. Hata `mainStreamEnabled`'a bağlanıyor ki forma
  // inline düşsün.
  .refine(v => v.mainStreamEnabled || v.subStreamEnabled, {
    message: 'Ana akım ve tali akım aynı anda kapatılamaz',
    path: ['mainStreamEnabled']
  });

export type CameraFormValues = z.infer<typeof cameraFormSchema>;

/** Yeni kamera formunun başlangıç değerleri — sunucudaki DTO varsayılanlarıyla aynı. */
export const emptyCameraForm: CameraFormValues = {
  name: '',
  description: '',
  manufacturer: 'Hikvision',
  model: '',
  ipAddress: '',
  rtspPort: 554,
  httpPort: 80,
  httpsPort: null,
  username: '',
  password: '',
  mainStreamChannel: 101,
  subStreamChannel: 102,
  mainStreamEnabled: true,
  subStreamEnabled: true,
  snapshotChannel: 101,
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
  manufacturer?: string;
  model?: string;
  ipAddress: string;
  rtspPort: number;
  httpPort: number;
  httpsPort: number | null;
  username?: string;
  password?: string;
  mainStreamChannel: number;
  subStreamChannel: number;
  mainStreamEnabled: boolean;
  subStreamEnabled: boolean;
  snapshotChannel: number;
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
    manufacturer: orUndefined(values.manufacturer),
    model: orUndefined(values.model),
    ipAddress: values.ipAddress,
    rtspPort: values.rtspPort,
    httpPort: values.httpPort,
    httpsPort: values.httpsPort,
    username: orUndefined(values.username),
    password: orUndefined(values.password),
    mainStreamChannel: values.mainStreamChannel,
    subStreamChannel: values.subStreamChannel,
    mainStreamEnabled: values.mainStreamEnabled,
    subStreamEnabled: values.subStreamEnabled,
    snapshotChannel: values.snapshotChannel,
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
    manufacturer: camera.manufacturer ?? '',
    model: camera.model ?? '',
    ipAddress: camera.ipAddress,
    rtspPort: camera.rtspPort,
    httpPort: camera.httpPort,
    httpsPort: camera.httpsPort,
    username: camera.username ?? '',
    password: '',
    mainStreamChannel: camera.mainStreamChannel,
    subStreamChannel: camera.subStreamChannel,
    mainStreamEnabled: camera.mainStreamEnabled,
    subStreamEnabled: camera.subStreamEnabled,
    snapshotChannel: camera.snapshotChannel,
    monitoringPort: camera.monitoringPort,
    pingIntervalSec: camera.pingIntervalSec,
    isMonitoringEnabled: camera.isMonitoringEnabled,
    isActive: camera.isActive
  };
}
