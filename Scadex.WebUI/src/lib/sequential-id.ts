/**
 * İstemci tarafında üretilen kalıcı kayıt kimliği.
 *
 * **Neden `crypto.randomUUID()` değil.** Bu Id'ler doğrudan SQL Server'da
 * `uniqueidentifier` birincil anahtar oluyor ve o anahtarlar kümelenmiş indeksi
 * sürüyor. Tamamen rastgele bir Guid, her eklemeyi indeksin rastgele bir yerine
 * düşürür (sayfa bölünmesi + parçalanma).
 *
 * **Neden UUIDv7 de değil.** v7 zaman damgasını İLK baytlara koyar; SQL Server
 * ise `uniqueidentifier`'ı SON 6 bayttan başlayarak sıralar. Yani v7 bu veritabanında
 * rastgeleden farksızdır. Sıralılık ancak zaman damgası son 6 bayta konursa oluşur —
 * EF Core'un `SequentialGuidValueGenerator`'ının yaptığı da tam olarak budur.
 *
 * Bu fonksiyon o üreteci istemci tarafında birebir taklit eder: Id üretimi sunucudan
 * istemciye taşınırken veritabanının ekleme davranışı DEĞİŞMESİN diye.
 */
export function newId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  const timestamp = Date.now();

  // Zaman damgası SON 6 bayta, büyük-endian: SQL Server'ın karşılaştırma sırası
  // burada başlar, dolayısıyla artan zaman = artan Guid.
  bytes[10] = Math.floor(timestamp / 2 ** 40) & 0xff;
  bytes[11] = Math.floor(timestamp / 2 ** 32) & 0xff;
  bytes[12] = Math.floor(timestamp / 2 ** 24) & 0xff;
  bytes[13] = Math.floor(timestamp / 2 ** 16) & 0xff;
  bytes[14] = Math.floor(timestamp / 2 ** 8) & 0xff;
  bytes[15] = timestamp & 0xff;

  const hex = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
