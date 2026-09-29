using System.Diagnostics;
using System.IO;

namespace Scadex.RemoteDesk.Windows.Services.Ffmpeg;

/// <summary> Uygulamayla gelen FFmpeg'in yeri, sürümü ve lisansı. </summary>
public sealed record FfmpegInfo(string Path, bool Exists, string? Version, bool IsGpl);

public interface IFfmpegLocator
{
    FfmpegInfo Info { get; }
}

/// <summary>
/// FFmpeg uygulama klasörüne göreli sabit yerdedir (<c>tools\ffmpeg\ffmpeg.exe</c>); ayar değildir.
/// İstemciyle dağıtılan build LGPL olmalıdır — GPL build bulunursa arayüz uyarır.
/// </summary>
public sealed class FfmpegLocator : IFfmpegLocator
{
    private readonly Lazy<FfmpegInfo> _info = new(Inspect);

    public FfmpegInfo Info => _info.Value;

    public static string ExecutablePath => System.IO.Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");

    private static FfmpegInfo Inspect()
    {
        string path = ExecutablePath;
        if (!File.Exists(path)) return new FfmpegInfo(path, false, null, false);

        var psi = new ProcessStartInfo(path) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-version");
        using var proc = Process.Start(psi)!;
        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);

        string firstLine = output.Split('\n', 2)[0].Trim();
        return new FfmpegInfo(path, true, firstLine.Replace("ffmpeg version ", "").Split(' ')[0], output.Contains("--enable-gpl"));
    }
}
