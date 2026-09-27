/** Ayna: Scadex.Model/Dtos/MediaGatewaySetting/Queries/MediaGatewaySettingDto.cs */

/**
 * Medya geçidi (MediaMTX) ayarları. Kameraya YALNIZCA MediaMTX bağlanır: canlı izleme
 * tarayıcıya WebRTC ile, anlık görüntü ve klip sunucudaki FFmpeg'e aynı yoldan (biletli RTSP) gider.
 *
 * Bu değerler `appsettings.json`'dan DEĞİL veritabanından gelir ve tek satırlık bir
 * tabloda durur; sunucu tarafında `ICacheService` ile önbelleklenir, her yazma kendi
 * anahtarını düşürür. Yani kaydettiğiniz an etkilidir — yeniden başlatma gerekmez.
 */
export interface MediaGatewaySettingDto {
  /** Control API çağrılarının zaman aşımı. */
  apiTimeoutMs: number;
  /** MediaMTX Control API kökü — `http://127.0.0.1:9997` gibi. */
  apiBaseUrl: string;
  /** Tarayıcının WHEP isteğini attığı genel adres; Control API'den FARKLI bir porttur. */
  webRtcPublicBaseUrl: string;
  /** İzleme biletinin ömrü. */
  tokenTtlSeconds: number;

  /**
   * Son izleyici ayrıldıktan sonra RTSP oturumunun kapanma süresi. Yol SİLİNMEZ,
   * yalnızca kameraya giden oturum kapanır.
   *
   * Sunucuda kolonda `"10s"` biçiminde durur; dönüşüm AutoMapper profilinde yapılır,
   * istemci yalnızca saniye görür.
   */
  sourceOnDemandCloseAfterSec: number;

  /** MediaMTX'in tanıdığı dört değerden biri — bkz. `RTSP_TRANSPORTS`. */
  rtspTransport: string;
}
