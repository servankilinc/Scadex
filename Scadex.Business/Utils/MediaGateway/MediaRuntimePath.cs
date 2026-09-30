namespace Scadex.Business.Utils.MediaGateway;

/// <summary> MediaMTX'te o an var olan bir yolun durumu (<c>v3/paths/get</c>). </summary>
/// <param name="IsReady">Yayıncı bağlı ve akış var — WHEP okuyucusu ancak bundan sonra bağlanabilir.</param>
/// <param name="ReaderCount">O anki okuyucu sayısı (WebRTC, RTSP…).</param>
/// <param name="SourceType">Yayıncının türü (ör. <c>rtspSession</c>); yayıncı yoksa <c>null</c>.</param>
/// <param name="SourceId">Yayıncının MediaMTX kimliği — kick bununla yapılır.</param>
public sealed record MediaRuntimePath(string Name, bool IsReady, int ReaderCount, string? SourceType, string? SourceId);
