namespace Scadex.RemoteDesk.Contracts.Hub;

/// <summary> Uzak PC panosuna yazılacak metin (panolar arası aktarım). </summary>
public sealed record SetClipboardCommand(string Text);
