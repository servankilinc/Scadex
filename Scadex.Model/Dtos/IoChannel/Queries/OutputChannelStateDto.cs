using Scadex.Core.Model;

namespace Scadex.Model.Dtos.IoChannel.Queries;

/// <summary> Bir output kanalının son durumu <see cref="CurrentValue"/>, <see cref="IsOn"/> şemanın ŞU ANKİ NO/NC kablolamasına göre yorumudur ("yük açık mı"). </summary>
public class OutputChannelStateDto : IDto
{
    public Guid IoChannelId { get; set; }
    public string? CurrentValue { get; set; }
    public DateTime? ValueUpdatedAt { get; set; }

    /// <summary> <c>null</c> = bilinmiyor: kanal yok/pasif, hiç başarılı komut görmemiş ya da kutbu çözülemiyor (hem NO hem NC kablolu). </summary>
    public bool? IsOn { get; set; }
}
