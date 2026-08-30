namespace Scadex.Core.Utils.Caching;

public class CacheSettings
{
    /// <summary> Erişilmeden önbellekte kalabileceği süre (dakika cinsinden). </summary>
    public int SlidingExpirationMinutes { get; set; } = 30;
    /// <summary> Erişim sıklığından bağımsız olarak kalabileceği maksimum süre (dakika cinsinden). </summary>
    public int AbsoluteExpirationMinutes { get; set; } = 120;
}
