using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Camera.Queries;

public class StreamTokenDto : IDto
{
    /// <summary> 
    /// Tarayicinin SDP teklifini gonderecegi WHEP adresi. <para/> 
    /// <c>WebRtcPublicBaseUrl</c>'den üretilir => <b>"{WebRtcPublicBaseUrl}/{pathName}/whep"</b>
    /// </summary>
    public string WhepUrl { get; set; } = null!;
    /// <summary>
    /// Istemci token bilgisini <c>Authorization: Basic base64("token:" + token)</c> olarak gonderir; Medya gecidi(MediaMTX) de dogrulamak icin bize geri sorar.
    /// </summary>
    public string Token { get; set; } = null!;
    public DateTime ExpirationUtc { get; set; }
}
