namespace Scadex.RemoteDesk.Contracts.Media;

/// <summary>
/// Yayının kalite ayarı. İleride sunucudan <c>StartScreenStream</c> ile gelir; istemci kodlayıcı yetişemezse
/// (geri kalma bekçisi) bir alt basamağa iner.
/// </summary>
public sealed record VideoProfile(int Fps, int MaxWidth, int BitrateKbps)
{
    /// <summary> Donanım kodlayıcılarının varsayılanı. </summary>
    public static VideoProfile HardwareDefault { get; } = new(30, 1920, 3000);

    /// <summary> Yazılım (VP9) varsayılanı — 1080p30'da. </summary>
    public static VideoProfile SoftwareDefault { get; } = new(15, 1600, 2000);

    /// <summary> GOP ≈ 2 sn: yeni izleyici en geç 2 sn'de görüntü alır. </summary>
    public int GopFrames => Fps * 2;

    public override string ToString() => $"{Fps} fps · ≤{MaxWidth} px · {BitrateKbps} kbps";
}
