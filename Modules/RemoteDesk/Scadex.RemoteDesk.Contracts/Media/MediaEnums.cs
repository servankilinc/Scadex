namespace Scadex.RemoteDesk.Contracts.Media;

/// <summary>
/// Ekran kartı üreticisi — Windows istemcisi DXGI adaptörünün PCI satıcı kimliğinden çözer ve kodlayıcı sırasını buna göre seçer. Sunucu
/// yorumlamaz; bilinmeyen platformda <see cref="Unknown"/>.
/// </summary>
public enum GpuVendor
{
    Unknown = 0,
    Nvidia = 1,
    Intel = 2,
    Amd = 3,
}

/// <summary> Tarayıcıya giden video formatı. Donanımda H.264, yazılımda en son tercih olarak VP9. </summary>
public enum VideoCodec
{
    H264 = 1,
    Vp9 = 2,
}

/// <summary>
/// Kodlayıcı türü — istemciye özgüdür (bugünkü değerler Windows istemcisinin FFmpeg zincirleri). Sunucu yorumlamaz: yayın durumunda kodlayıcı
/// adı metin olarak gelir (<c>StreamStateReport.Encoder</c>). Başka platformun istemcisi (VAAPI, MediaCodec…) yeni değer ekler; mevcut
/// numaralar değişmez.
/// </summary>
public enum EncoderKind
{
    /// <summary> NVIDIA NVENC, H.264 — D3D11 kare doğrudan kodlayıcıya. </summary>
    Nvenc = 1,
    /// <summary> Intel QSV, H.264 — kare GPU'dan hiç çıkmaz (<c>hwmap</c>); ölçekleme yapmaz. </summary>
    QsvGpu = 2,
    /// <summary> Intel QSV, H.264 — kare sistem belleğine indirilir (<c>hwdownload</c>); ölçekleyebilir. </summary>
    QsvDownload = 3,
    /// <summary> AMD AMF, H.264 — D3D11 kare doğrudan kodlayıcıya. </summary>
    Amf = 4,
    /// <summary> libvpx VP9, yazılım — her işlemcide çalışan telifsiz yedek. </summary>
    Vp9Software = 10,
}
