/**
 * Ayna: Scadex.Model/Dtos/ChannelEvent/Queries/ChannelEventDto.cs
 * Sözleşme: docs/api-contract/12-channel-events.md
 */

export interface ChannelEventDto {
  /**
   * Sunucuda `long` (IDENTITY). JSON'da sayı olarak iner.
   * Bugünkü hacimlerde `Number.MAX_SAFE_INTEGER` sorunu yok, ama bu bir
   * KİMLİKTİR — üzerinde aritmetik yapılmamalı.
   */
  id: number;
  ioChannelId: string;
  cabinetId: string;

  // Aşağıdaki beş alan TÜREVDİR ve hepsi null olabilir.
  //
  // Saklanan kopyalar değil, okuma anında IoChannel -> Device üzerinden
  // çözülüyorlar: operatör kanalı yeniden adlandırdığında geçmiş olaylar da yeni
  // adla görünür. IoChannel soft-delete taşıdığı için, kanal silindiğinde olay
  // satırı DURUR (silinmiş bir kanalın geçmişi de delildir) ama adı çözülemez.
  //
  // null = "kaynak kanal silinmiş".
  /** Kanalın diyagramdaki adı — "In7" değil "Kapı Sensörü". */
  channelName: string | null;
  channelNumber: number | null;
  deviceId: string | null;
  deviceName: string | null;
  deviceExternalCode: string | null;

  /** Bugün yalnızca "1" / "0". String kalıyor: analog kapsama girdiğinde "23.5" aynı alana düşecek. */
  value: string;
  /** Kanalın ilk okumasında null. */
  previousValue: string | null;

  /** Olayın SAHADA gerçekleştiği an (ingest gövdesindeki `timestampUtc`). */
  occurredAtUtc: string;
  /**
   * Bilginin bize ulaştığı an, sunucu saatiyle.
   * `occurredAtUtc` ile EŞİTSE, SCADA damga göndermemiş demektir.
   */
  receivedAtUtc: string;
}
