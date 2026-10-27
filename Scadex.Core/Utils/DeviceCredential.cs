using System.Security.Cryptography;
using System.Text;

namespace Scadex.Core.Utils;

/// <summary>
/// Sahadaki cihazın (kontrol kartı, RemoteDesk PC'si) Basic Auth kimliği
/// Kullanıcı adı cihazın Id'sidir; parola sistemin ürettiği <see cref="SecretLength"/> karakterlik onaltılık sırdır (128 bit).
/// Sunucuda yalnızca SHA-256 hash'i saklanır. Sır yüksek entropili olduğu için yavaş hash (PBKDF2) gerekmez:
/// doğrulama ingest'in sıcak yolundadır.
/// <para/>
/// Sırrı tarayıcı üretir (diyagram paneli) ve diyagramın cihaz uçlarıyla gönderir; bu sınıf yalnızca BİÇİMİ zorlar ki elle zayıf
/// parola girilemesin.
/// </summary>
public static class DeviceCredential
{
    public const int SecretLength = 32;

    /// <summary> Tam olarak <see cref="SecretLength"/> küçük harf onaltılık karakter. </summary>
    public static bool IsValidSecret(string? secret)
    {
        if (secret is null || secret.Length != SecretLength)
            return false;

        foreach (char c in secret)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }
        return true;
    }

    /// <summary> Saklanan biçim: SHA-256, büyük harf onaltılık (64 karakter). </summary>
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary> Sabit zamanlı karşılaştırma; hash yoksa (kimlik bilgisi iptal edilmiş) her zaman <c>false</c>. </summary>
    public static bool Verify(string secret, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return false;

        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        byte[] expected;
        try { expected = Convert.FromHexString(storedHash); }
        catch (FormatException) { return false; }

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
