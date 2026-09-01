using Scadex.Core.Model;

namespace Scadex.Model.Dtos.Camera.Commands;

public class MediaMtxAuthDto : IDto
{
    /// <summary>MediaMTX'in oturum kimligi.</summary>
    public string? Id { get; set; }

    /// <summary>
    /// <b>Gönderdiğimiz token burada gelir</b>. <para/>
    /// Client <c>Basic base64("token:" + token)</c> gonderir, MediaMTX bunu ikiye ayirir ve parola kismini buraya yazar.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>Istemcinin IP'si.</summary>
    public string? Ip { get; set; }

    /// <summary> Istenen eylem: <c>read</c>, <c>publish</c>, <c>playback</c>, <c>api</c>, <c>metrics</c>, <c>pprof</c>. <b>Yalnizca <c>read</c> kabul edilir.</b> </summary>
    public string? Action { get; set; }

    /// <summary> Erisilmek istenen yol adi (orn. <c>cam_{guid}_sub</c>). Token'ın bağlı oldugu yolla BIREBIR eslesmeli. </summary>
    public string? Path { get; set; }

    /// <summary>Protokol (<c>webrtc</c>, <c>rtsp</c> ...).</summary>
    public string? Protocol { get; set; }
}



// ### ÖRNEK SUB STREAM TOKEN JSON ###
/* 

    Action	"read"
    Id	"8e1e91b9-8223-48fa-a114-f2585a44ba51"
    Ip	"127.0.0.1"
    Password	"xcBUxRRJ4mvMvyY49wELDHcG10w-KqPhaaVPgkfVEXM"
    Path	"cam_22222222222222222222222222222222_sub"
    Protocol	"webrtc"
    Query	""
    Token	"xcBUxRRJ4mvMvyY49wELDHcG10w-KqPhaaVPgkfVEXM"
*/
