using Scadex.RemoteDesk.Contracts.Media;
using Scadex.RemoteDesk.Windows.Services.Encoding;

namespace Scadex.RemoteDesk.Windows.Services.Ffmpeg;

/// <summary> FFmpeg komut satırını tek yerde kurar; sınama ve yayın aynı zinciri kullanır (sınamanın anlamı budur). </summary>
public static class FfmpegCommand
{
    /// <summary>
    /// Yakalama + kodlama. Adaptör açıkça verilir (<c>-init_hw_device d3d11va=cap:N</c>) ve <c>ddagrab</c>
    /// <c>-filter_complex</c> içinde çalışır: <c>-f lavfi -i</c> biçimi yalnızca varsayılan adaptörün monitörlerini
    /// görür. Çıkış eklenmez; çağıran ekler.
    /// </summary>
    public static List<string> Capture(MonitorInfo monitor, EncoderCandidate candidate, VideoProfile profile)
    {
        string filters = candidate.Filters(profile);
        string graph = $"ddagrab=output_idx={monitor.OutputIndex}:framerate={profile.Fps}:draw_mouse=1" +
                       (filters.Length > 0 ? "," + filters : "");

        var args = new List<string>
        {
            "-hide_banner", "-loglevel", "error",
            "-init_hw_device", $"d3d11va=cap:{monitor.AdapterIndex}", "-filter_hw_device", "cap",
            "-filter_complex", graph,
        };
        args.AddRange(candidate.CodecArgs(profile));
        args.AddRange([
            "-b:v", $"{profile.BitrateKbps}k", "-maxrate", $"{profile.BitrateKbps}k", "-bufsize", $"{profile.BitrateKbps}k",
            "-g", $"{profile.GopFrames}",
        ]);
        // FFmpeg'in RTP VP9 paketleyicisi deneysel işaretli: -strict experimental olmadan RTSP'ye yazılmaz.
        if (candidate.Codec == VideoCodec.Vp9) args.AddRange(["-strict", "experimental"]);
        // ZORUNLU: sabit kare hızında kodlayıcı yetişemeyince kopya kareler birikir, gecikme sınırsız büyür (§ 7.4).
        args.AddRange(["-fps_mode", "passthrough"]);
        return args;
    }

    /// <summary> Sınama: birkaç kare kodla ve at — ekran yakalama dahil tüm zincir gerçekten çalışıyor mu? </summary>
    public static List<string> Probe(MonitorInfo monitor, EncoderCandidate candidate)
    {
        var args = Capture(monitor, candidate, candidate.DefaultProfile);
        // Yalnızca sınamada -nostdin: yayın stdin'e 'q' yazılarak nazikçe durdurulur, orada stdin açık kalmalı.
        args.Insert(0, "-nostdin");
        args.AddRange(["-frames:v", "8", "-f", "null", "-"]);
        return args;
    }
}
