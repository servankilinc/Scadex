namespace Scadex.Business.Utils.OutputPolarity;

/// <summary> Output kanalının NO/NC kutbunu şemanın ŞU ANKİ kablolamasından çözer. </summary>
public interface IOutputPolarityResolver
{
    Task<PolarityResolution> ResolveAsync(Guid ioChannelId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Kanaldaki NO/NC pinleri koşullarına göre sonuçlar:
    /// 1) Nc/NO pinleri yok => Led modülü bu şekilde tanımlıdır L+ ve L- bulunur bu nednele doğrudan aç ise 1 kapat ise 0 gönderilir
    /// 2) Sadece NO pini var => NO kutbu çıkışın aktif olduğu durumda 1, pasif olduğunda 0 verir
    /// 3) Sadece NC pini var => NC kutbu çıkışın aktif olduğu durumda 0, pasif olduğunda 1 verir
    /// 4) Hem NO hem NC pini var => Bu durumda kabloların hangisinin bağlı olduğu çözülür ve ona göre kutup belirlenir
    /// 5) Hem NO hem NC pini var ama kabloların hiçbiri bağlı değil => Bu durumda kutup belirlenemez, çözümleme başarısız olur
    /// 6) Hem NO hem NC pini var ama kabloların her ikisi de bağlı => Bu durumda kutup belirlenemez, çözümleme başarısız olur
    /// </summary>
    Task<IReadOnlyDictionary<Guid, PolarityResolution>> ResolveManyAsync(IReadOnlyCollection<Guid> ioChannelIds, CancellationToken cancellationToken = default);
}
