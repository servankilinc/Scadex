/** Ayna: Scadex.Model/Dtos/Diagram/Commands/DiagramSaveRequest.cs — sözleşme: docs/api-contract/03-diagram-save.md */
import type { AnnotationShape, EdgeRouting, LineStyle, PinDirection, WireType } from '@/models/enums';
import type { PointDto } from '../queries/pointDto';

/**
 * Bu dosya, `commands/` klasöründeki zod konvansiyonundan BİLEREK ayrılır.
 *
 * Zod şemaları KULLANICI GİRDİSİNİ doğrulamak için var: hata mesajı bir form
 * alanının altına asılır. Bu gövde ise editörün React Flow state'inden makine
 * tarafından üretilir — bağlanacağı bir form yok, dolayısıyla bir zod parse'ı
 * sunucunun validator'ını kopyalamaktan başka bir iş yapmazdı.
 *
 * Kullanıcının elle yazdığı alanlar (cihaz adı gibi) düzenlendikleri formda
 * doğrulanır, gönderim anında değil.
 */

/**
 * Tek bir varlık ailesinin değişiklik kümesi.
 *
 * **`created`/`updated` ayrımı YOKTUR.** Guid'i istemci ürettiği için bir
 * taslağın yeni mi mevcut mu olduğu sunucuda tek bir yerde — Id veritabanında
 * var mı — cevaplanır. İstemcinin bunu ayrıca takip etmesi gerekmez.
 */
export interface EntityDelta<T> {
  upserted: T[];
  /**
   * Karşılığı bulunamayan Id'ler sunucuda SESSİZCE ATLANIR; 400 dönmez ve yanıtta
   * da raporlanmaz. Bu yüzden buraya hiç kaydedilmemiş bir kaydın Id'si düşse bile
   * gönderi bozulmaz.
   */
  deleted: string[];
}

/**
 * Yeni bir cihazın TEK bir pini için istemcinin ürettiği kimlik.
 *
 * Pin VERİSİ taşımaz: ad, konum, fonksiyon, yön ve gerilim sunucuda
 * `ComponentTemplatePin`'den kopyalanır. Gönderilen `componentTemplatePinId`
 * kümesi şablonun pin şemasına BİREBİR eşit olmalıdır, aksi hâlde 400.
 */
export interface DevicePinDraft {
  id: string;
  componentTemplatePinId: string;
}

/**
 * Yeni bir cihazın TEK bir telemetri kanalı için istemcinin ürettiği kimlik.
 *
 * Pinin içine gömülü DEĞİL: "aynı (yön, kanal numarası) çifti tek bir kanaldır"
 * kuralı böyle yapısal olarak tutarsız ifade edilemez hâle gelir.
 *
 * **Adres kabin genelinde benzersizdir, cihaz içinde değil.** Kabin bir kontrol
 * kartıdır ve kartın adres uzayı düzdür; aynı şablonu bir kabine iki kez bırakmak
 * bu yüzden 400 döner.
 */
export interface DeviceIoChannelDraft {
  id: string;
  /**
   * Yön anahtarın parçası: kartta IN1 ile OUT1 AYRI noktalardır. Yalnızca kimlik
   * eşlemesi için gönderilir — kanalın gerçek yönünü sunucu yine şablon pininden
   * kopyalar.
   */
  direction: PinDirection;
  channelNumber: number;
}

export interface DeviceDraft {
  id: string;
  /**
   * Yalnızca OLUŞTURMADA kullanılır. Mevcut bir cihazın şablonu değiştirilemez —
   * farklı gönderilirse sunucu 400 döner.
   */
  componentTemplateId: string;
  name: string;
  coordinateX: number;
  coordinateY: number;
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
  externalCode: string | null;
  /**
   * Yalnızca OLUŞTURMADA doldurulur; mevcut bir cihazda dolu gönderilirse 400
   * (pinleri zaten var). "Bu cihaz yeni mi" sorusunu `lib/diagram/unsaved-store.ts`
   * cevaplar — Id'nin kendisi cevaplayamaz.
   */
  pins: DevicePinDraft[];
  /** Ad okuma yolundaki `DiagramDeviceDto.ioChannels` ile AYNI: aynı liste okunup geri gönderiliyor. */
  ioChannels: DeviceIoChannelDraft[];
}

export interface ConnectionDraft {
  id: string;
  /**
   * Uçlar mevcut bir kabloda DEĞİŞTİRİLEMEZ (farklı gönderilirse 400); bir
   * kablonun ucunu taşımak sil + oluştur'dur.
   *
   * Uç, kalıcı bir pini de AYNI GÖNDERİDE doğacak bir pini de gösterebilir —
   * pin Id'lerini istemci ürettiği için cihaz bırakılıp ona aynı kaydetmede kablo
   * çizilebiliyor.
   */
  sourcePinId: string;
  targetPinId: string;
  label: string | null;
  wireType: WireType;
  color: string;
  lineStyle: LineStyle;
  strokeWidth: number;
  routing: EdgeRouting;
  waypoints: PointDto[];
  zIndex: number;
}

export interface AnnotationDraft {
  id: string;
  name: string;
  coordinateX: number;
  coordinateY: number;
  width: number;
  height: number;
  rotation: number;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
  text: string;
  shape: AnnotationShape;
  backgroundColor: string;
  fontColor: string;
  fontSize: number;
  isBold: boolean;
  borderColor: string;
}

/**
 * `cabinetId` ROTADAN gider, gövdede YOKTUR.
 *
 * Taslaklar TAM durumdur, patch değil. Burada OLMAYAN alanlar sunucuda
 * dokunulmadan kalır — `deviceStatusId` / `lastSeen` (telemetri) ve `ipAddress` /
 * `macAddress` (cihaz yönetimi) bilerek dışarıda: kaydetmek SCADA'nın yazdığı
 * değerleri ezmemeli.
 */
export interface DiagramSaveRequest {
  devices: EntityDelta<DeviceDraft>;
  connections: EntityDelta<ConnectionDraft>;
  annotations: EntityDelta<AnnotationDraft>;
}

export function emptyDelta<T>(): EntityDelta<T> {
  return { upserted: [], deleted: [] };
}

export function isDeltaEmpty(delta: EntityDelta<unknown>): boolean {
  return delta.upserted.length === 0 && delta.deleted.length === 0;
}

export function isSaveRequestEmpty(request: DiagramSaveRequest): boolean {
  return isDeltaEmpty(request.devices) && isDeltaEmpty(request.connections) && isDeltaEmpty(request.annotations);
}
