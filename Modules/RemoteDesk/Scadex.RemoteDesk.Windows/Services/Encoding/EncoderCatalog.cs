using Scadex.RemoteDesk.Contracts.Media;

namespace Scadex.RemoteDesk.Windows.Services.Encoding;

/// <summary>
/// Bir kodlayıcı adayının FFmpeg karşılığı. Zincir <c>ddagrab</c>'dan sonra gelen filtrelerdir; kodlayıcı ayarları
/// Faz 1 ölçümleriyle sabitlenmiştir.
/// </summary>
public sealed class EncoderCandidate
{
    public required EncoderKind Kind { get; init; }
    /// <summary> Donanım adayının üreticisi; yazılım adayında <see cref="GpuVendor.Unknown"/>. </summary>
    public required GpuVendor Vendor { get; init; }
    public required VideoCodec Codec { get; init; }
    public required string FfmpegEncoder { get; init; }
    public required string DisplayName { get; init; }
    /// <summary> Karenin yolu, arayüzde gösterilir (GPU'da / hwdownload / yazılım). </summary>
    public required string Chain { get; init; }
    public bool IsHardware => Vendor != GpuVendor.Unknown;
    /// <summary> GPU'da kalan zincirler ölçekleme yapmaz: genişlik profili yok sayılır. </summary>
    public required bool CanScale { get; init; }
    public required VideoProfile DefaultProfile { get; init; }

    /// <summary> <c>ddagrab</c>'dan sonra eklenecek filtreler (boş olabilir). </summary>
    public required Func<VideoProfile, string> Filters { get; init; }
    /// <summary> <c>-c:v</c> ve kodlayıcıya özgü ayarlar (bit hızı ve GOP ortak olarak eklenir). </summary>
    public required Func<VideoProfile, string[]> CodecArgs { get; init; }
}

public static class EncoderCatalog
{
    private static string Scale(VideoProfile p, string pixelFormat) =>
        $"hwdownload,format=bgra,scale='min({p.MaxWidth},iw)':-2,format={pixelFormat}";

    // async_depth 1 ZORUNLU: varsayılan 4 kare tampon ~300 ms ekler ve tarayıcıda jitter buffer'ı şişirir (§ 7.4).
    private static readonly string[] QsvArgs =
        ["-c:v", "h264_qsv", "-profile:v", "main", "-preset", "veryfast", "-async_depth", "1", "-look_ahead", "0", "-bf", "0"];

    public static EncoderCandidate Nvenc { get; } = new()
    {
        Kind = EncoderKind.Nvenc, Vendor = GpuVendor.Nvidia, Codec = VideoCodec.H264, FfmpegEncoder = "h264_nvenc",
        DisplayName = "NVIDIA NVENC", Chain = "GPU'da (kopyasız)", CanScale = false, DefaultProfile = VideoProfile.HardwareDefault,
        Filters = _ => "",
        CodecArgs = _ => ["-c:v", "h264_nvenc", "-preset", "p1", "-tune", "ull", "-zerolatency", "1", "-delay", "0", "-rc", "cbr", "-bf", "0"],
    };

    public static EncoderCandidate QsvGpu { get; } = new()
    {
        Kind = EncoderKind.QsvGpu, Vendor = GpuVendor.Intel, Codec = VideoCodec.H264, FfmpegEncoder = "h264_qsv",
        DisplayName = "Intel QSV", Chain = "GPU'da (kopyasız)", CanScale = false, DefaultProfile = VideoProfile.HardwareDefault,
        Filters = _ => "hwmap=derive_device=qsv,format=qsv",
        CodecArgs = _ => QsvArgs,
    };

    public static EncoderCandidate QsvDownload { get; } = new()
    {
        Kind = EncoderKind.QsvDownload, Vendor = GpuVendor.Intel, Codec = VideoCodec.H264, FfmpegEncoder = "h264_qsv",
        DisplayName = "Intel QSV", Chain = "hwdownload (kare CPU belleğinden)", CanScale = true, DefaultProfile = VideoProfile.HardwareDefault,
        Filters = p => Scale(p, "nv12"),
        CodecArgs = _ => QsvArgs,
    };

    public static EncoderCandidate Amf { get; } = new()
    {
        Kind = EncoderKind.Amf, Vendor = GpuVendor.Amd, Codec = VideoCodec.H264, FfmpegEncoder = "h264_amf",
        DisplayName = "AMD AMF", Chain = "GPU'da (kopyasız)", CanScale = false, DefaultProfile = VideoProfile.HardwareDefault,
        Filters = _ => "",
        CodecArgs = _ => ["-c:v", "h264_amf", "-usage", "ultralowlatency", "-rc", "cbr", "-bf", "0"],
    };

    public static EncoderCandidate Vp9Software { get; } = new()
    {
        Kind = EncoderKind.Vp9Software, Vendor = GpuVendor.Unknown, Codec = VideoCodec.Vp9, FfmpegEncoder = "libvpx-vp9",
        DisplayName = "VP9 yazılım (libvpx)", Chain = "yazılım (işlemcide)", CanScale = true, DefaultProfile = VideoProfile.SoftwareDefault,
        Filters = p => Scale(p, "yuv420p"),
        CodecArgs = _ => ["-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", "-row-mt", "1", "-tile-columns", "2",
            "-lag-in-frames", "0", "-error-resilient", "1", "-auto-alt-ref", "0"],
    };


    /// <summary> Bir üreticinin donanım adayları, kendi içinde tercih sırasıyla (QSV: önce kopyasız). </summary>
    public static IReadOnlyList<EncoderCandidate> ForVendor(GpuVendor vendor) => vendor switch
    {
        GpuVendor.Nvidia => [Nvenc],
        GpuVendor.Intel => [QsvGpu, QsvDownload],
        GpuVendor.Amd => [Amf],
        _ => [],
    };

    /// <summary>
    /// RemoteDesk.md § 7.4 seçim sırası: önce monitörün bağlı olduğu kartın kodlayıcısı, sonra makinedeki diğer
    /// kartlarınki, en sonda yazılım VP9. Sıra yalnızca öncelik verir; her aday ayrıca gerçek denemeyle sınanır.
    /// </summary>
    public static IReadOnlyList<EncoderCandidate> OrderFor(MonitorInfo monitor, IEnumerable<GpuVendor> machineVendors)
    {
        var vendors = new List<GpuVendor> { monitor.GpuVendor };
        vendors.AddRange(machineVendors.Where(v => v != monitor.GpuVendor).Distinct());
        return [.. vendors.SelectMany(ForVendor), Vp9Software];
    }
}
