/**
 * Ayna: Scadex.Model/Dtos/Camera/Queries/StreamTokenDto.cs
 * Sözleşme: docs/api-contract/11-camera.md
 */
export interface StreamTokenDto {
  /**
   * Tarayıcının SDP teklifini göndereceği WHEP adresi.
   *
   * Sunucunun değil TARAYICININ ulaşacağı adres. Bağlantı kurulamıyorsa ilk
   * bakılacak yer sunucudaki `Mediamtx:WebRtcPublicBaseUrl` ayarıdır —
   * `127.0.0.1` yazılıysa yalnızca sunucunun kendi tarayıcısında çalışır.
   */
  whepUrl: string;

  /**
   * Opak bilet. `Authorization: Basic base64("ticket:" + token)` olarak
   * gönderilir — kullanıcı adı kısmı sabit `ticket` dizesidir, alanın adı değil.
   *
   * Yola bağlıdır: A kamerası için alınan bilet B'nin adresinde çalışmaz.
   */
  token: string;

  /**
   * Biletin son geçerlilik anı (ISO-8601 UTC).
   *
   * Bağlantı koptuğunda eski bilet SAKLANMAZ; yeniden bağlanma her denemede
   * yenisini ister (bkz. `lib/camera/stream-session.ts`).
   */
  expirationUtc: string;
}
