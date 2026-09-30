namespace Scadex.RemoteDesk.Media;

/// <summary> PC ekran yayınının MediaMTX yolu: <c>pc_{deviceId:N}_{monitör}</c> </summary>
public static class PcMediaPath
{
    public const string Prefix = "pc_";

    public static string Name(Guid deviceId, int monitorIndex) => $"{Prefix}{deviceId:N}_{monitorIndex}";

    public static bool IsPcPath(string path) => path.StartsWith(Prefix, StringComparison.Ordinal);
}
