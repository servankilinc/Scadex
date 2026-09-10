# Kontrol Kartı TCP/IP Haberleşme Protokolü ve C# Entegrasyon Rehberi

Bu döküman, sahadaki kontrol kartının (ve `WinFormScadaSimulator` simülatörünün) ham bayt protokolünü anlatır ve **.NET Framework 4.7.2+** / **.NET 6-10** projelerinde kullanılabilecek örnek istemci kodlarını içerir.

> **Bu protokol CabinetOS'un kapsamı DIŞINDADIR ve SCADA tarafında kalır.** CabinetOS kartla hiç konuşmaz; SCADA ile HTTP üzerinden konuşur. Ancak **gelen çerçevenin biçimi** (§ 1.3) CabinetOS'un ingest sözleşmesinin türetildiği kaynaktır — alan eşlemesi § 1.4'te, sözleşmenin kendisi `docs/api-contract/07-scada-ingest.md`'dedir.

---

## 1. Protokol Spesifikasyonu

İletişim **TCP/IP soketi** üzerinden ham bayt (raw bytes) alışverişiyle sağlanır. Metin, JSON, uzunluk öneki ya da el sıkışma yoktur.

### 1.1 İki yön, iki farklı çerçeve

Protokol **asimetriktir**. Giden ve gelen çerçeveler ne uzunluk ne de alan olarak birbirine benzer:

| Yön | Uzunluk | Biçim | Footer |
|---|---|---|---|
| **Giden** (biz → kart) — kumanda | 4 veya 5 bayt | `[başlık][kod][değer]` (+ `[zaman]`) `[0x7A]` | **var** |
| **Gelen** (kart → biz) — telemetri | **3 bayt** | `[başlık][kod][değer]` | **yok** |

Bu ayrım kritiktir: gelen tarafı 5 baytlık çerçeve bekleyerek ayrıştıran bir okuyucu **hizadan çıkar** ve baştan itibaren yanlış veri üretir.

---

### 1.2 Giden çerçeve — kumanda (biz → kart)

**Sabit alanlar:** başlangıç baytı `inOut` karakterinden üretilir, paket `0x7A` (ASCII `'z'`) ile biter.

| Bayt | Adı | Açıklama |
|---|---|---|
| 0 | **Header** | ASCII karakter — `'O'` (`0x4F`) çıkış, `'I'` giriş, `'A'` analog |
| 1 | **Device ID** | Hedef nokta numarası |
| 2 | **Value** | Komut değeri |
| *(3)* | **Time** | **Opsiyonel** — gecikme; verilmezse bu bayt **hiç gönderilmez** |
| son | **Footer** | `0x7A` |

> ⚠ **Footer'ın indeksi sabit DEĞİLDİR.** Zaman baytı atlandığında paket 4 bayttır ve footer 3. indekse düşer. Ayrıştırıcı footer'ı **konuma göre değil, bayt değerine göre** aramalıdır.

#### İki gönderici, iki farklı uzunluk

Sahada bu protokolü konuşan iki ayrı kod yolu var ve **aynı mantıksal komut için farklı uzunlukta paket üretiyorlar**:

| Çağrı | Telde giden | Uzunluk |
|---|---|---|
| `ControlCartPostData("O", 1, 0)` (§ 5, referans proje) | `4F 01 00 7A` | **4 bayt** — Time yok |
| `ControlCartPostData("O", 1, 0, 5)` | `4F 01 00 05 7A` | 5 bayt |
| `SendCommandAsync(1, 0x00)` (§ 2, bu rehberin istemcisi) | `4F 01 00 00 7A` | **5 bayt** — Time hep `0x00` |

İkisi de çalışan projelerden geliyor, dolayısıyla kartın her ikisini de kabul ettiği anlaşılıyor (footer'a kadar okuyor olmalı). **Yeni bir entegrasyonda hangisinin kullanılacağı saha ekibiyle teyit edilmelidir** — bu rehber ikisini de belgeler, birini normatif ilan etmez.

#### Adres uzayı

Çıkışlar ve LED'ler **tek bir düz uzayı** paylaşır; ayrı bir başlık ya da ayrı bir numaralandırma yoktur:

```
1  – 16   Röle çıkışları
17 – 24   LED'ler          (LED n  ->  Device ID 16 + n)
```

#### Değer kutbu — röle ile LED TERS

Bu, protokolün en kolay gözden kaçan ayrıntısıdır:

| Hedef | Aç | Kapat |
|---|---|---|
| **Röle / çıkış** (1-16) | `0x00` | `0x01` |
| **LED** (17-24) | `0x01` | `0x00` |

§ 2'deki `TurnOnOutputAsync` / `TurnOnLedAsync` yardımcıları bu farkı kapatır; ham `SendCommandAsync` çağıran kod bunu kendisi bilmek zorundadır.

---

### 1.3 Gelen çerçeve — telemetri (kart → biz)

Kart **olay güdümlüdür**: bir giriş değiştiğinde 3 baytlık bir çerçeve gönderir. Sorgu/yanıt yoktur, biz değer çekmeyiz.

| Bayt | Adı | Açıklama |
|---|---|---|
| 0 | **Header** | ASCII — `'I'` dijital giriş, `'A'` analog giriş |
| 1 | **Kod** | Noktanın kart üzerindeki numarası |
| 2 | **Değer** | `0..255` |

**Footer ve zaman baytı YOKTUR.** Çerçeve tam 3 bayttır.

#### `'O'` gelen tarafta işlenmez

Referans çözümleyici (§ 5) yalnızca `'I'` ve `'A'` başlıklarını ele alır. Çıkışlar durum rapor etmez — bir röleyi biz sürdüğümüzde dönen değer saha olayı değil, kendi komutumuzun yankısı olurdu.

#### `'I'` ve `'A'` BAĞIMSIZ kod uzaylarıdır

Aynı kartta hem `A/1` hem `I/1` bulunabilir ve bunlar **farklı fiziksel noktalardır**. Referans projede adres `(InOut, Code)` ikilisidir:

```
I / 1   Darbe sensörü
I / 7   Dış kapı switch
A / 1   Sıcaklık
A / 2   Nem
O / 5   Kağıt para kasası      (yalnızca kumanda)
O / 6   Bilgisayar             (yalnızca kumanda)
```

Kodu tek başına adres sayan bir eşleme `A/1` ile `I/1`'i birbirine karıştırır.

#### Değer tek bayttır — ondalık ve negatif taşımaz

Analog bir okuma da tek bayta sığmak zorundadır (`0..255`). `-20…+60 °C` gibi bir aralık gerekiyorsa **ölçekleme/offset kartın dışında** yapılmalıdır; protokolde birim, katsayı ya da işaret alanı yoktur.

#### Ayrıştırma

TCP **mesaj sınırı taşımaz**. Tek bir `Read` çağrısı 3 baytın katı olmayan bir uzunluk döndürebilir; ardışık iki olay tek okumada birleşebilir, ya da bir çerçeve iki okumaya bölünebilir. Doğru okuyucu tamponu 3'erli ilerletir:

```csharp
for (int i = 0; i + 3 <= data.Length; i += 3)
{
    char header = (char)data[i];        // 'I' | 'A'
    int  code   = data[i + 1];
    int  value  = data[i + 2];
    // ...
}
```

> ⚠ **Referans uygulamadaki eksik.** § 5'teki `DataProcessing`, 3'e bölünmeyen artık baytları `break` ile **atar** ve bir sonraki okumaya devretmez. Çerçeve bir okuma sınırında ikiye bölünürse o çerçeve kaybolur. Yeni bir entegrasyon, artık baytları kalıcı bir tamponda saklayıp sonraki okumanın başına eklemelidir.

---

### 1.4 CabinetOS ingest'ine eşleme

SCADA bu çerçeveleri ayrıştırıp **her çerçeve için bir HTTP isteği** atar:

| Gelen çerçeve | `POST /api/Scada/ingest` gövdesi |
|---|---|
| `'I' 7 1` | `{ "macAddress": "AA:BB:CC:DD:EE:FF", "type": "I", "channelNumber": 7, "value": "1" }` |
| `'A' 1 235` | `{ "macAddress": "AA:BB:CC:DD:EE:FF", "type": "A", "channelNumber": 1, "value": "235" }` |
| `'O' 5 0` | Gönderilmez — gövdede ifade edilemez |

`macAddress` çerçevede yoktur; **kontrol kartının kendi MAC adresidir** (bir kabin = bir kontrol kartı = bir soket). CabinetOS bu adresle eşleşen `DeviceType.ControlModule` cihazını bulup kabini oradan çözer — saha CabinetOS'un ürettiği kabin `Guid`'ini bilemez, ama MAC adresi iki tarafta da bilinen ortak değerdir. Eşleşme **birebir string karşılaştırmasıdır**: adres CabinetOS'ta nasıl kayıtlıysa (`Device.MacAddress`) çerçeveye de öyle yazılmalıdır; ayraç (`:` / `-`) ya da harf farkı eşleşmeyi bozar ve istek **404** döner. Değer string olarak taşınır. Tam sözleşme, kısıtlar ve hata kodları: **`docs/api-contract/07-scada-ingest.md`**.

---

## 2. Yeniden Kullanılabilir C# İstemci Kütüphanesi (`ScadaDeviceClient`)

> **Kapsam:** bu sınıf **giden** yönü (§ 1.2, 5 baytlık varyant) eksiksiz uygular. **Gelen** yönde ise ham baytları `DataReceived` olayıyla dışarı verir — **ayrıştırma yapmaz**. Çerçeveleme § 1.3'teki döngüyle çağıran tarafta yapılmalıdır.

### `ScadaDeviceClient.cs`

```csharp
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ScadaCommunicationLibrary
{
    /// <summary>
    /// Kontrol kartıyla TCP/IP üzerinden haberleşmeyi sağlayan modüler istemci sınıfı.
    /// </summary>
    public class ScadaDeviceClient : IDisposable
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private bool _isDisposed;

        public string IpAddress { get; private set; }
        public int Port { get; private set; }
        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;

        // Cihazdan veri geldiğinde tetiklenen olaylar
        public event EventHandler<byte[]> DataReceived;
        public event EventHandler<string> ConnectionStateChanged;
        public event EventHandler<Exception> ErrorOccurred;

        /// <summary>
        /// Cihaza TCP/IP bağlantısı kurar ve arka planda veri dinlemeyi başlatır.
        /// </summary>
        public async Task<bool> ConnectAsync(string ipAddress, int port)
        {
            if (IsConnected)
                await DisconnectAsync();

            IpAddress = ipAddress;
            Port = port;

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ipAddress, port);
                _stream = _tcpClient.GetStream();
                _cts = new CancellationTokenSource();

                ConnectionStateChanged?.Invoke(this, $"Bağlandı: {ipAddress}:{port}");

                // Arka planda gelen verileri dinleyen görevi başlat
                _ = ListenForIncomingDataAsync(_cts.Token);

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        /// <summary>
        /// Bağlantıyı güvenli bir şekilde kapatır.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            finally
            {
                _stream = null;
                _tcpClient = null;
                ConnectionStateChanged?.Invoke(this, "Bağlantı kesildi");
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cihaza 5 baytlık kumanda paketini gönderir (bkz. § 1.2 — Time baytı DAİMA yazılır).
        /// </summary>
        /// <param name="deviceId">Hedef nokta (röle 1-16, LED 17-24)</param>
        /// <param name="commandValue">Komut değeri — kutup röle ve LED'de TERSTİR, bkz. § 1.2</param>
        /// <param name="time">Zaman parametresi (varsayılan 0x00)</param>
        public async Task<bool> SendCommandAsync(byte deviceId, byte commandValue, byte time = 0x00)
        {
            if (!IsConnected || _stream == null)
            {
                ErrorOccurred?.Invoke(this, new InvalidOperationException("Cihaza bağlı değil!"));
                return false;
            }

            // Protokol Paketi: Header (0x4F) + DeviceId + Value + Time + Footer (0x7A)
            byte[] packet = new byte[] { 0x4F, deviceId, commandValue, time, 0x7A };

            await _sendSemaphore.WaitAsync();
            try
            {
                await _stream.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        // --- Kutup farkını kapatan yardımcılar (bkz. § 1.2 "Değer kutbu") ---

        /// <summary>Röle / Output Açma Komutu — rölede AÇ = 0x00.</summary>
        public Task<bool> TurnOnOutputAsync(byte outputId) => SendCommandAsync(outputId, 0x00);

        /// <summary>Röle / Output Kapatma Komutu — rölede KAPAT = 0x01.</summary>
        public Task<bool> TurnOffOutputAsync(byte outputId) => SendCommandAsync(outputId, 0x01);

        /// <summary>LED Açma Komutu — LED'de AÇ = 0x01 (rölenin TERSİ). LED'ler 17'den başlar.</summary>
        public Task<bool> TurnOnLedAsync(byte ledIndex) => SendCommandAsync((byte)(16 + ledIndex), 0x01);

        /// <summary>LED Kapatma Komutu — LED'de KAPAT = 0x00 (rölenin TERSİ).</summary>
        public Task<bool> TurnOffLedAsync(byte ledIndex) => SendCommandAsync((byte)(16 + ledIndex), 0x00);

        /// <summary>
        /// Arka planda cihazdan gelen verileri sürekli okuyan döngü.
        ///
        /// DİKKAT: Ham baytları olduğu gibi yayar. 3 baytlık çerçevelere ayırmak
        /// ve okuma sınırında bölünen çerçeveyi tamponlamak ÇAĞIRANIN işidir (§ 1.3).
        /// </summary>
        private async Task ListenForIncomingDataAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0) break; // Bağlantı karşı taraftan kapatıldı

                    byte[] receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);

                    // Veriyi event ile bildir
                    DataReceived?.Invoke(this, receivedData);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            finally
            {
                if (IsConnected)
                    _ = DisconnectAsync();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _cts?.Cancel();
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _sendSemaphore?.Dispose();
            _isDisposed = true;
        }
    }
}
```

---

## 3. Farklı Projelerde Kullanım Örnekleri

### Örnek A: Konsol (Console) veya Servis Uygulamasında Kullanım

Aşağıdaki örnek, § 1.3'teki çerçeveleme döngüsünü **artık bayt tamponuyla birlikte** gösterir — hex dökmek yerine gerçekten ayrıştırır.

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScadaCommunicationLibrary;

class Program
{
    // Okuma sınırında bölünen çerçevenin artığı burada bekler.
    static readonly List<byte> _carry = new List<byte>();

    static async Task Main(string[] args)
    {
        using (var scadaClient = new ScadaDeviceClient())
        {
            scadaClient.ConnectionStateChanged += (s, msg) => Console.WriteLine($"[DURUM] {msg}");
            scadaClient.ErrorOccurred += (s, ex) => Console.WriteLine($"[HATA] {ex.Message}");
            scadaClient.DataReceived += (s, bytes) => ProcessIncoming(bytes);

            Console.WriteLine("Cihaza bağlanılıyor...");
            bool connected = await scadaClient.ConnectAsync("192.168.1.100", 5000);

            if (connected)
            {
                await scadaClient.TurnOnOutputAsync(1);   // Röle 1 aç
                await Task.Delay(1000);

                await scadaClient.TurnOnLedAsync(1);      // LED 1 aç (Device ID 17)
                await Task.Delay(2000);

                await scadaClient.TurnOffOutputAsync(1);  // Röle 1 kapat

                await scadaClient.DisconnectAsync();
            }
        }
    }

    /// <summary>Gelen baytları 3'erli çerçevelere ayırır; artığı bir sonraki okumaya devreder.</summary>
    static void ProcessIncoming(byte[] chunk)
    {
        _carry.AddRange(chunk);

        int i = 0;
        while (i + 3 <= _carry.Count)
        {
            char header = (char)_carry[i];
            int  code   = _carry[i + 1];
            int  value  = _carry[i + 2];
            i += 3;

            switch (header)
            {
                case 'I': Console.WriteLine($"[DIJITAL] IN{code} = {value}"); break;
                case 'A': Console.WriteLine($"[ANALOG ] AI{code} = {value}"); break;
                default:  Console.WriteLine($"[BILINMEYEN BASLIK] 0x{(byte)header:X2}"); break;
            }
        }

        _carry.RemoveRange(0, i);   // işlenenleri at, artığı sakla
    }
}
```

> Bilinmeyen bir başlıkla karşılaşmak genellikle **hizanın kaymış** olduğunu gösterir (ör. akışın ortasından bağlanıldı). Üretimde bu durumda tamponu atıp yeniden senkronizasyon denenmelidir.

---

### Örnek B: WPF / WinForms Uygulamasında Kullanım

```csharp
public partial class MainPage : Form
{
    private ScadaDeviceClient _client = new ScadaDeviceClient();

    public MainPage()
    {
        InitializeComponent();

        _client.ConnectionStateChanged += Client_ConnectionStateChanged;
        _client.DataReceived += Client_DataReceived;
    }

    private async void btnConnect_Click(object sender, EventArgs e)
    {
        string ip = txtIp.Text;
        int port = int.Parse(txtPort.Text);

        bool result = await _client.ConnectAsync(ip, port);
        btnConnect.Enabled = !result;
        btnDisconnect.Enabled = result;
    }

    private async void btnOutput1Toggle_Click(object sender, EventArgs e)
    {
        await _client.TurnOnOutputAsync(1);
    }

    private void Client_DataReceived(object sender, byte[] data)
    {
        // UI thread'inde listbox güncelleme
        this.Invoke((MethodInvoker)delegate {
            lstLog.Items.Insert(0, $"Gelen: {BitConverter.ToString(data)}");
        });
    }

    private void Client_ConnectionStateChanged(object sender, string message)
    {
        this.Invoke((MethodInvoker)delegate {
            lblStatus.Text = message;
        });
    }
}
```

---

## 4. Dikkat Edilmesi Gereken Hususlar

1. **Çerçeveleme.** TCP mesaj sınırı taşımaz. Gelen tarafta 3'erli ilerleyin ve artık baytları bir sonraki okumaya devredin (§ 1.3). Tek bir `Read`'in tam bir çerçeve döndüreceğini **varsaymayın**.
2. **Yön asimetrisi.** Giden 4-5 bayt + footer, gelen 3 bayt footer'sız. Aynı ayrıştırıcıyı iki yöne kullanmayın.
3. **Kutup farkı.** Röle ve LED'de aç/kapat değerleri terstir (§ 1.2). Ham `SendCommandAsync` yerine yardımcı metotları tercih edin.
4. **Thread safety.** `SemaphoreSlim` sayesinde eşzamanlı komutlar TCP akışına sırayla yazılır, paketler iç içe geçmez.
5. **UI thread uyumluluğu.** `DataReceived` arka plan thread'inde tetiklenir. Arayüz bileşenlerini güncellerken `Control.Invoke` / `Dispatcher.Invoke` kullanın.
6. **Soket kapanması.** Karşı taraf bağlantıyı kestiğinde `ReadAsync` 0 bayt döner ve dinleme döngüsü sonlanır. Yeniden bağlanma stratejisi çağıranın sorumluluğundadır.

---

## 5. Referans proje kodları

Aşağıdaki kodlar, aynı kartla çalışan **mevcut ve çalışır durumdaki** bir projeden alınmıştır. Bu bölüm normatif kaynaktır: § 1.2'deki 4 baytlık varyant ve § 1.3'teki gelen çerçeve biçimi buradan çıkarılmıştır.

Tanımlı cihaz örnekleri:

```
--Darbe Sensörü     =>  Code:1   InOutCode:"I"
--Dış Kapı Switch   =>  Code:7   InOutCode:"I"
--Kağıt Para Kasası =>  Code:5   InOutCode:"O"
--Bilgisayar        =>  Code:6   InOutCode:"O"
--Sıcaklık          =>  Code:1   InOutCode:"A"
--Nem               =>  Code:2   InOutCode:"A"
```

### 5.1 Gönderme yolu

`SendData` başlığı ilk token'dan ASCII olarak üretir, kalan token'ları bayta çevirir ve sona `0x7A` ekler. **Zaman baytı yalnızca `delay` verilirse araya girer** — § 1.2'deki uzunluk farkının kaynağı budur.

```csharp
static TcpClient _socketConnection;
static Guid _automationId;
static string KontrolKartIp = string.Empty;
static int KontrolKartPort = 0;
static DataTable CalculateExpression = new DataTable();
static IServiceScopeFactory _serviceProviderFactory;

public static void Configure(IServiceScopeFactory serviceProviderFactory)
{
    _serviceProviderFactory = serviceProviderFactory;
    var scope = _serviceProviderFactory.CreateScope();

    _automationService = scope.ServiceProvider.GetRequiredService<IAutomationService>();
    _controlCardDeviceService = scope.ServiceProvider.GetRequiredService<IControlCardDeviceService>();
    _groupConditionService = scope.ServiceProvider.GetRequiredService<IGroupConditionService>();
    _inOutDeviceService = scope.ServiceProvider.GetRequiredService<IInOutDeviceService>();
    _inOutDeviceMovementService = scope.ServiceProvider.GetRequiredService<IInOutDeviceMovementService>();
    _logicalOperatorService = scope.ServiceProvider.GetRequiredService<ILogicalOperatorService>();
    _triggerEffectService = scope.ServiceProvider.GetRequiredService<ITriggerEffectService>();
    _triggerReasonService = scope.ServiceProvider.GetRequiredService<ITriggerReasonService>();
    _userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    _triggerService = scope.ServiceProvider.GetRequiredService<ITriggerService>();
}

public static bool SendMessage(byte[] data)
{
    if (_automationId == Guid.Empty || KontrolKartIp == "" || KontrolKartPort == 0)
    {
        var automation = _automationService.Get(x => x.Status == (byte)Enums.Status.Active);
        if (automation.Entity != null)
        {
            _automationId = automation.Entity.Id;

            var statusData = _controlCardDeviceService.Get(x => x.AutomationId == _automationId && x.Status == (byte)Enums.Status.Active);
            if (statusData.Entity != null)
            {
                KontrolKartIp = statusData.Entity.Ip;
                KontrolKartPort = statusData.Entity.Port;
            }
        }
    }
    if (_socketConnection == null)
    {
        _socketConnection = new TcpClient(KontrolKartIp, KontrolKartPort);
    }
    else if (!_socketConnection.Connected)
    {
        _socketConnection.Close();
        _socketConnection = new TcpClient(KontrolKartIp, KontrolKartPort);
    }
    if (_socketConnection != null && _socketConnection.Connected)
    {
        try
        {
            // Get a stream object for writing.
            NetworkStream stream = _socketConnection.GetStream();
            if (stream.CanWrite)
            {
                // Write byte array to socketConnection stream.
                stream.Write(data, 0, data.Length);
            }
            _socketConnection.Close();
            return true;
        }
        catch (SocketException ex)
        {
            return false;
        }
    }
    return false;
}

public static bool SendData(string data)
{
    try
    {
        var charData = data.Split("+");
        if (charData.Count() < 3) return false;

        var cmd = new List<byte>();

        for (int i = 0; i < charData.Length; i++)
        {
            cmd.Add(i == 0 ? Encoding.ASCII.GetBytes(charData[i].ToString())[0] : (byte)Convert.ToInt32(charData[i].ToString()));
        }

        cmd.Add(0x7A);

        var result = SendMessage(cmd.ToArray());
        return result;
    }
    catch (Exception ex)
    {
        return false;
    }
}

public static bool ControlCartPostData(string inOut, int code, int value, int? delay = null)
{
    try
    {
        var cmd = inOut + "+" + code + "+" + value;
        if (delay != null) cmd += "+" + delay;

        Thread.Sleep(150);
        var result = SendData(cmd);
        return true;
    }
    catch (Exception ex)
    {
        return false;
    }
}
```

### 5.2 Dinleme döngüsü

> **Bu worker artık kullanılmayacak.** SCADA verisi girişten okuduğunda doğrudan CabinetOS'un API ucuna istek atacak (§ 1.4). Kartın gönderdiği **format değişmiyor** — bu kod, o formatın referansı olarak duruyor.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // **************** PRINTER CONTROL TASK ****************
    _ = Task.Run(async () => { await PrinterStatusControl(); }, stoppingToken);

    // **************** MAIN TASK FOR ALARM ****************
    if (!string.IsNullOrEmpty(KontrolKartIp))
    {
        Byte[] bytes = new Byte[1024];
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_socketConnection == null)
                {
                    _logger.LogInformation("Step 1- _socketConnection == null");

                    _socketConnection = new TcpClient(KontrolKartIp, KontrolKartPort);
                }
                else if (!_socketConnection.Connected)
                {
                    _socketConnection.Close();
                    _socketConnection = new TcpClient(KontrolKartIp, KontrolKartPort);
                }
                if (!isFirstExecute && _socketConnection != null && _socketConnection.Connected)
                {
                    using (NetworkStream stream = _socketConnection.GetStream())
                    {
                        int length;
                        stream.ReadTimeout = 30000; // 30 saniye timeout

                        try
                        {
                            while ((length = await stream.ReadAsync(bytes, 0, bytes.Length).TimeoutAfter(TimeSpan.FromSeconds(30))) != 0)
                            {
                                var incommingData = new byte[length];
                                Array.Copy(bytes, 0, incommingData, 0, length);

                                if (length >= 3)
                                {
                                    await Task.Factory.StartNew(() => DataProcessing(incommingData));
                                }
                            }
                        }
                        catch (TimeoutException)
                        {
                        }
                    }
                }
                isFirstExecute = false;

            }
            catch (SocketException ex)
            {
                Thread.Sleep(2000);
                _logger.LogError(ex, "SocketException");
            }
            catch (Exception ex)
            {
                Thread.Sleep(2000);
                _logger.LogError(ex, "Exception");
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

### 5.3 Çerçeve çözümleme ve tetikleyici akışı

Gelen çerçevenin **3 baytlık** olduğunun ve `'I'`/`'A'` ayrımının kaynağı bu fonksiyondur. Aşağıdaki `Trigger` iş akışı (koşul değerlendirme, grup mantığı, efekt gönderimi) CabinetOS'ta **henüz kodlanmamış** otomasyon modülüne karşılık gelir; bugün ilgilendiğimiz kısım yalnızca bilginin alınması ve hangi noktadan geldiğinin çözülmesidir.

Analog dalda dikkat çeken ayrıntı: **5 dakikalık aralık kuralı** — analog bir giriş sürekli seğirdiği için her değişimde satır yazılmaz. CabinetOS aynı sorunu, analog kanalların hiç `ChannelEvent` yazmaması kuralıyla çözer.

```csharp
private void DataProcessing(byte[] incommingData)
{
    try
    {
        var dataLength = incommingData.Length;
        for (int i = 0; i < dataLength; i += 3)
        {
            //3 ve 3 ün katlarında data gelmemiş ise işleme
            if ((i + 3) > dataLength) break;

            var startByte = (char)incommingData[i];
            var deviceByte = Convert.ToInt32(incommingData[i + 1]);
            var valueByte = Convert.ToInt32(incommingData[i + 2]);

            if (startByte == 'I')
            {
                var inOutDevice = _inOutDeviceService.Get(x => x.InOut == "I" && x.Code == deviceByte && x.Status == (byte)Enums.Status.Active);
                if (inOutDevice.Entity != null)
                {
                    var lastInOutDeviceMovement = _inOutDeviceMovementService.GetList(x => x.InOutDeviceId == inOutDevice.Entity.Id && x.Status == (byte)Enums.Status.Active).Entity.OrderByDescending(x => x.CreateDate).FirstOrDefault();

                    #region CihazHareketiKaydet
                    //Hareket kaydı varsa && Kaydın durumu değişmemiş ise
                    if (lastInOutDeviceMovement != null && lastInOutDeviceMovement.StateChangeDate == null)
                    {
                        lastInOutDeviceMovement.StateChangeDate = DateTime.Now;
                        lastInOutDeviceMovement.Control = false;
                        _inOutDeviceMovementService.Update(lastInOutDeviceMovement);
                    }

                    //Cihazın son değeri değişmiş ise
                    if (inOutDevice.Entity.LastState != valueByte)
                    {
                        var entity = new InOutDeviceMovement()
                        {
                            Id = Guid.NewGuid(),
                            InOutDeviceId = inOutDevice.Entity.Id,
                            State = valueByte,
                            StateChangeDate = null,
                            AutomationId = _automationId,
                            Description = "",
                            Status = (byte)Enums.Status.Active,
                            Control = false,
                            CreateDate = DateTime.Now
                        };

                        _inOutDeviceMovementService.Add(entity);

                        inOutDevice.Entity.LastState = valueByte;
                        inOutDevice.Entity.Control = false;

                        _inOutDeviceService.Update(inOutDevice.Entity);
                    }
                    #endregion
                    #region TetikleyicileriCalistir
                    //Cihaza tanımlı TriggerReason listesi
                    var triggerReasons = _triggerReasonService.GetList(x => x.InOutDeviceId == inOutDevice.Entity.Id && x.Status == (byte)Enums.Status.Active);

                    if (triggerReasons.Entity.Count > 0)
                    {
                        //TriggerReason listesinde birden fazla Trigger a göre grupla
                        var differentTriggerIds = triggerReasons.Entity.GroupBy(x => x.TriggerId).Select(x => x.Key).ToList();

                        foreach (var triggerId in differentTriggerIds)
                        {
                            #region TriggerCondition
                            //TriggerReason şartını destekliyor mu?
                            var triggerReason = triggerReasons.Entity.FirstOrDefault(x => x.TriggerId == triggerId);
                            var triggerCondition = "(" + valueByte + " " + ((Enums.Conditions)triggerReason.ConditionId).GetEnumDescription() + " " + triggerReason.Value + ")";
                            var isTheRight = CalculateExpression.Compute("IIF (" + triggerCondition + ", 1, 0)", "").ToBoolean();
                            if (!isTheRight)
                                continue;
                            #endregion

                            //Triggere bağlı tüm TriggerReason lar
                            var sameTriggerReason = _triggerReasonService.GetList(x => x.TriggerId == triggerId && x.Status == (byte)Enums.Status.Active).Entity;

                            //Triggere bağlı tüm TriggerReason lar farklı gruplara göre gruplanıyor
                            var groupCodes = sameTriggerReason.GroupBy(x => x.GroupCode).OrderBy(x => x.Key).Select(x => x.Key).ToList();

                            var groupCounter = 0;
                            var groupCondition = "(";
                            foreach (var groupCode in groupCodes)
                            {
                                groupCounter++;

                                ////Triggere bağlı TriggerReason lar içinden aynı grup içindekiler ele alınıyor
                                var sameGroupTriggerReasons = sameTriggerReason.Where(x => x.GroupCode == groupCode).ToList();

                                var inGroupCounter = 0;
                                var inGroupCondition = "(";
                                foreach (var sameGroupTriggerReason in sameGroupTriggerReasons)
                                {
                                    inGroupCounter++;

                                    var lastState = _inOutDeviceService.Get(x => x.Id == sameGroupTriggerReason.InOutDeviceId && x.Status == (byte)Enums.Status.Active).Entity.LastState;
                                    var deviceCondition = "(" + lastState + " " + ((Enums.Conditions)sameGroupTriggerReason.ConditionId).GetEnumDescription() + " " + sameGroupTriggerReason.Value + ")";

                                    if (inGroupCounter != sameGroupTriggerReasons.Count())
                                    {
                                        var logicName = _logicalOperatorService.Get(x => x.Id == sameGroupTriggerReason.LogicalOperatorId && x.Status == (byte)Enums.Status.Active).Entity.Name;
                                        inGroupCondition += deviceCondition + (logicName == "Ve" ? " and " : " or ");
                                    }
                                    else
                                        inGroupCondition += deviceCondition + ")";
                                }

                                if (groupCodes.Count() > 1 && groupCounter < groupCodes.Count())
                                {
                                    var betweenGroupsCondition = _groupConditionService.Get(x => x.TriggerId == triggerId && x.FirstGroupCode == groupCodes[groupCounter - 1] && x.SecondGroupCode == groupCodes[groupCounter] && x.Status == (byte)Enums.Status.Active).Entity;
                                    var logicName = _logicalOperatorService.Get(x => x.Id == betweenGroupsCondition.LogicalOperatorId && x.Status == (byte)Enums.Status.Active).Entity.Name;
                                    groupCondition += inGroupCondition + (logicName == "Ve" ? " and " : " or ");
                                }
                                else
                                {
                                    groupCondition += inGroupCondition + ")";
                                }
                            }

                            //Trigger a ait tüm şartlar sağlıyor mu
                            isTheRight = CalculateExpression.Compute("IIF (" + groupCondition + ", 1, 0)", "").ToBoolean();
                            if (isTheRight)
                            {
                                var triggerEffects = _triggerEffectService.GetList(x => x.TriggerId == triggerId && x.Status == (byte)Enums.Status.Active);

                                foreach (var triggerEffect in triggerEffects.Entity)
                                {
                                    var inOutDeviceEffect = _inOutDeviceService.Get(x => x.Id == triggerEffect.InOutDeviceId && x.Status == (byte)Enums.Status.Active);
                                    if (inOutDeviceEffect.Entity != null)
                                    {
                                        var cmd = inOutDeviceEffect.Entity.InOut + "+" + inOutDeviceEffect.Entity.Code + "+" + triggerEffect.Value;

                                        Thread.Sleep(triggerEffect.DelayTime * 1000);
                                        SendData(cmd);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                }
            }
            else if (startByte == 'A')
            {
                //Sıcaklık => deviceByte == 1 , Nem => deviceByte == 2
                var inOutDevice = _inOutDeviceService.Get(x => x.InOut == "A" && x.Code == deviceByte && x.Status == (byte)Enums.Status.Active);
                if (inOutDevice.Entity != null)
                {
                    var lastInOutDeviceMovement = _inOutDeviceMovementService.GetList(x => x.InOutDeviceId == inOutDevice.Entity.Id && x.Status == (byte)Enums.Status.Active).Entity.OrderByDescending(x => x.CreateDate).FirstOrDefault();

                    //Hareket kaydı varsa && Kaydın durumu değişmemiş ise && Son kayıt üzerinden 5 dk geçmiş ise
                    if (lastInOutDeviceMovement != null && lastInOutDeviceMovement.StateChangeDate == null && lastInOutDeviceMovement.CreateDate < DateTime.Now.AddMinutes(-5))
                    {
                        lastInOutDeviceMovement.StateChangeDate = DateTime.Now;
                        lastInOutDeviceMovement.Control = false;
                        _inOutDeviceMovementService.Update(lastInOutDeviceMovement);
                    }

                    //Cihazın son değeri değişmiş ise && (Hareket kaydı yoksa || Hareket kaydı varsa && Son kayıt üzerinden 5 dk geçmiş ise)
                    if (inOutDevice.Entity.LastState != valueByte && (lastInOutDeviceMovement == null || (lastInOutDeviceMovement != null && lastInOutDeviceMovement.CreateDate < DateTime.Now.AddMinutes(-5))))
                    {
                        var entity = new InOutDeviceMovement()
                        {
                            Id = Guid.NewGuid(),
                            InOutDeviceId = inOutDevice.Entity.Id,
                            State = valueByte,
                            StateChangeDate = null,
                            AutomationId = _automationId,
                            Description = "",
                            Status = (byte)Enums.Status.Active,
                            Control = false,
                            CreateDate = DateTime.Now
                        };

                        _inOutDeviceMovementService.Add(entity);

                        inOutDevice.Entity.LastState = valueByte;
                        inOutDevice.Entity.Control = false;

                        _inOutDeviceService.Update(inOutDevice.Entity);
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "DataProcessing => Exception");
    }
}
```
