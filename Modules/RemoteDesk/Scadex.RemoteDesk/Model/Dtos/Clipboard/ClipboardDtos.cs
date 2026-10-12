namespace Scadex.RemoteDesk.Model.Dtos.Clipboard;

/// <summary> Pano işlemi sonucu (RemoteDesk.md § 12.9). İçe aktarmada <see cref="Text"/> uzak panonun metnidir. </summary>
public sealed class ClipboardResult
{
    public bool Success { get; init; }
    public string? Text { get; init; }
    public string? Message { get; init; }
}
