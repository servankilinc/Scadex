namespace Scadex.Model.Enums;

public static class EntityEnums
{
    /// <summary> Herhangi bir cihazın ağ/donanım durumu. </summary>
    public enum DeviceStatus
    {
        Offline = 0,
        Online = 1,
        Warning = 2,
        // 3 (Critical) kaldirildi
        Maintenance = 4
    }
    public static int DeviceStatusSeverityRank(int status) => status switch
    {
        (int)DeviceStatus.Offline => 3,
        (int)DeviceStatus.Warning => 2,
        // Bakım durumu BILEREK yapilir; alarm degildir ama her şey normal de deüildir.
        (int)DeviceStatus.Maintenance => 1,
        _ => 0
    };

    /// <summary>
    /// Diyagram üzerinde yer alan cihazların kategorisi.
    /// </summary>
    public enum DeviceType
    {
        ControlModule = 1,
        /// <summary>Dijital/analog giriş kartı (IN1..IN16).</summary>
        InputModule = 2,
        /// <summary>Röle çıkış kartı (OUT1..OUT15, NC/COM/NO).</summary>
        OutputModule = 3,
        /// <summary>LED gösterge kartı (LD1..LD8).</summary>
        LedModule = 4,
        /// <summary>Klemens / dağıtım bloğu — pasif, sadece kablo toplar.</summary>
        TerminalBlock = 5,
        /// <summary>Harici sensör (kabloyla bir giriş pinine bağlanan).</summary>
        Sensor = 6,
        /// <summary>Çevre birimi — siren, kilit, lamba, yazıcı, POS, barkod okuyucu vb.</summary>
        Peripheral = 7,
        /// <summary>Güç kaynağı / adaptör kartı.</summary>
        PowerSupply = 8,
        /// <summary>Panoya monteli ölçüm cihazı, voltmetre, ampermetre, akım trafosu.</summary>
        MeasurementDevice = 9,
        /// <summary>
        /// Kart okuyucu — kart ID'sini gönderen modül.
        /// Kendi ingest endpoint'i vardır çünkü taşıdığı veri bir kanal değeri değildir:
        /// <c>POST /api/Scada/card</c> ile <c>{ cardId, timestampUtc }</c> gelir; otomasyon, ingest'teki gibi
        /// isteği gönderen kontrol modülünün Basic Auth kimliğinden çözülür.
        /// Kart ID'si IoChannel'a yazılmaz ve ChannelEvent'a gitmez kart ID'si bir ölçüm değildir.
        /// </summary>
        CardReader = 10,
        /// <summary>Şebeke girişi — otomasyona dışarıdan gelen 220 AC beslemenin başladığı nokta. Pinleri L / N / PE'dir.</summary>
        Mains = 11,
        /// <summary> Sigorta / devre kesici — referans diyagramdaki "ŞEBEKE", "220V ÇIKIŞ" ve "LAMBA" şalterleri. </summary>
        CircuitBreaker = 12,
        /// <summary>Bilgisayar — RemoteDesk modülünün ekranını izlediği PC. Ek alanları <c>PcDevice</c>'tadır.</summary>
        Pc = 13,
        /// <summary>IP kamera — MediaMTX üzerinden izlenir, anlık görüntü/klip alınır. Ek alanları <c>Camera</c>'dadır.</summary>
        Camera = 14
    }



    /// <summary> Pinin veri/enerji akış yönü. </summary>
    public enum PinDirection
    {
        Input = 0,
        Output = 1,
        Bidirectional = 2,
        AnalogInput = 3
    }

    /// <summary> React Flow bir handle'ı yerleştirmek için kenarı AÇIKÇA ister; <c>RelativeX/Y</c> tek başına yetmez </summary>
    public enum HandleSide
    {
        Left = 0,
        Right = 1,
        Top = 2,
        Bottom = 3
    }

    /// <summary> Pinin spesifik elektriksel fonksiyonu. String yerine enum kullanılarak tip güvenliği sağlanır. </summary>
    public enum PinFunction
    {
        /// <summary>Ortak uç (röle).</summary>
        COM = 0,
        /// <summary>Normally Open (röle).</summary>
        NO = 1,
        /// <summary>Normally Closed (röle).</summary>
        NC = 2,
        /// <summary>Pozitif besleme (+VCC).</summary>
        VCC = 3,
        /// <summary>Negatif / Toprak (GND).</summary>
        GND = 4,
        /// <summary>RS485 Data+ hattı.</summary>
        RS485_POS = 5,
        /// <summary>RS485 Data- hattı.</summary>
        RS485_NEG = 6,
        /// <summary>RJ45 Ethernet portu.</summary>
        RJ45 = 7,
        /// <summary>LED anot (+).</summary>
        LED_Anode = 8,
        /// <summary>LED katot (-).</summary>
        LED_Cathode = 9,
        /// <summary>Dijital giriş sinyali.</summary>
        Signal_In = 10,
        /// <summary>Analog giriş.</summary>
        Analog_In = 12,

        /// <summary>Faz (220 AC).</summary>
        Line_L = 14,
        /// <summary>Nötr (220 AC).</summary>
        Neutral_N = 15,
        /// <summary>Toprak / koruma hattı (PE).</summary>
        Earth_PE = 16,
        /// <summary>Genel amaçlı (özel tanımlı).</summary>
        General = 99
    }

    /// <summary> Gerilim seviyesi — kablo bağlantı doğrulaması ve diyagram renklendirmesi için. Farklı seviyelerdeki pinlerin birbirine bağlanması engellenir.</summary>
    public enum VoltageLevel
    {
        None = 0,
        DC_12V = 1,
        DC_24V = 2,
        AC_220V = 3,
        Signal_5V = 4,
        Data = 5
    }



    /// <summary> Kablo çizim stili (UI). </summary>
    public enum LineStyle
    {
        Solid = 0,
        Dashed = 1,
        Dotted = 2
    }

    /// <summary> Kablonun canvas üzerinde çizim şekli. </summary>
    public enum EdgeRouting
    {
        /// <summary>Dik açılı kırılmalar.</summary>
        Orthogonal = 0,
        /// <summary>Uçtan uca düz çizgi.</summary>
        Straight = 1,
        /// <summary>Yumuşak eğri (bezier).</summary>
        Curved = 2
    }



    /// <summary> Diyagram üzerinde ki serbest metin etiketleri. </summary>
    public enum AnnotationShape
    {
        /// <summary>Çerçevesiz düz metin.</summary>
        Text = 0,
        /// <summary>Çerçeveli kutu.</summary>
        Rectangle = 1,
        /// <summary>Not / açıklama balonu.</summary>
        Note = 2,
        /// <summary>Yön oku.</summary>
        Arrow = 3
    }

    /// <summary> Canvas arka plan deseni. </summary>
    public enum BackgroundVariant
    {
        /// <summary>Desen yok — düz zemin rengi.</summary>
        None = 0,
        /// <summary>Küçük noktalardan oluşan grid.</summary>
        Dots = 1,
        /// <summary>Çizgilerden oluşan grid.</summary>
        Lines = 2,
        /// <summary>Artı işaretlerinden oluşan grid.</summary>
        Cross = 3
    }



    /// <summary> SCADA'ya gönderilen komutun sonucu. Kuyruk olmadığı için "Pending" ve "Cancelled" durumları YOKTUR. </summary>
    public enum CommandStatus
    {
        /// <summary>İstek gönderildi, cevap henüz işlenmedi (geçici ara durum).</summary>
        Sent = 1,
        /// <summary>SCADA komutu kabul ettiğini bildirdi (2xx).</summary>
        Succeeded = 2,
        /// <summary>SCADA hata döndürdü (4xx/5xx) — gövdesi ResultMessage'dadır.</summary>
        Failed = 3,
        /// <summary>Zaman aşımı veya bağlantı hatası — SCADA'ya hiç ulaşılamadı.</summary>
        NoResponse = 4
    }


    /// <summary>
    /// Kanal değerindeki değişimi kim fark etti — <c>ChannelChangedEvent.Source</c> ve <c>ChannelEvent.Source</c>.
    /// <see cref="Sync"/>'te değişimin ne zaman olduğu bilinmez; <c>OccurredAtUtc</c> yalnızca fark edildiği andır.
    /// </summary>
    public enum ChannelChangeSource
    {
        /// <summary> SCADA değişimi kendisi bildirdi (<c>POST /api/Scada/ingest</c>). </summary>
        Ingest = 1,
        /// <summary>
        /// Scadex'in başarılı çıkış komutu. Kanalın değerini değiştirdiyse <c>ChannelEvent</c>'e de yazılır (2026-10-31); komutun kendisinin
        /// kaydı <c>DeviceCommand</c>'dadır.
        /// </summary>
        Command = 2,
        /// <summary> Karttan okuma (<c>/status</c>) kayıtlı değerden farklı çıktı. Çıkışta bu, rölenin son komuttan sapmasıdır. </summary>
        Sync = 3
    }


    public enum StreamProfile
    {
        /// <summary>Yüksek kalite — tam ekran / tek kamera görünümü.</summary>
        Main = 1,
        /// <summary>Düşük bant genişliği — liste, küçük önizleme.</summary>
        Sub = 2
    }

    public enum CameraBrand
    {
        Hikvision = 1
    }

    // CaptureType / CaptureStatus (kare/klip çekimi) CameraManagement modülündedir: Scadex.CameraManagement.Model.Enums.


    /// <summary> Kullanıcı izin türleri. </summary>
    public enum Permission
    {
        /// <summary> Otomasyon diyagramını açıp izleyebilir (canlı değerler, komut geçmişi). </summary>
        ViewDiagram = 0,

        /// <summary>
        /// Diyagramı düzenleyip kaydedebilir (cihaz, kablo, not, tuval ayarı). İzlemeden ve komuttan ayrıdır. Kontrol kartının MAC'i ve
        /// SCADA adresi/zaman aşımı da kartın cihaz formundan bu izinle yazılır (2026-11-04, kullanıcı kararı): adresi değiştirmek
        /// telemetriyi ve kumandayı başka bir adrese yönlendirir.
        /// </summary>
        EditDiagram = 1,

        /// <summary>
        /// Çekirdek diyagram üzerinden output kanalına komut gönderebilir — röle, kilit, siren. Uç ayrıca <see cref="ViewDiagram"/>
        /// ister (görmeden komut yok). Sinyalizasyonun sanal kabini bu izni KULLANMAZ: <see cref="SignalVirtualCabinet"/> (2026-10-14).
        /// </summary>
        ControlOutput = 2,

        // 3 (AcknowledgeAlarm) 2026-10-31'de kaldırıldı — ucu yoktu. Numara yeniden kullanılmaz.

        /// <summary> Kullanıcı ve rol yönetimi </summary>
        ManageUsers = 4,

        /// <summary>
        /// Sistem ve otomasyon yapılandırması: otomasyon, firma, kamera, şablon kütüphanesi, ayarlar. SCADA adresi 2026-11-04'ten beri
        /// otomasyonda değil kontrol kartındadır ve <see cref="EditDiagram"/> ile yazılır.
        /// </summary>
        ConfigureSystem = 5,

        /// <summary> Kamera görüntüsü izleyebilir </summary>
        ViewCamera = 6,

        // 7 (ExportData) ve 8 (ManageWorkflow) 2026-10-31'de kaldırıldı — ucu yoktu. Numaralar yeniden kullanılmaz.

        /// <summary> Geçiş kartı tanımlama, yetkilendirme ve iptal etme </summary>
        ManageAccessCards = 9,
        
        /// <summary> PC ekranlarını canlı izleyebilir (RemoteDesk modülü). </summary>
        RemotePcView = 10,

        /// <summary> PC'yi uzaktan kontrol edebilir (fare, klavye) — RemoteDesk modülü için </summary>
        RemotePcControl = 11,

        /// <summary> PC izleme/kontrol denetim raporunu görebilir (kim, hangi PC'yi, ne zaman) — RemoteDesk modülü için </summary>
        RemotePcAudit = 12,

        /// <summary> Uzak PC ile dosya transferi yapabilir (gez/indir/yükle) — RemoteDesk modülü; kontrolü elinde tutana açıktır, ayrı ve en hassas izin </summary>
        RemotePcFileTransfer = 13,

        /// <summary>
        /// Devam eden operatör işlemini elle kapatabilir (Sinyalizasyon modülü). Kapatma sahaya dokunur: siren talebi bırakılır,
        /// iç kapılara otomatik kilitleme denenir — bu yüzden ayrı izindir.
        /// </summary>
        SignalSessionClose = 14,

        /// <summary>
        /// Sinyalizasyonun sanal kabinini açar ve oradan komut gönderir (siren, aydınlatma, iç kapı kilidi) + kart okuma listesi.
        /// Çekirdeğin <see cref="ControlOutput"/>'undan ayrıdır: sanal kabini yöneten operatör diyagrama komut gönderemez, tersi de.
        /// Kapı yapılandırması (<see cref="ConfigureSystem"/>) ve operatör kurumu (<see cref="ManageAccessCards"/>) bu izne dahil değildir.
        /// </summary>
        SignalVirtualCabinet = 15
    }
}
