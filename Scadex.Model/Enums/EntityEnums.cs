namespace Scadex.Model.Enums;

public static class EntityEnums
{
    /// <summary> Herhangi bir cihazın ağ/donanım durumu. </summary>
    public enum DeviceStatus
    {
        Offline = 0,
        Online = 1,
        Warning = 2,
        Critical = 3,
        Maintenance = 4
    }

    /// <summary>
    /// Diyagram üzerinde yer alan cihazların kategorisi.
    /// DİKKAT: Ağ üzerinden izlenen 3. parti cihazlar (kamera, UPS, switch) bu enum'da DEĞİLDİR 
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
        /// <c>örenğin /api/scada/cardreader/{ExternalCode}</c> ile <c>{ "cardId": "A1B2C3D4" }</c> gelir. 
        /// Kart ID'si IoChannel'a yazılmaz ve ChannelEvent'a gitmez kart ID'si bir ölçüm değildir.
        /// </summary>
        CardReader = 10,
        /// <summary>Şebeke girişi — kabine dışarıdan gelen 220 AC beslemenin başladığı nokta. Pinleri L / N / PE'dir.</summary>
        Mains = 11,
        /// <summary> Sigorta / devre kesici — referans diyagramdaki "ŞEBEKE", "220V ÇIKIŞ" ve "LAMBA" şalterleri. </summary>
        CircuitBreaker = 12
    }



    /// <summary> Pinin veri/enerji akış yönü. </summary>
    public enum PinDirection
    {
        Input = 0,
        Output = 1,
        Bidirectional = 2
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
        /// <summary>Dijital çıkış sinyali.</summary>
        Signal_Out = 11,
        /// <summary>Analog giriş.</summary>
        Analog_In = 12,
        /// <summary>Kuru kontak (Dry Contact).</summary>
        DryContact = 13,
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



    /// <summary> Kablo fiziksel türü. </summary>
    public enum WireType
    {
        Power = 0,
        Signal = 1,
        DataRS485 = 2,
        DataEthernet = 3,
        Relay = 4,
        Sensor = 5
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



    /// <summary>
    /// SCADA'ya gönderilen kontrol isteğinin türü — <c>PayloadJson</c>'ın hangi şemayla okunacağını ve hangi endpoint'e gidileceğini belirler.
    /// Komut yolu güvenliğinin sağlar: yetki kontrolü ("bu kullanıcı çıkış sürebilir mi?") ve doğrulama ("hedef kanalın Direction'ı Output mu?") bu alana bakarak yapılır. 
    /// </summary>
    public enum DeviceCommandType
    {
        /// <summary>Çıkış kanalını kalıcı olarak sürer (röle, LED). Payload: { "Value": 1 }.</summary>
        SetOutput = 1
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
    /// Kameranın video akışında kullandığı sıkıştırma.
    /// </summary>
    public enum VideoCodec
    {
        H264 = 1,
        H265 = 2
    }


    public enum StreamProfile
    {
        /// <summary>Yüksek kalite — tam ekran / tek kamera görünümü.</summary>
        Main = 1,
        /// <summary>Düşük bant genişliği — liste, küçük önizleme.</summary>
        Sub = 2
    }

    /// <summary> Yakalanan kamera kaydının cinsi. </summary>
    public enum CaptureType
    {
        /// <summary>Tek kare.</summary>
        Snapshot = 1,
        /// <summary>Kısa video klip.</summary>
        Clip = 2
    }

    public enum CaptureStatus
    {
        /// <summary>İstek alındı, dosya henüz depoda değil.</summary>
        Pending = 1,
        /// <summary>Dosya depoda, indirilebilir.</summary>
        Available = 2,
        /// <summary>Alınamadı..</summary>
        Failed = 3
    }


    /// <summary> Kullanıcı izin türleri. </summary>
    public enum Permission
    {
        /// <summary> Kabin diyagramını açıp okuyabilir </summary>
        ViewDiagram = 0,

        /// <summary> Diyagramı düzenleyip kaydedebilir </summary>
        EditDiagram = 1,

        /// <summary> Output kanalı sürebilir — röle, kilit, siren Sahaya fiziksel etki eden tek izin budur. </summary>
        ControlOutput = 2,

        /// <summary> Alarmı görüp kabul edebilir </summary>
        AcknowledgeAlarm = 3,

        /// <summary> Kullanıcı ve rol yönetimi </summary>
        ManageUsers = 4,

        /// <summary>
        /// Sistem ve kabin yapılandırması: SCADA adresi, zaman aşımı, şablon kütüphanesi.
        /// <c>Cabinet.ScadaBaseUrl</c>'i değiştirmek tüm telemetriyi ve kumandayı başka bir adrese yönlendirir.
        /// </summary>
        ConfigureSystem = 5,

        /// <summary> Kamera görüntüsü izleyebilir </summary>
        ViewCamera = 6,

        /// <summary> Önemli raporları görebilir ve dışa aktarabilir. </summary>
        ExportData = 7,

        /// <summary> Otomasyon iş akışlarını tanımlar ve düzenler </summary>
        ManageWorkflow = 8,

        /// <summary> Geçiş kartı tanımlama, yetkilendirme ve iptal etme </summary>
        ManageAccessCards = 9
    }
}
