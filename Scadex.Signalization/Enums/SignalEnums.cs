namespace Scadex.Signalization.Enums;

/// <summary>
/// Sinyalizasyon modulunun enum'lari. Sayi olarak serilesirler (JsonStringEnumConverter bilerek kayitli degil):
/// bir uyenin degerini DEGISTIRMEYIN, yeni uye SONA eklenir.
/// </summary>
public static class SignalEnums
{
    public enum OperatorSessionStatus
    {
        /// <summary> Dis kapi acik, islem suruyor. </summary>
        Open = 1,
        /// <summary> Dis kapi kapandi, hicbir uyari bayragi yok. </summary>
        Completed = 2,
        /// <summary> Dis kapi kapandi ama en az bir uyari bayragi var (<see cref="SessionFlags"/>). </summary>
        CompletedWithWarning = 3,
        /// <summary> Dis kapi kapanmadan <c>SessionMaxDurationMin</c> doldu; oturumu zamanlayici kapatti. </summary>
        TimedOut = 4
    }

    /// <summary> Oturumun uyari bayraklari; birden fazlasi ayni anda olabilir. </summary>
    [Flags]
    public enum SessionFlags
    {
        None = 0,
        /// <summary> Oturum boyunca hic yetkili kart okutulmadi. </summary>
        NoCardPresented = 1,
        /// <summary> Dis kapi acildiktan sonra <c>AwaitingCardTimeoutSec</c> icinde yetkili kart okutulmadi. </summary>
        UnauthorizedEntry = 2,
        /// <summary> Dis kapi kapandiginda kilitsiz bir ic kapi ACIK duruyordu; kilitlenemedi. </summary>
        InnerDoorLeftOpen = 4,
        /// <summary> Kilitli bir ic kapinin anahtari "acik" gosterdi. </summary>
        ForcedOpen = 8,
        /// <summary> Bir kilit ya da siren komutu basarisiz oldu. </summary>
        CommandFailed = 16,
        /// <summary> Oturum dis kapi acilisi gelmeden (kart ya da ic kapi olayiyla) ortuk olarak acildi. </summary>
        OuterOpenMissing = 32,
        /// <summary> Oturum dis kapi kapanmadan zamanlayiciyla kapatildi. </summary>
        TimedOut = 64
    }

    public enum SessionEventType
    {
        OuterOpened = 1,
        OuterClosed = 2,
        CardPresented = 3,
        AccessDenied = 4,
        Unlocked = 5,
        Locked = 6,
        AutoLocked = 7,
        LockSkippedDoorOpen = 8,
        InnerOpened = 9,
        InnerClosed = 10,
        ForcedOpen = 11,
        CommandFailed = 12,
        SnapshotTaken = 13,
        SnapshotFailed = 14,
        AwaitingCardTimedOut = 15,
        // 16 BOS: eski "AlertAcknowledged" (onay akisi kaldirildi, 2026-09-11). Numara baska bir tipe VERILMEZ —
        // gecmis satirlarda 16 kalmis olabilir ve sayi olarak serilesir.
        SirenRequested = 17,
        SirenReleased = 18
    }

    /// <summary>
    /// Guvenlik uyarisi sayilan bayraklar: canli panelde vurgulanir, raporda filtrelenir. Onay beklemez; bayrak oturum
    /// kaydinda kalici olarak durur. Diger bayraklar raporda gorunur ama uyari sayilmaz.
    /// </summary>
    public const SessionFlags AlertFlags = SessionFlags.UnauthorizedEntry | SessionFlags.ForcedOpen;

    /// <summary> Acik oturumun asamasi. SAKLANMAZ, kapi ve operator satirlarindan turetilir. </summary>
    public enum SessionPhase
    {
        /// <summary> Henuz yetkili kart okutulmadi. </summary>
        AwaitingCard = 1,
        /// <summary> Dis kapinin ardinda kilitsiz bir ic kapi var. </summary>
        Inside = 2,
        /// <summary> Operator var, tum ic kapilar kilitli, dis kapi hala acik (siren caliyor olabilir). </summary>
        Exiting = 3
    }
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

    // LockSkippedDoorOpen gerekceleri
    public const string SwitchUnknown = "SwitchUnknown";
    public const string SessionEnd = "SessionEnd";

    // CommandFailed hedefleri
    public const string Lock = "Lock";
    public const string Siren = "Siren";
}
