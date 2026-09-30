using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Scadex.DataAccess.Migrations
{
    /// <summary>
    /// Yalnızca VERİ dönüşümü (model değişmedi): mevcut <c>Device.MacAddress</c> değerleri tek tipe çevrilir — <c>AA:BB:CC:DD:EE:FF</c>
    /// (büyük harf, <c>:</c> ayraç). Kural <c>Scadex.Core.Utils.MacAddressFormat</c> ile AYNIDIR: <c>- : .</c> ve boşluk atılır, 12 harf/rakam
    /// kalıyorsa ikişerli <c>:</c> ile yazılır; kalmıyorsa dokunulmaz (kaydedilirken doğrulayıcı reddeder). Boş metin <c>NULL</c> olur.
    /// <para/>
    /// Çakışma emniyeti: tek tipe çevrilince başka bir AKTİF kayıtla aynı adrese düşen aktif satıra dokunulmaz — yoksa
    /// <c>IX_Device_MacAddress</c> (unique, aktifler) migration'ı yarıda keserdi. Bu satırlar diyagramdan elle düzeltilmeli; kaydederken
    /// benzersizlik ön kontrolü onları "başka cihaza kayıtlı" diye gösterir.
    /// </summary>
    public partial class NormalizeDeviceMacAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Device] SET [MacAddress] = NULL
                WHERE [MacAddress] IS NOT NULL AND LTRIM(RTRIM([MacAddress])) = '';

                WITH [compact] AS (
                    SELECT [Id], [IsActive],
                           UPPER(REPLACE(REPLACE(REPLACE(REPLACE([MacAddress], '-', ''), ':', ''), '.', ''), ' ', '')) AS [c]
                    FROM [Device]
                    WHERE [MacAddress] IS NOT NULL
                ),
                [canonical] AS (
                    SELECT [Id], [IsActive],
                           CASE WHEN LEN([c]) = 12 AND [c] NOT LIKE '%[^0-9A-Z]%' COLLATE Latin1_General_BIN
                                THEN CONCAT(SUBSTRING([c], 1, 2), ':', SUBSTRING([c], 3, 2), ':', SUBSTRING([c], 5, 2), ':',
                                            SUBSTRING([c], 7, 2), ':', SUBSTRING([c], 9, 2), ':', SUBSTRING([c], 11, 2))
                           END AS [mac]
                    FROM [compact]
                )
                UPDATE [d] SET [MacAddress] = [n].[mac]
                FROM [Device] AS [d]
                JOIN [canonical] AS [n] ON [n].[Id] = [d].[Id]
                WHERE [n].[mac] IS NOT NULL
                  AND [d].[MacAddress] <> [n].[mac] COLLATE Latin1_General_BIN
                  AND NOT ([d].[IsActive] = 1 AND EXISTS (
                        SELECT 1 FROM [canonical] AS [o]
                        WHERE [o].[mac] = [n].[mac] AND [o].[IsActive] = 1 AND [o].[Id] <> [n].[Id]));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alınamaz: eski ayraçlar saklanmadı. Tek tip değer her iki yönde de geçerli bir MAC yazımıdır.
        }
    }
}
