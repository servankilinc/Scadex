/** Ayna: Scadex.Model/Dtos/DeviceCommand/Commands/DeviceCommandSendRequest.cs — sözleşme: docs/api-contract/08-scada-command.md */
import type { DeviceCommandType } from '@/models/enums';

/**
 * `POST /api/Device/{deviceId}/command` gövdesi.
 *
 * **Payload tiplidir, ham JSON string değil.** `DeviceCommand.PayloadJson` bir
 * string kolonu ve onu doğrudan göndermek daha az kod olurdu; o şekilde gövdenin
 * sahaya ne gönderdiği doğrulanamazdı — istemcinin yazdığı metin olduğu gibi röle
 * süren bir sisteme geçerdi. `payloadJson`'ı sunucu bu alanlardan kendisi kurar.
 */
export interface DeviceCommandSendRequest {
  commandType: DeviceCommandType;
  /** Hedef kanal — zorunlu. Kanalsız, modül geneline giden bir kumanda artık yok. */
  ioChannelId: string;
  /**
   * NİYET — zorunlu. `true` = "yükü ver", `false` = "yükü kes".
   *
   * **Neden ham değer değil.** Karta giden bayt rölenin BOBİNİNİ sürer; yükün
   * devresinin kapanıp kapanmadığı yükün NO mu NC kontağa kablolandığına bağlıdır.
   * Yani "aç" ile telde giden `"1"`/`"0"` arasındaki eşleme sabit DEĞİLDİR ve
   * eskiden bu kararı operatöre yıkıyorduk. Telde gidecek değeri artık sunucu
   * `Pin.Function`'daki NO/NC'den çözüyor; ne gönderdiğini `sentValue` ile
   * geri bildiriyor.
   */
  turnOn: boolean;
}
