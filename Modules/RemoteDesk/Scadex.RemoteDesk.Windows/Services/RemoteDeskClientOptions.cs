namespace Scadex.RemoteDesk.Windows.Services;

/// <summary> appsettings.json </summary>
public sealed class RemoteDeskClientOptions
{
    public const string Section = "RemoteDesk";

    /// <summary> Scadex merkezinin adresi (PcHub — Faz 3). </summary>
    public string CentralApiUrl { get; set; } = "";
}
