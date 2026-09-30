namespace Scadex.RemoteDesk.Enums;

public static class RemoteDeskEnums
{
    /// <summary> Bir yayın oturumunun (bir FFmpeg ömrü) durumu — RemoteDesk.md § 8.4. </summary>
    public enum ScreenSessionStatus
    {
        /// <summary> Satır yazıldı, istemciye henüz komut gitmedi. </summary>
        Created = 1,
        /// <summary> <c>StartScreenStream</c> gönderildi; yol MediaMTX'te hazır olana kadar beklenir. </summary>
        CommandSent = 2,
        /// <summary> Yayın MediaMTX'te hazır. </summary>
        Streaming = 3,
        /// <summary> <c>StopScreenStream</c> gönderildi. </summary>
        Stopping = 4,
        Stopped = 5,
        /// <summary> İstemci hata bildirdi, zaman aşımı ya da bağlantı koptu; neden <c>FailureReason</c>'da. </summary>
        Failed = 6
    }

    /// <summary> Yayının neden durduğu. </summary>
    public enum ScreenStopReason
    {
        /// <summary> Son izleyicinin kiralaması bırakıldı ya da düştü. </summary>
        NoViewers = 1,
        /// <summary> Aynı monitör için yeni bir oturum açıldı. </summary>
        Replaced = 2,
        /// <summary> Windows istemcisinin bağlantısı koptu. </summary>
        ClientDisconnected = 3,
        /// <summary> İstemci yayını başlatamadı / sürdüremedi. </summary>
        ClientFailed = 4,
        /// <summary> Sunucu kapanırken ya da yeniden başlarken açık kalan oturum. </summary>
        ServerRestart = 5
    }
}
