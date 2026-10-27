/** Ayna: Scadex.Model/Dtos/Diagram/Commands/*.cs — editörün her eylemi kendi ucuna anında gider (2026-10-27). */
import type { AnnotationShape, CameraBrand, EdgeRouting, LineStyle, WireType } from '@/models/enums';
import type { PointDto } from '../queries/pointDto';

/**
 * Bu dosya, `commands/` klasöründeki zod konvansiyonundan BİLEREK ayrılır: gövdeler editörün React Flow state'inden
 * makine tarafından üretilir. Kullanıcının elle yazdığı alanlar (cihaz adı gibi) düzenlendikleri formda doğrulanır.
 *
 * `cabinetId` her uçta ROTADAN gider, gövdede YOKTUR.
 */

// ──────────────────────────────────────────────────────────────── cihaz

/**
 * Ayna: `DeviceExtensionDrafts.cs`. Sunucu kuralları: model ≤ 64, açıklama ≤ 512, kullanıcı/parola ≤ 128.
 * `password`: `null` = mevcut parolaya dokunma, `''` = temizle, değer = yaz.
 */
export interface CameraDraft {
  brand: CameraBrand;
  model: string | null;
  description: string | null;
  username: string | null;
  password: string | null;
}

/**
 * Basic Auth sırrı (kullanıcı adı cihazın Id'si). `null` = mevcut kimliğe dokunma, `''` = iptal, değer = yeni sır —
 * yalnızca `generateDeviceSecret()`'in ürettiği biçim kabul edilir (32 küçük harf onaltılık), aksi 400 `ControlCard.Secret`.
 */
export interface ControlCardDraft {
  secret: string | null;
}

/** `ControlCardDraft` ile aynı kural; hata anahtarı `Pc.Secret`. */
export interface PcDeviceDraft {
  secret: string | null;
}

/**
 * Cihaz formunun Kaydet'i: `PUT …/devices/{id}`. Konum, boyut, z-sırası, kilit ve görünürlük burada DEĞİL
 * (`DiagramLayoutRequest`). `deviceStatusId` / `lastSeen` / `lastConnectionError` bilerek yok: onları yoklama ve SCADA teması yazar.
 *
 * İzleme kuralları (`DeviceUpdateRequestValidator`): port 1..65535 (boş = ICMP ping), periyot 5..86400 sn, izleme açıkken
 * `ipAddress` zorunlu. Ek nesneler: şablon tipine göre TAM OLARAK biri dolu (kamera / kontrol modülü / PC), diğer tiplerde
 * üçü de `null`; eksik ya da yanlış tipe giden nesne 400'dür.
 */
export interface DeviceUpdateRequest {
  name: string;
  ipAddress: string | null;
  monitoringPort: number | null;
  pingIntervalSec: number;
  isMonitoringEnabled: boolean;
  camera: CameraDraft | null;
  controlCard: ControlCardDraft | null;
  pc: PcDeviceDraft | null;
}

/**
 * Paletten bırakma ve kopyalama: `POST …/devices`. Id'yi istemci üretir (`newId()`); pinleri ve kanalları SUNUCU şablondan
 * üretir ve yanıttaki `DiagramDeviceDto`'da döndürür — istemci pin kimliği göndermez.
 */
export interface DeviceCreateRequest extends DeviceUpdateRequest {
  id: string;
  componentTemplateId: string;
  coordinateX: number;
  coordinateY: number;
  /** `null` = şablon boyutunu takip et (bir kopyası DEĞİL). */
  width: number | null;
  height: number | null;
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
}

// ──────────────────────────────────────────────────────────────── kablo

/** Kablonun görünümü: `PUT …/connections/{id}`. Uçlar yok — ucu değiştirmek silip yeniden çizmektir. */
export interface ConnectionUpdateRequest {
  label: string | null;
  wireType: WireType;
  color: string;
  lineStyle: LineStyle;
  strokeWidth: number;
  routing: EdgeRouting;
  waypoints: PointDto[];
  zIndex: number;
}

/** Kablo çizme: `POST …/connections`. İki uç da bu kabinde KALICI pin olmalı (cihaz bırakılınca anında oluşturulur). */
export interface ConnectionCreateRequest extends ConnectionUpdateRequest {
  id: string;
  sourcePinId: string;
  targetPinId: string;
}

// ──────────────────────────────────────────────────────────────── not

/** Notun içeriği: `PUT …/annotations/{id}`. Yerleşim alanları `DiagramLayoutRequest`'tedir. */
export interface AnnotationUpdateRequest {
  name: string;
  text: string;
  shape: AnnotationShape;
  backgroundColor: string;
  fontColor: string;
  fontSize: number;
  isBold: boolean;
  borderColor: string;
}

export interface AnnotationCreateRequest extends AnnotationUpdateRequest {
  id: string;
  coordinateX: number;
  coordinateY: number;
  width: number;
  height: number;
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
}

// ──────────────────────────────────────────────────────────── yerleşim / silme

export interface DeviceLayoutItem {
  id: string;
  coordinateX: number;
  coordinateY: number;
  /** `null` "dokunma" DEĞİL, "şablon boyutuna dön" demektir. */
  width: number | null;
  height: number | null;
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
}

export interface AnnotationLayoutItem {
  id: string;
  coordinateX: number;
  coordinateY: number;
  width: number;
  height: number;
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
}

/** `PUT …/layout`: diyagramın kalan tek toplu ucu; referans taşımaz, her kayıt yalnızca kendi satırını yazar. */
export interface DiagramLayoutRequest {
  devices: DeviceLayoutItem[];
  annotations: AnnotationLayoutItem[];
}

/**
 * `POST …/delete`: cihazın pinleri ve kabloları sunucuda birlikte kalkar. Bu kabinde karşılığı olmayan Id sessizce atlanır.
 */
export interface DiagramDeleteRequest {
  deviceIds: string[];
  connectionIds: string[];
  annotationIds: string[];
}
