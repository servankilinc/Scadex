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
        ServerRestart = 5,
        /// <summary> PC'de durduruldu (saha ekranındaki "Durdur" ya da istemci kapatıldı). </summary>
        StoppedOnPc = 6
    }

    /// <summary> Kontrol isteğinin sonucu (viewer hub <c>RequestControl</c>). </summary>
    public enum ControlRequestStatus
    {
        Granted = 1,
        /// <summary> PC'yi başka bir kullanıcı kontrol ediyor — kontrol PC başına tek kullanıcıdadır. </summary>
        Busy = 2,
        /// <summary> Kullanıcı bu PC'yi izlemiyor (canlı kiralaması yok) — görmeden kontrol verilmez. </summary>
        NotViewing = 3,
        /// <summary> PC'deki istemci merkeze bağlı değil. </summary>
        PcNotConnected = 4,
        NotFound = 5
    }

    /// <summary> Uzaktan kontrolün neden bittiği (Faz 8). </summary>
    public enum RemoteControlEndReason
    {
        /// <summary> Kullanıcı "Kontrolü bırak" dedi ya da ekrandan çıktı. </summary>
        Released = 1,
        /// <summary> Tarayıcının viewer hub bağlantısı koptu (sekme kapandı, ağ). </summary>
        ViewerDisconnected = 2,
        /// <summary> PC'deki istemcinin bağlantısı koptu. </summary>
        PcDisconnected = 3,
        /// <summary> Uzun süre girdi gelmedi. </summary>
        Idle = 4,
        /// <summary> Sunucu kapanırken ya da yeniden başlarken açık kalan oturum. </summary>
        ServerRestart = 5,
        /// <summary> Kullanıcının bu PC'deki izlemesi (kiralaması) bitti — görmeden kontrol edilmez. </summary>
        ViewEnded = 6,
        /// <summary> Aynı kullanıcı başka bir sekmeden kontrolü aldı. </summary>
        Replaced = 7
    }
}
