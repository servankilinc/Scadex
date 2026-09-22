namespace Scadex.Signalization.Model.Utils;

/// <summary>
/// Bir inputun ya da output kanalından okunmuş durum bilgisi: kapı açık mı, siren çalıyor mu, kilit açık mı vb.
/// <para/>
/// <see cref="IsOn"/> = <c>null</c> "bilinmiyor" demektir, <c>false</c> ile aynı şey DEĞİLDİR: kanal yok/pasif, hiç değer okunmamış ya da çıkış hiç başarılı komut görmemiş olabilir.
/// </summary>
/// <param name="IsOn"> Kapı açık / siren çalıyor / LED yanıyor / kilit AÇIK. </param>
/// <param name="ChangedAtUtc"> Kanalın bu değere geçtiği an (<c>IoChannel.ValueUpdatedAt</c>). </param>
public readonly record struct ChannelReading(bool? IsOn, DateTime? ChangedAtUtc)
{
    public static ChannelReading Unknown => new(null, null);
}
