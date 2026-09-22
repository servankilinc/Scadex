namespace Scadex.Signalization.Enums;

public static class SignalEnums
{
    public enum OperatorSessionStatus
    {
        /// <summary> Islem suruyor: is bitmedigi surece dis kapi kapali olsa da oturum aciktir. </summary>
        Open = 1,
        /// <summary> Is bitti (tum ic kapilar kilitli) ve dis kapi kapandi; hicbir uyari bayragi yok. </summary>
        Completed = 2,
        /// <summary> Is bitti ve dis kapi kapandi ama en az bir uyari bayragi var (<see cref="SessionFlags"/>). </summary>
        CompletedWithWarning = 3,
        /// <summary> Dış kapı kapanmadan <c>SessionMaxDurationMin</c> doldu; oturumu zamanlayici kapatti. </summary>
        TimedOut = 4
    }

    /// <summary> 
    /// Oturumun uyari bayraklari; birden fazlasi ayni anda olabilir. 
    /// <para/>
    /// örn. "0110001" değerinin karşılığı => [NoCardPresented, TimedOut, CommandFailed]
    /// </summary>
    [Flags]
    public enum SessionFlags
    {
        None = 0,
        /// <summary> Oturum boyunca hic yetkili kart okutmadı (operatör belli değil). </summary>
        NoCardPresented = 1,
        /// <summary> Dış kapı açildıktan sonra <c>AwaitingCardTimeoutSec</c> icinde yetkili kart okmadı (süre geçtikten sonra okuttu). </summary>
        UnauthorizedEntry = 2,
        /// <summary> Dış kapı kapandıgında kilitsiz bir iç kapı açık duruyordu; kilitlenemedi. </summary>
        InnerDoorLeftOpen = 4,
        /// <summary> Kilitli bir iç kapının(Son gönderilen komutu kilidi aç olmamasına rağmen) switch'i "açık" gosterdi bu beklenmeyen bir durum. </summary>
        ForcedOpen = 8,
        /// <summary> Bir kilit ya da siren komutu basarisiz oldu. </summary>
        CommandFailed = 16,
        /// <summary> Oturum Dış kapı acilisi gelmeden (kart ya da ic kapı olayiyla) ortuk olarak acildi. </summary>
        OuterOpenMissing = 32,
        /// <summary> Oturum Dış kapı kapanmadan zamanlayiciyla kapatildi. </summary>
        TimedOut = 64
    }

    public enum SessionEventType
    {
        /// <summary> Dış kapı açıldı — oturum bu olayla başlar ya da açık oturuma yeni bir açılış olarak kaydedilir. </summary>
        OuterOpened = 1,

        /// <summary>
        /// Dış kapı kapandı — otomatik kilitlemeyi tetikler. Oturum yalnızca iş bittiyse (dış kapının ardındaki
        /// tüm iç kapılar kilitliyse) sonlanır; bitmediyse <c>Detail</c> alanı <c>Unfinished</c> olur ve oturum açık kalır.
        /// </summary>
        OuterClosed = 2,

        /// <summary> Kart okutuldu ve kullanıcı+kurum+iç kapı eşlemesi başarıyla çözüldü. </summary>
        CardPresented = 3,

        /// <summary> Kart reddedildi (tanımsız kart, yetkisiz kullanıcı, birden fazla kurum, kabinde uygun iç kapı yok vb.) </summary>
        AccessDenied = 4,

        /// <summary> İç kapı kilidi kart okutmayla başarıyla açıldı. </summary>
        Unlocked = 5,

        /// <summary> İç kapı kilidi kart okutmayla (kapı kilitsiz, switch kapalı) başarıyla kilitlendi. </summary>
        Locked = 6,

        /// <summary> Dış kapı kapanırken hâlâ kilitsiz kalmış bir iç kapı, kart okutulmadan otomatik kilitlendi. </summary>
        AutoLocked = 7,

        /// <summary> Kilitleme talebi geldi ama switch açık ya da durumu bilinmiyor olduğu için komut gönderilmedi. </summary>
        LockSkippedDoorOpen = 8,

        /// <summary> İç kapının switch'i "açık" oldu — kapı o sırada kilitsizdi (yetkili/beklenen açılış). </summary>
        InnerOpened = 9,

        /// <summary> İç kapının switch'i "kapalı" oldu. </summary>
        InnerClosed = 10,

        /// <summary> İç kapının switch'i "açık" oldu ama kapı kayıtlı olarak kilitliydi — zorlanmış açılış (güvenlik uyarısı). </summary>
        ForcedOpen = 11,

        /// <summary> Bir kilit ya da siren komutu SCADA'ya gönderildi ama başarısız/zaman aşımına uğradı. </summary>
        CommandFailed = 12,

        /// <summary> Kamera karesi başarıyla çekildi (dış kapı açılışı ya da kart okutma tetikleyicisiyle). </summary>
        SnapshotTaken = 13,

        /// <summary> Kamera karesi çekilemedi (kamera tanımsız/pasif ya da çekim başarısız). </summary>
        SnapshotFailed = 14,

        /// <summary> Dış kapı açıldıktan sonra <c>AwaitingCardTimeoutSec</c> içinde hiç yetkili kart okutulmadı. </summary>
        AwaitingCardTimedOut = 15,

        /// <summary> Siren talebi açıldı (tüm iç kapılar kilitlendi ama dış kapı hâlâ açık). </summary>
        SirenRequested = 17,

        /// <summary> Siren talebi kapandı (dış kapı kapandı, kart tekrar okutuldu ya da zaman aşımı) — gerekçe <c>Detail</c> alanında. </summary>
        SirenReleased = 18,

        /// <summary> Dış kapı aydınlatması yakıldı (karanlık saatlerde kapı açıldı). </summary>
        LightOn = 19,

        /// <summary> Dış kapı aydınlatması söndürüldü (kapı kapandı). </summary>
        LightOff = 20
    }

    /// <summary>
    /// Guvenlik uyarisi sayilan bayraklar: canli panelde vurgulanir, raporda filtrelenir. Onay beklemez; bayrak oturum
    /// kaydinda kalici olarak durur. Diger bayraklar raporda gorunur ama uyari sayilmaz.
    /// </summary>
    public const SessionFlags AlertFlags = SessionFlags.UnauthorizedEntry | SessionFlags.ForcedOpen;

    /// <summary> açık oturumun asamasi. SAKLANMAZ, kapı ve operator satirlarindan turetilir. </summary>
    public enum SessionPhase
    {
        /// <summary> Henuz yetkili kart okutulmadi. </summary>
        AwaitingCard = 1,
        /// <summary> Dış kapınin ardinda kilitsiz bir ic kapı var. </summary>
        Inside = 2,
        /// <summary> Operator var, tum ic kapılar kilitli, Dış kapı hala açık (siren caliyor olabilir). </summary>
        Exiting = 3
    }
}

/// <summary>
/// Sinyalizasyon kabinlerinde manuel komutu hedef cihzaı (<c>SignalCabinetCommandRequest.Target</c>)
/// SignalR ile canlı takipte hangi cihazın değiştiğini bildirdiği alan olarak kullanılır (<c>SignalCabinetStateChangedMessage.Target</c>)
/// </summary>
public enum SignalCabinetOutput
{
    /// <summary> Kabinin sireni (komutun yazılacağı cihaz <c>SignalCabinet.SirenIoChannelId</c>). </summary>
    Siren = 1,
    /// <summary> Dış kapı aydinlatma LED'i (komutun yazılacağı cihaz <c>SignalOuterDoor.LightIoChannelId</c>). </summary>
    OuterDoorLight = 2,
    /// <summary> İç kapı kilit rölesi (komutun yazılacağı cihaz <c>SignalInnerDoor.LockIoChannelId</c>). </summary>
    InnerDoorLock = 3
}

/// <summary> Anahtarı değişen kapının türü (<c>SignalDoorSwitchChangedMessage.DoorKind</c>). </summary>
public enum SignalDoorKind
{
    /// <summary> Dış kapı (<c>SignalOuterDoor</c>). </summary>
    Outer = 1,
    /// <summary> İç kapı (<c>SignalInnerDoor</c>). </summary>
    Inner = 2
}

public enum SignalTimerKind
{
    /// <summary> Oturumun siren talebinin suresi doldu. </summary>
    SirenDue = 1,
    /// <summary> Dis kapi acildiktan sonra yetkili kart okutma suresi doldu. </summary>
    AwaitingCardDue = 2,
    /// <summary> Oturum azami suresini asti. </summary>
    MaxDurationDue = 3
}

/// <summary> <c>OperatorSessionEvent.Detail</c> alaninin sabit degerleri — rapor ekraninin cevirdigi anahtarlar. </summary>
public static class SessionEventDetail
{
    // AccessDenied gerekceleri
    public const string UnknownCard = "UnknownCard";
    public const string NoAuthority = "NoAuthority";
    public const string MultipleAuthorities = "MultipleAuthorities";
    public const string NoDoorForAuthority = "NoDoorForAuthority";

    // SirenReleased gerekceleri
    public const string UnlockedAgain = "UnlockedAgain";
    public const string OuterClosed = "OuterClosed";
    public const string Timeout = "Timeout";
    public const string SessionTimedOut = "SessionTimedOut";

    // OuterClosed gerekcesi: kapi kapandi ama is bitmemisti (oturum acik birakildi)
    public const string Unfinished = "Unfinished";

    // Unlocked gerekcesi: kayit "kilit acik" diyordu ama kapi son komuttan beri hic acilmadi
    // (switch kapali + InnerOpened yok) — komut fiilen uygulanmamis, kilit yeniden acildi.
    public const string UnlockNotEffective = "UnlockNotEffective";

    // OuterOpenMissing gerekcesi: oturum ortuk acilirken dis kapi switch'i "kapali" gosteriyordu
    // (kacirilmis bir acilis degil, gercek bir anomali).
    public const string OuterSwitchClosed = "OuterSwitchClosed";

    // LockSkippedDoorOpen gerekceleri
    public const string SwitchUnknown = "SwitchUnknown";
    public const string SessionEnd = "SessionEnd";

    // CommandFailed hedefleri
    public const string Lock = "Lock";
    public const string Siren = "Siren";
    public const string Light = "Light";
}
