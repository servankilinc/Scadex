# Scadex RemoteDesk — PC Ekran İzleme ve Uzaktan Kontrol

> **Durum:** Mimari şartname — uygulama başlamadı. Son karar tarihi: 2026-09-29.
> **Kapsam:** `Modules/RemoteDesk/` altındaki üç proje: merkezdeki `Scadex.RemoteDesk` modülü, sahadaki
> Windows PC'lerde çalışan `Scadex.RemoteDesk.Windows` (WPF) ve ikisinin paylaştığı
> `Scadex.RemoteDesk.Contracts`.
> **Uygulayıcı:** Claude Code, bu depoda, faz faz ([§ 14](#14-fazlar)).

Bu doküman Scadex kurallarına tabidir: [CLAUDE.md](CLAUDE.md) ve [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)
bununla çelişirse **onlar kazanır**. Yorum, log, hata mesajı ve dokümanlar Türkçe; tip/metot/DTO adları
İngilizce. Kodda ve dokümanda PC tarafı için **"Agent" adı kullanılmaz**; adı "Windows istemcisi" ya da
"PC istemcisi"dir.

---

## 1. Amaç ve kapsam

Modül açıkken operatör, Scadex'te `DeviceType.Pc` olan cihazların listesini görür, bir PC'nin istediği
monitörünü canlı izler. Sonraki aşamada aynı ekrandan fare ve klavye komutu gönderir.

| | MVP (Faz 1–7) | Sonra (Faz 8+) |
|---|---|---|
| PC listesi (bağlı / bağlı değil, monitörler, izleyen sayısı) | ✅ | |
| Monitör seçerek izleme — **çoklu monitör dahil** | ✅ | |
| Yayın isteğe bağlı: izlenmeyen monitör kodlanmaz | ✅ | |
| Birden fazla izleyici aynı yayını izler | ✅ | |
| PC'de bağlantı ve "izleniyor" durumunu gösteren WPF arayüzü | ✅ | |
| İzleme denetim kaydı (kim, hangi PC, hangi monitör, ne zaman) | ✅ | |
| Fare / klavye | Sözleşme hazır, **uygulama yok** | ✅ |
| Tüm monitörler tek görüntüde, pano, dosya, ses, kayıt, otomatik güncelleme | ❌ | ✅ |
| Kilit ekranı, UAC güvenli masaüstü, oturum açılmamış PC | ❌ ([§ 9.7](#97-wpf-seçiminin-bedeli)) | ayrı bileşen gerekir |

---

## 2. Kilit kararlar

| Konu | Karar | Neden |
|---|---|---|
| PC kimliği | **`Device.MacAddress`** | PC kendi MAC'ini bilir; merkezde cihaz zaten tanımlı. Ek tablo, ek kimlik, "ekle" ekranı yok. (2026-09-29) |
| Kimlik doğrulama | **Yalnızca MAC — ek sır yok** | Kullanıcı kararı (2026-09-29). Kabul edilen risk ve sınırları: [§ 5.3](#53-kabul-edilen-risk-yalnızca-mac). |
| PC istemcisi | **WPF uygulaması** (`Scadex.RemoteDesk.Windows`), kullanıcı oturumunda, oturum açılınca otomatik başlar | Bağlantı durumu ekranda görünür; ekran yakalama ve `SendInput` doğrudan çalışır, servis + yardımcı süreç + named pipe gerekmez. (2026-09-29) |
| Dil / çatı | **.NET 10** — modül `net10.0`, istemci `net10.0-windows` | Scadex `net10.0`. |
| Ekran yakalama | **DXGI Desktop Duplication**, FFmpeg `ddagrab`, monitör başına `output_idx` | GPU tarafında, düşük CPU. `gdigrab` yalnızca teşhis. |
| Kodlama | **Donanımda H.264** (monitörün bağlı olduğu kartın NVENC / QSV / AMF'si önce), **yoksa yazılımda VP9** (libvpx); seçim gerçek denemeyle; çıkış `-fps_mode passthrough` | Dağıtılan hiçbir parçada GPL ya da açık patent yok; tarayıcı ikisini de çözer ([§ 7.4](#74-ffmpeg-hattı-istemci)). |
| Medya taşıma | **İstemci → MediaMTX, RTSP/TCP publish** | MediaMTX zaten çalışıyor, `paths.all_others` zaten `source: publisher`. |
| İzleme | **Mevcut WHEP oynatıcı** (`src/lib/camera/whep.ts`) | Kamera ile aynı kod. |
| Kontrol düzlemi | **SignalR**, istemci dışarı bağlanır: `/hubs/remote-desk/pc` | NAT sorunu yok, PC port açmaz. |
| Fare/klavye (sonra) | Tarayıcı → SignalR → **sunucu (yetki + denetim)** → SignalR → istemci → `SendInput` | Yetki ve kayıt tek noktada. |
| Merkezdeki yeri | **Müşteri modülü** `Scadex.RemoteDesk`, `Modules:RemoteDesk:Enabled` | Signalization kalıbı; kurulum başına açılır. |
| Paylaşılan tipler | **`Scadex.RemoteDesk.Contracts`** (`net10.0`, bağımlılıksız) | Hub/komut/ekran tipleri tek yerde; WPF modülü (EF, Business…) referans almaz. (2026-09-29) |

Reddedilen alternatifler (tekrar açılmasın diye):

- **Windows Service + oturum yardımcısı:** Kilit ekranına erişim sağlar ama iki süreç, named pipe ve
  `CreateProcessAsUser` getirir; bağlantı durumu ayrı bir UI ister. WPF tercih edildi (2026-09-29).
- **Ayrı ajan kimliği / ajan tablosu / "ajan ekle" ekranı:** MAC zaten var; cihaz kopyası tablo istenmiyor.
- **WebSocket üzerinden JPEG kareleri:** 1080p'de 5–10 Mbps, tıkanıklık kontrolü sıfırdan yazılır.
- **PC ↔ tarayıcı doğrudan WebRTC:** Her PC'de WebRTC yığını + STUN/TURN; MediaMTX çoklu izleyiciyi zaten
  tek yayından besliyor.

---

## 3. Mimari

```text
 Saha PC'si (Windows 10/11, kullanıcı oturumu)            Scadex merkez sunucusu
┌───────────────────────────────────────┐              ┌───────────────────────────────────────────────┐
│ Scadex.RemoteDesk.Windows  (WPF, System Tray)│── WS/WSS ──▶│ /hubs/remote-desk/pc  (PcHub)                   │
│  · MAC'leri + monitörleri bildirir      │◀── komut ────│   Hello(mac'ler) → Device(Pc) eşleşmesi          │
│  · bağlantı / yayın durumunu gösterir   │              │   StartScreenStream / StopScreenStream           │
│  · monitör başına FFmpeg (isteğe bağlı) │              │                                                  │
│      ddagrab(output_idx) → H.264        │── RTSP/TCP ─▶│ MediaMTX   pc_{deviceId:N}_{monitör}             │
│  · (sonra) SendInput                    │   publish    │   /api/MediaGateway/auth                         │
└───────────────────────────────────────┘              │     read cam_*    → CameraService (değişmez)      │
                                                       │     read/publish pc_* → RemoteDesk yetkilendirici │
                                                       │        │ WHEP 8889 + ICE 8189/UDP                 │
                                                       │        ▼                                          │
                                                       │ Tarayıcı: PC listesi → monitör seç → whep.ts      │
                                                       └───────────────────────────────────────────────┘
```

**Video kanalı ile kontrol kanalı birbirine bağlanmaz.** Video RTSP → MediaMTX → WebRTC; komutlar SignalR.

---

## 4. Ağ

İstemci **hiçbir dinleyen port açmaz**; PC'nin IP'si bilinmek zorunda değildir.

| Yön | Protokol | Merkez portu | Not |
|---|---|---|---|
| İstemci → API | HTTP(S) + WS(S) | API portu (5208 / 443) | `PcHub` |
| İstemci → MediaMTX | RTSP/TCP | 8554 | Güvenlik duvarında PC ağlarına açılır |
| Tarayıcı → MediaMTX | WHEP + ICE | 8889/TCP, 8189/UDP | Mevcut, değişmez |

**Varsayım: PC'ler merkeze kapalı LAN/VPN üzerinden ulaşır** — "yalnızca MAC" kararı ([§ 5.3](#53-kabul-edilen-risk-yalnızca-mac))
bununla birlikte anlamlıdır. PC'ler internetten bağlanacaksa bu doküman yeniden açılır: RTSPS
(`rtspEncryption: "optional"`, `:8322`; `optional` modunda Scadex'in kendi FFmpeg'inin okuduğu
`LiveRtspUrl` = `rtsp://127.0.0.1:8554` değişmez) ve bir istemci sırrı.

İstemciye verilen yayın adresi sunucu ayarıdır: `Modules:RemoteDesk:PublishRtspBaseUrl`
(ör. `rtsp://10.0.0.5:8554`). İstemcinin config'inde MediaMTX adresi, portu ya da parolası **yoktur**.

---

## 5. Kimlik

### 5.1 MAC eşleşmesi

İstemci bağlanınca `Hello` ile makinenin **fiziksel** ağ kartlarının MAC'lerini gönderir. Sunucu bunları,
**aktif** ve şablonunun `DeviceType`'ı `Pc` olan cihazların `MacAddress`'leriyle karşılaştırır.

| Sonuç | Sunucu | İstemci |
|---|---|---|
| Tam 1 cihaz | Bağlantıyı o `DeviceId`'ye kaydeder, cihaz/kabin adını döner | "Bağlı — {kabin} / {cihaz}" |
| 0 cihaz | `UnknownDevice` döner, bağlantıyı kapatır | "Bu PC Scadex'te tanımlı değil" + kendi MAC'leri (teknisyen diyagrama girer); 60 sn'de bir yeniden dener |
| >1 cihaz | `Ambiguous` (iki kart iki cihaza yazılmış) | Hata + ilgili MAC'ler |
| Cihaz zaten bağlı | `AlreadyConnected` — **ilk bağlanan kazanır**, deneme loglanır | "Bu cihaz başka bir bağlantıda" ; geri çekilerek yeniden dener |

Bu kural yalnızca **PC'deki istemci** içindir (PC başına tek istemci bağlantısı). **İzleyici sayısını sınırlamaz:** aynı PC'yi,
aynı ya da farklı monitörlerini istenen sayıda operatör aynı anda izler ([§ 8](#8-yayın-oturumu-ve-izleyiciler); kullanıcı kararı 2026-09-30).

- **MAC karşılaştırması normalize edilir** (yalnızca onaltılık haneler, büyük harf: `AA:BB-cc…` →
  `AABBCC…`). Bu, SCADA ingest'teki "ham string, normalizasyon yok" kuralından **bilinçli olarak farklıdır**:
  Windows istemcisi teknisyenin adresi diyagrama hangi ayraçla yazdığını bilemez. Karşılaştırma, modülün
  `IUnitOfWork` projeksiyonuyla okuduğu PC cihazları üzerinde bellekte yapılır (PC sayısı küçüktür).
  Çekirdekteki `IX_Device_MacAddress` farklı yazımları yakalamaz; o durumu `Ambiguous` yakalar.
- Diyagramda cihaz silinip yeniden çizilirse yeni `DeviceId` bir sonraki `Hello`'da kendiliğinden bulunur;
  istemcide hiçbir şey değişmez.
- İstemci MAC listesi: `NetworkInterfaceType` Ethernet / Wireless80211 / GigabitEthernet; sanal
  (Hyper-V `vEthernet`, VPN/TAP, loopback, tunnel) kartlar dışarıda.
- **Wi-Fi rastgele donanım adresi** açıksa MAC ağdan ağa değişir. Teknisyen Ethernet MAC'ini girmeli ya da
  bu özelliği kapatmalı; WPF ekranı hangi MAC'lerin gönderildiğini gösterir.
- `MacAddress`'i boş PC cihazı hiçbir istemciyle eşleşmez; listede "MAC tanımlı değil" olarak görünür.

### 5.2 Hub kimlik doğrulaması

`PcHub` **anonimdir** (kullanıcı JWT'si yok, ayrı token ucu yok). Bağlantı `Hello` kabul edilene kadar
hiçbir şey yapamaz: kayıtsız bağlantıdan gelen her çağrı reddedilir. Hub'ın metotları yalnızca istemcinin
kendi durumunu bildirmesine izin verir; hiçbir veri okumaz.

Yine de medya yolu korunur: yayın **bileti** olmadan `pc_` yoluna publish edilemez ([§ 7.3](#73-biletler)).
Bilet yalnızca `Hello`'su kabul edilmiş bağlantıya, yalnızca bir izleme başlatıldığında verilir.

### 5.3 Kabul edilen risk: yalnızca MAC

MAC bir sır değildir. **2026-09-29 kararıyla kabul edilen** sonuçlar:

- Bir PC'nin MAC'ini bilen biri, gerçek PC bağlı değilken onun yerine bağlanıp operatöre **sahte ekran**
  gösterebilir.
- Faz 8+'da (fare/klavye) sahte istemci, operatörün o PC'ye yazdığı tuşları (parolalar dahil) alır.

Hafifletmeler (bedava olanlar): ilk bağlanan kazanır; reddedilen ve çakışan `Hello`'lar IP'siyle loglanır;
isteğe bağlı `Modules:RemoteDesk:AllowedNetworks` (CIDR listesi, boş = herkes) ile hub ve publish yalnızca
belirli ağlardan kabul edilir. İnternetten erişim ya da Faz 8 bu kararı yeniden değerlendirmek için
doğal noktalardır.

---

## 6. Kontrol düzlemi — `PcHub`

### 6.1 Bağlantı ömrü

```text
açılış → appsettings.json oku → /hubs/remote-desk/pc bağlan → Hello → Accepted ise komut bekle
kopunca: 1 → 2 → 5 → 10 → 30 sn (üst sınır 30 sn, sonsuz)
```

- **Ayrı bir heartbeat yok.** Canlılık SignalR'ın `KeepAliveInterval` (15 sn) / `ClientTimeoutInterval`
  (30 sn) mekanizmasıdır. İstemci yalnızca **bir şey değişince** bildirir (yayın durumu, monitör listesi).
- Sunucu bağlantıları **bellekte** tutar: `DeviceId → { connectionId, mac'ler, sürüm, monitörler, yayınlar }`.
  Tek sunucu varsayımı; yatay ölçekleme SignalR backplane ister — kapsam dışı.
- Aynı PC'de birden fazla Windows oturumu varsa (hızlı kullanıcı değiştirme) **yalnızca etkin konsol
  oturumundaki** istemci bağlanır; oturum değişince (`SystemEvents.SessionSwitch`) bırakır/devralır.

### 6.2 Sözleşme (`Scadex.RemoteDesk.Contracts`)

Kodda: `Contracts/Hub/PcHubContract.cs` (hub yolu, metot adları, `IPcHubClient`, kayıtlar) ve `Contracts/Hub/MacAddress.cs`
(normalizasyon — iki taraf aynı kodu kullanır). Aşağıdaki tablo özettir; alan adları koddakidir.

İstemci → sunucu:

| Metot | Gövde → Dönüş |
|---|---|
| `Hello` | `{ macAddresses[], clientVersion, osVersion, machineName, userName, monitors[] }` → `{ status, deviceId?, deviceName?, cabinetName?, matchedMacAddresses[] }` (`status`: Accepted 1, UnknownDevice 2, Ambiguous 3, AlreadyConnected 4, NetworkNotAllowed 5) |
| `ReportMonitors` | `{ monitors[] }` — ekran eklenince/çıkınca/çözünürlük değişince |
| `ReportStreamState` | `{ sessionId, monitorIndex, state, encoder?, failureReason? }` — Faz 5 |

`monitors[]` öğesi: `MonitorInfo` — `{ index, adapterIndex, outputIndex, deviceName, left, top, width, height, isPrimary,
gpuVendor, gpuName }`, piksel cinsinden (fiziksel). Yakalama `(adapterIndex, outputIndex)` çiftiyle yapılır (§ 7.4).

Sunucu → istemci (`IPcHubClient`):

| Metot | Gövde |
|---|---|
| `StartScreenStream` | `{ sessionId, monitorIndex, publishUrl, profile? }` — bilet `publishUrl`'nin parola alanındadır; `profile` = `VideoProfile { fps, maxWidth, bitrateKbps }`, `null` = seçilen kodlayıcının varsayılanı, GOP her zaman 2 sn |
| `StopScreenStream` | `{ sessionId }` |
| *(Faz 8+)* `Input` | `{ controlSessionId, monitorIndex, events: [...] }` ([§ 12](#12-uzaktan-kontrol-faz-8--mvpde-yazılmaz-sözleşmesi-şimdiden-sabit)) |

Kurallar: JSON Scadex'le aynı — camelCase, **enum sayı**, `null` alanlar gövdede kalır; istemcinin
SignalR bağlantısı aynı `JsonSerializerOptions`'la kurulur. Sunucudan gelen `...Utc` damgalarında `Z`
yoktur, istemci UTC kabul eder.

### 6.3 İdempotency

- Yayının kimliği `sessionId`'dir; **monitör başına en fazla bir yayın** vardır.
- `StartScreenStream(S)` S zaten çalışıyorsa → yeni FFmpeg açılmaz, durum yeniden bildirilir.
- Aynı monitör için farklı `S2` gelirse → S1 durur, S2 başlar.
- `StopScreenStream(bilinmeyen/eski S)` → sessizce başarılı.
- Bağlantı koparsa istemci tüm yayınlarını durdurur; sunucu o PC'nin açık oturumlarını `Failed` sayar.

---

## 7. Medya düzlemi

### 7.1 Yol adı

`pc_{deviceId:N}_{monitorIndex}` — ör. `pc_2e9f4c8e4f8c4a2d8a9d1d7e5f7c1234_0`. Kameradaki
`cam_{id:N}_{profil}` ile aynı biçim. `mediamtx.yml`'e yol eklenmez (`all_others` = `source: publisher`).

`MediaPathCleanupWorker` yalnızca `cam_` önekli yolları aday sayar (`IMediaGateway.IsManagedPathName`);
`pc_` yolları ona görünmez. **Bu worker'a dokunulmaz.** Temizlemesi de gerekmez: worker MediaMTX **yapılandırmasındaki** yolları
siler (`v3/config/paths/delete`). `cam_` yolları API ile yapılandırmaya eklendiği için birikir. `pc_` yolları yapılandırmaya hiç
eklenmez; `all_others` üzerinden yalnızca yayıncı (PC'deki FFmpeg) bağlıyken vardır ve yayıncı gidince kendiliğinden kalkar.
PC tarafında temizlenecek şey yol değil **yayıncı, oturum ve bilettir**; bunun sahibi modüldür ([§ 8.3](#83-kiralama-ve-otomatik-durdurma)).

### 7.2 MediaMTX yetkilendirmesi

Bugün `MediaGatewayController.Auth` `read` dışındaki **her** eylemi reddediyor; kamerada publish yoktur
(kameralar MediaMTX tarafından çekilir). Değişiklik:

```text
read    cam_*  → CameraService.ValidateStreamTokenAsync         (DEĞİŞMEZ)
read    pc_*   → RemoteDesk: okuma bileti
publish pc_*   → RemoteDesk: yayın bileti (+ AllowedNetworks)
diğer her şey (cam_* publish dahil) → 401
```

Çekirdek modülü bilmez; genişleme noktası `IScadaEventObserver` kalıbındadır: Business'ta
`IMediaPathAuthorizer { bool CanHandle(string path); Task<bool> AuthorizeAsync(action, path, password, ip, ct); }`.
Controller `cam_` için bugünkü yolu izler, diğerlerini kayıtlı yetkilendiricilere sorar; modül kapalıyken
kayıt yoktur → 401.

### 7.3 Biletler

| | Okuma bileti | Yayın bileti |
|---|---|---|
| Alan | Tarayıcı | Windows istemcisi (`StartScreenStream` içinde) |
| Bağlı olduğu | yol | yol + `sessionId` |
| Ömür | kısa (WHEP el sıkışmasında bir kez doğrulanır) | **oturum boyunca**; oturum bitince silinir |
| Cache anahtarı | kameradakinden ve yayın biletinden ayrı | okuma biletinden ayrı |

- Okuma biletiyle publish, yayın biletiyle okuma yapılamaz — anahtar uzayları ayrıdır.
- Yayın bileti **tek kullanımlık değildir**: FFmpeg koparsa yeniden bağlanır ve MediaMTX auth'u tekrar sorar.
- Bilet RTSP parolasıdır: `rtsp://pc:{bilet}@merkez:8554/pc_…`; MediaMTX onu `MediaMtxAuthDto.Password`'a
  koyar. Bilet base64url'dir.
- `overridePublisher: true` (varsayılan) — geçerli bileti olan ikinci yayıncı birincisini düşürür; bilet
  oturuma bağlı olduğu için kabul edilebilir.

### 7.4 FFmpeg hattı (istemci)

FFmpeg: BtbN **8.1 LGPL** build'i (`--enable-gpl` yok). Geliştirme ortamında istemci projesinin `tools\ffmpeg\`
klasöründe elle tutulur (git'te yok), derlemede çıktıya kopyalanır. Ölçümler Faz 1 (§ 16.2) ve depo dışında
tutulan medya laboratuvarından gelir.

#### Kodlayıcı seçimi (karar 2026-09-29)

**Donanım varsa üreticinin H.264 kodlayıcısı, yoksa yazılımda VP9.** Donanım kodlayıcılarında H.264 patenti
sürücü/üreticiyle gelir; VP9 telifsizdir — dağıtılan hiçbir parçada GPL ya da açık patent sorusu kalmaz.
Tarayıcı ikisini de WebRTC'de çözer; oynatıcı gelen kodeği bilmek zorunda değildir.

```text
Kodlayıcı seçimi — monitör başına, İLK YAYINDA (boştayken CPU harcanmasın); sonuç bellekte tutulur
│
├─ 1. Monitörün bağlı olduğu ekran kartının H.264 kodlayıcısı     ─ sına ─ ✗ nedeni kaydet ─┐
│      NVIDIA → NVENC · Intel → QSV (önce GPU'da, sonra hwdownload) · AMD → AMF              │
├─ 2. Makinedeki diğer ekran kartlarının H.264 kodlayıcısı        ─ sına ─ ✗ ──────────────┤
└─ 3. VP9 libvpx — yazılım, her işlemcide (15 fps, ≤ 1600 genişlik, geri kalma bekçisi)  ◄─┘
```

- **Seçim üretici adına göre değil, gerçek denemeyle yapılır.** Her aday gerçek zinciriyle (ekran yakalama
  dahil) 8 kare kodlatılır. Kart var ama sürücü eski (NVENC < 570), kart var ama kodeği sunmuyor (HD 620'de VP9
  QSV) gibi durumlar ancak böyle anlaşılır; `-encoders` listesi donanımı olmayan makinede de NVENC/AMF gösterir.
- **Monitörün bağlı olduğu kart önce gelir:** kare o kartta oluşur. Kodlayıcı aynı karttaysa kare GPU'dan
  çıkmadan kodlanır (HD 620'de QSV: %12 → %2 CPU); farklı karttaysa kartlar arası kopya olur.
- **Çıkarılanlar:** `h264_mf` (~340 ms, `hw_encoding` ile takılıyor), OpenH264 (patent sorusu), x264 (GPL).
- NVENC ve AMF'nin gecikme/CPU değerleri henüz ölçülmedi (sahada kullanıcı test ediyor).

#### Ekran yakalama

```text
ffmpeg -init_hw_device d3d11va=cap:<adaptör> -filter_hw_device cap
       -filter_complex "ddagrab=output_idx=<çıkış>:framerate=<fps>:draw_mouse=1,<kodlayıcıya göre zincir>"
```

Monitör = (**adaptör**, **çıkış**) çifti, istemci **DXGI**'dan okur. `-f lavfi -i ddagrab` biçimi yalnızca
varsayılan adaptörün monitörlerini görür; ikinci karta bağlı monitör ancak `-init_hw_device` ile yakalanır.
Bu biçimde de kodlayıcı yetişemeyince `ddagrab` kare atlar, geri kalma birikmez (VP9 30 fps → 23 fps,
`speed` ≈ 0,99; 2026-09-29).

#### Adaylar

| Aday | Zincir (`ddagrab` sonrası) | Kodlayıcı ayarları |
|---|---|---|
| NVENC | — (D3D11 kare doğrudan) | `h264_nvenc -preset p1 -tune ull -zerolatency 1 -delay 0 -rc cbr -bf 0` |
| QSV, GPU'da | `hwmap=derive_device=qsv,format=qsv` | `h264_qsv -profile:v main -preset veryfast -async_depth 1 -look_ahead 0 -bf 0` |
| QSV, hwdownload | `hwdownload,format=bgra,scale…,format=nv12` | aynı |
| AMF | — (D3D11 kare doğrudan) | `h264_amf -usage ultralowlatency -rc cbr -bf 0` |
| VP9 yazılım | `hwdownload,format=bgra,scale…,format=yuv420p` | `libvpx-vp9 -deadline realtime -cpu-used 8 -row-mt 1 -tile-columns 2 -lag-in-frames 0 -error-resilient 1` + `-strict experimental` |

Ortak çıkış: `-b:v/-maxrate/-bufsize <kbps> -g <2×fps> -fps_mode passthrough -rtsp_transport tcp -f rtsp <url>`.

#### Kurallar — her biri ölçülmüş bir hataya karşılık gelir

- **`-fps_mode passthrough` ZORUNLU.** RTSP çıkışında FFmpeg varsayılan olarak sabit kare hızı uygular:
  kodlayıcı yetişemeyip `ddagrab` kare atlayınca boşlukları **kopya karelerle** doldurur, kopyalar da
  kodlanır, geri kalma **sınırsız büyür** (ölçüm: 30 sn'de medya zamanı 8,4 sn, `dup=260`; tarayıcıda
  dakikalarca eski görüntü). `passthrough` ile aynı yükte geri kalma ~2 sn'de sabit kalır.
- **QSV'de `-async_depth 1` ZORUNLU.** Varsayılan 4 kare tampon 15 fps'de ~300 ms ekler (364 → 58 ms) ve
  kareleri toplu gönderip tarayıcının jitter buffer'ını şişirir (izleyicide ~3 sn).
- **VP9 / AV1 RTSP'ye `-strict experimental` ile yazılır** — FFmpeg'in RTP paketleyicileri deneysel işaretli.
- **NVENC asgari sürücü ister:** FFmpeg, derlendiği `nv-codec-headers` sürümüne göre NVIDIA sürücüsü şart koşar;
  seçilen 8.1 LGPL build'i **≥ 570.0** ister (NVENC API 13.0). Eski sürücüde "Driver does not support the required
  nvenc API version" ile açılmaz (2026-09-29, sahadaki NVIDIA'lı bir PC'de görüldü). İstemci bunu ayrı bir neden
  olarak bildirir ve sıradaki kodlayıcıya düşer. Sahada eski sürücü yaygınsa seçenek: daha eski başlıklarla
  derlenmiş FFmpeg (12.1 → ≥ 531.61, 12.0 → ≥ 522.25, 11.1 → ≥ 471.41) — kendi build'imizi gerektirir.
- **QSV'de GPU'da kalan zincir** (`hwmap`) HD 620'de çalıştı; `vpp_qsv` / `scale_d3d11=format=nv12` eklenince
  doku oluşturulamıyor (`80070057`). Bu yüzden GPU'da kalan zincir ölçekleme yapmaz; ölçekleme gerekirse
  `hwdownload`'lı zincire düşülür.
- Tarayıcı için zorunlu: **B-frame yok**, **4:2:0** (`nv12`/`yuv420p`), GOP ≈ 2 sn.
- **Varsayılan profil:** donanımda 30 fps / ≤ 1920 / 3 Mbps; VP9 yazılımda 15 fps / ≤ 1600 / 2 Mbps (1080p30'da
  bu makinede yetişemedi, gecikme 3 sn'ye sıçradı). Değerler sunucudan komutla gelir.
- **Ekran kilitliyken** `ddagrab` açılmaz (`Operation not permitted`), masaüstü değişince (UAC/kilit) çalışan
  yayın `887a0026` (`DXGI_ERROR_ACCESS_LOST`) ile düşer. İstemci bunu "ekran kilitli" durumu olarak bildirir,
  kilit açılınca (`SessionSwitch`) yayını sürdürür.
- Build'de `hstack_qsv`/`xstack_qsv` var: "tüm monitörler tek görüntü" (Faz 10) GPU'da birleştirilebilir.

### 7.5 FFmpeg süreç yönetimi

- Monitör başına bir FFmpeg; yalnızca o monitör izlenirken çalışır. Süreç önceliği `BelowNormal`
  (PC'deki asıl işi yavaşlatmasın).
- Nazik durdurma (stdin'e `q`) → süre aşımında öldürme; beklenmedik çıkışta 1 → 2 → 5 → 10 → 30 sn
  yeniden deneme (oturum sürdükçe).
- stderr satır satır loglanır, **adres maskelenir** (`rtsp://***@…`) — `CameraCaptureGateway` ile aynı
  kural. Sunucuya giden `failureReason` sabit metinlerden seçilir.
- stderr'de `401` → yeniden deneme durur, `Failed` bildirilir (oturum kapanmış ya da bilet geçersiz).
- **Geri kalma bekçisi:** istemci `-progress pipe:1` çıktısından `speed` ve `out_time`'ı okur. `speed`
  10 sn boyunca 0,9'un altındaysa (kodlayıcı yetişemiyor) yayını bir alt profille (fps → çözünürlük)
  yeniden başlatır ve sunucuya bildirir. `passthrough` sınırsız birikmeyi önler; bekçi, düşük fps'li
  takılan görüntüyü önler.
- Çözünürlük değişimi, kilit ekranı/UAC geçişi Desktop Duplication'ı düşürür, FFmpeg çıkar; yeniden
  deneme karşılar. Kilit ekranı süresince görüntü yoktur ([§ 9.7](#97-wpf-seçiminin-bedeli)).
- Uygulama kapanırken ya da çökerken **sahipsiz FFmpeg kalmaz**: süreçler `KILL_ON_JOB_CLOSE`'lu bir
  Windows Job Object'e bağlanır.

---

## 8. Yayın oturumu ve izleyiciler

### 8.1 Kavramlar

```text
ScreenSession         (PC, monitör) başına en fazla 1 — bir FFmpeg'in ömrü
ViewerLease           izleyici başına — "ben bu monitörü izliyorum" kiralaması (bellekte)
RemoteControlSession  (Faz 8+) — "ben kontrol ediyorum", PC başına aynı anda tek kullanıcı
```

Farklı izleyiciler aynı PC'nin farklı monitörlerini aynı anda izleyebilir (monitör başına ayrı yayın). Bir
izleyicinin "Kapat"ı **yalnızca kendi kiralamasını** bırakır.

### 8.2 Başlatma

```text
Tarayıcı ── POST /api/RemoteDesk/pcs/{deviceId}/monitors/{index}/view
Sunucu:
  1. Cihaz aktif mi, DeviceType.Pc mi, istemcisi bağlı mı, monitör var mı   değilse ProblemDetails
  2. (deviceId, index) için yayın yoksa: ScreenSession oluştur, yayın bileti üret,
     PcHub → StartScreenStream
  3. Yol hazır olana kadar bekle (en fazla ~15 sn): IMediaGateway ile MediaMTX'te yol "ready"
     istemci Failed bildirirse → sabit mesajlı hata
  4. ViewerLease + ScreenViewLog satırı + okuma bileti
  → 200 { viewId, whepUrl, token, expirationUtc, leaseRenewSec }
```

Adım 3 zorunludur: publisher yolu yayıncı gelmeden **yoktur**, erken WHEP isteği 404 alır (kamerada
`sourceOnDemand` bekletir, burada öyle bir şey yok). Yanıt kameradaki `StreamTokenDto` biçimini taşıdığı
için oynatıcı değişmeden çalışır.

### 8.3 Kiralama ve otomatik durdurma

- Tarayıcı 15 sn'de bir `POST /api/RemoteDesk/views/{viewId}/renew`; ekrandan çıkınca
  `DELETE /api/RemoteDesk/views/{viewId}`.
- 45 sn yenilenmeyen kiralama düşer (sekme çökmesi `DELETE` göndermez).
- Kiralaması kalmayan yayın 10 sn sonra `StopScreenStream` alır.
- Yedek emniyet: MediaMTX'te `readers == 0` olan `pc_` yolu 60 sn sonra durdurulur.
- Durdurmada yayın bileti hemen silinir. İstemci `StopScreenStream`'e uymazsa sunucu o RTSP bağlantısını MediaMTX API'siyle
  zorla kapatır (kick; `IMediaGateway`'e eklenecek). Bilet silindiği için FFmpeg yeniden yayın yapamaz.
- İstemci koparsa yayın `Failed` olur, istemci kendi FFmpeg'lerini durdurur; oynatıcı kopar ve kullanıcıya bildirilir.
- Sunucu yeniden başlarsa bellekteki kiralamalar ve hub bağlantıları düşer; istemciler FFmpeg'lerini durdurur. Açılışta DB'de
  açık kalan `ScreenSession` satırları `Failed` / `ServerRestart` olarak kapatılır.

### 8.4 Oturum durumu

```text
Created → CommandSent → Streaming → Stopping → Stopped
             └──────────────┴──→ Failed (failureReason)
```

---

## 9. Windows istemcisi — `Scadex.RemoteDesk.Windows`

### 9.1 Süreç modeli

Tek bir WPF süreci, **oturum açmış kullanıcının** oturumunda çalışır. Servis, yardımcı süreç ve named pipe
**yoktur**.

- **Otomatik başlatma:** kurulum, her kullanıcı için "oturum açılınca" tetiklenen bir Görev Zamanlayıcı
  görevi oluşturur. (Run anahtarı yerine görev, çünkü Faz 8'de UIPI için "en yüksek ayrıcalıkla çalıştır"
  seçeneği UAC sorusu çıkarmadan açılabilir.)
- **Tek örnek:** oturum başına `Local\Scadex.RemoteDesk.Windows` mutex'i; ikinci örnek mevcut pencereyi
  öne getirip çıkar.
- **Pencere kapatılınca uygulama kapanmaz, System Trayye iner.** Çıkış yalnızca System Tray menüsünden.
- Uygulama **Per-Monitor DPI Aware v2**'dir (`app.manifest`); monitör boyutları ve (sonra) fare
  koordinatları fiziksel piksel olarak doğru çıkar.

### 9.2 Arayüz

System Tray ikonu (WinForms `NotifyIcon`, `UseWindowsForms` — üçüncü parti paket yok) + tek pencere:

| Alan | İçerik |
|---|---|
| Bağlantı | Bağlı / Bağlanıyor / Bağlantı yok / **Tanımsız cihaz** / Başka bağlantıda — son hata |
| Merkez | Sunucu adresi, eşleşilen kabin ve cihaz adı |
| Bu PC | Gönderilen MAC'ler (kopyalanabilir — teknisyen diyagrama girer), makine adı, sürüm |
| Monitörler | Liste; izlenen monitör(ler) işaretli, kodlayıcı |
| İzleniyor | Aktif yayın varken **System Tray ikonu değişir** ve pencerede belirgin gösterge çıkar |

System Tray menüsü: Göster · Yeniden bağlan · Günlük klasörünü aç · Çıkış.

### 9.3 Minimal kaynak kullanımı

- **Boştayken:** yalnızca SignalR WebSocket'i (15 sn keep-alive). Zamanlayıcı yok, yoklama yok, CPU ≈ 0.
- **UI yalnızca olayla güncellenir:** servisler durum değişince olay yayar, ViewModel özelliği değişir;
  gizli pencere çizilmediği için bunun maliyeti yoktur. Zamanlayıcıyla yenilenen kontrol ve animasyon yok.
- Monitör listesi yalnızca açılışta ve `SystemEvents.DisplaySettingsChanged`'de okunur; MAC listesi yalnızca
  bağlanırken.
- Kodlayıcı denemesi ilk yayında (§ 7.4); FFmpeg yalnızca izlenen monitör için, `BelowNormal` öncelikte.
- Workstation GC, `ReadyToRun` yayın (hızlı açılış). Hedef: boşta **< 100 MB** bellek, **≈ %0 CPU**
  ([§ 16](#16-kaynak-ve-performans-ölçümü)'da ölçülür).

### 9.4 Yapılandırma

Uygulama klasöründeki `appsettings.json` (`Program Files` altında — kurulum yazar, kullanıcılar için salt
okunur). Generic Host'un içerik kökü **`AppContext.BaseDirectory`**'dir; çalışma klasörüne güvenilmez
(oturum açılışında başlatılan süreçte `System32` olur).

```json
{ "RemoteDesk": { "CentralApiUrl": "http://10.0.0.5:5208" } }
```

Tek ayar budur; `IOptions<RemoteDeskClientOptions>` ile okunur. MAC'ler makineden okunur; video profili ve
yayın adresi sunucudan gelir. FFmpeg yolu uygulama klasörüne göreli sabittir (`tools\ffmpeg\ffmpeg.exe`).

### 9.5 Loglama

Serilog, `%LocalAppData%\Scadex\RemoteDesk\logs` (kullanıcı başına; günlük dönen dosya, 7 gün, boyut
sınırlı), seviye Information. Olaylar: açılış, bağlandı/koptu, `Hello` sonucu, komut alındı/reddedildi,
FFmpeg başladı/çıktı (çıkış kodu), yeniden deneme. **Bilet asla loglanmaz**; FFmpeg adresi maskelenir.

### 9.6 Kurulum

- **Inno Setup:** `Program Files\Scadex\RemoteDesk\` + `tools\ffmpeg\` → merkez adresi sorulur →
  `appsettings.json` → oturum açma görevi → uygulama başlatılır.
- **Saha dağıtımı (2026-09-29):** paket ayrı bir betikle değil standart komutla alınır ve proje sahibi elle dağıtır:
  `dotnet publish Modules/RemoteDesk/Scadex.RemoteDesk.Windows -c Release -r win-x64 --self-contained true -o <klasör>`
  (.NET kurulumu gerekmez; LGPL FFmpeg `tools\ffmpeg\` altında gelir — geliştirme ortamında yoksa paket FFmpeg'siz çıkar ve
  uygulama "FFmpeg bulunamadı" uyarısı verir). Ekranda her monitör için kodlayıcı sınaması, seçilen kodlayıcı/zincir/
  profil ve canlı fps/hız/CPU görünür; "Bu adayla yayınla" ile adaylar elle karşılaştırılır.
- Binary'ler kod imzalı olmalı (uzaktan erişim yazılımları AV'lerce sık işaretlenir).
- Otomatik güncelleme MVP dışı; geldiğinde imzalı paket + imza doğrulaması.

### 9.7 WPF seçiminin bedeli

Kullanıcı oturumunda çalışan bir uygulama şunları **göremez / yapamaz**: kilit ekranı, oturum açılmamış PC,
UAC güvenli masaüstü, Ctrl+Alt+Del. Kullanıcı uygulamayı System Trayden kapatırsa PC bir sonraki oturum açılışına
kadar bağlı değildir (listede "bağlı değil" görünür). Bunlar gerekirse ileride **ayrı, küçük bir Windows
servisi** eklenir; WPF istemcisi o zaman arayüz olarak kalır.

---

## 10. Merkez: `Scadex.RemoteDesk` modülü

Signalization modülünün kalıbı birebir izlenir (CLAUDE.md § "Müşteri modülleri").

### 10.1 Kayıt

- `Modules:RemoteDesk:Enabled` (tanımsız = kapalı; `appsettings.json`'da `false`,
  `appsettings.Development.json`'da `true`). `Program.cs` → `AddControllers()` zincirinde
  `.AddRemoteDeskModule(configuration)` (servisler + ApplicationPart çıkarma) ve `MapHub<DiagramHub>` yanında
  `app.MapRemoteDeskModule(...)` (`/hubs/remote-desk/pc`, sonra `/hubs/remote-desk/viewer`).
- Frontend aynası: `VITE_MODULES=signalization,remotedesk`, `src/modules/remotedesk/`.
- Kendi context'i: `RemoteDeskDbContext`, şema `remotedesk`, `remotedesk.__EFMigrationsHistory`.

### 10.2 Tablolar (şema `remotedesk`) — cihaz kopyası YOK

| Tablo | Alanlar | Not |
|---|---|---|
| `ScreenSession` | `Id`, `DeviceId`, `MonitorIndex`, `MediaPath`, `Status`, `CreatedUtc`, `StartedUtc`, `StoppedUtc`, `StopReason`, `FailureReason` | Bir FFmpeg ömrü |
| `ScreenViewLog` | `Id`, `ScreenSessionId`, `DeviceId`, `MonitorIndex`, `UserId`, `StartedUtc`, `EndedUtc` | **Kim, hangi PC'nin hangi monitörünü, ne zaman izledi** |
| `RemoteControlSession` *(Faz 8+)* | `Id`, `DeviceId`, `UserId`, `StartedUtc`, `EndedUtc`, `EndReason` | |

`DeviceId`/`UserId` FK değildir (modül kuralı). PC'nin adı, kabini ve MAC'i her zaman çekirdekteki
`Device`'tan okunur; bağlantı durumu, istemci sürümü ve monitör listesi **bellektedir** — hiçbiri tabloya
kopyalanmaz. Saklama/temizlik işi yok (Scadex'in genel kararı; proje sahibi sonra ekler).

### 10.3 Çekirdekte gereken minimum değişiklik

| Yer | Değişiklik |
|---|---|
| `Scadex.Model/Enums/EntityEnums.cs` | `DeviceType.Pc = 13` |
| `Scadex.DataAccess/Contexts/AppDbContext.cs` (DEVICE TYPE seed) | `Pc` satırı + migration `AddPcDeviceType`. PC **Peripheral değildir**: mevcut "Bilgisayar" sistem şablonu Peripheral'dan `Pc` tipine taşındı; Id'si eski (Peripheral, 7) kimliğinde kalır (Id değişseydi ona bağlı cihazlar migration'ı kırardı). Migration'da işlem sırası elle düzeltildi: önce tip, sonra şablon (EF tersini üretiyor, FK patlar) |
| `Scadex.Business/Utils/MediaGateway/IMediaPathAuthorizer.cs` | Genişleme noktası ([§ 7.2](#72-mediamtx-yetkilendirmesi)) |
| `Scadex.WebAPI/Controllers/MediaGatewayController.cs` | `cam_` yolu yetkilendiricilere **hiç sorulmaz**, bugünkü yoldan geçer; diğer yollar onu üstlenen yetkilendiriciye, kimse üstlenmezse bugünkü yola (→ 401). `MediaMtxAuthDto` XML doc'u güncellendi |
| Frontend `models/enums/entityEnums.ts`, palet ikonu, şablon ekranı tip listesi | `DeviceType.Pc = 13` aynası ("Bilgisayar") |
| `Scadex.WebAPI/Program.cs` | Modülün iki kayıt satırı (Faz 2'de `.AddRemoteDeskModule`, hub eşlemesi Faz 3'te) |
| `appsettings*.json` | `Modules:RemoteDesk:{Enabled, PublishRtspBaseUrl, AllowedNetworks}` |
| `CLAUDE.md` | Modül bölümü + "`Scadex.slnx` 6 proje" cümlesi (artık WPF dahil 9) |

Modül çekirdek tablolarına **yazmaz**; `Device`'ı `IUnitOfWork` ile projeksiyonla okur. Başka çekirdek
değişikliği yoktur.

`Scadex.slnx` artık WPF projesini içerir: `dotnet build Scadex.slnx` yalnızca Windows'ta derlenir.

### 10.4 Canlılık

İstemcinin bağlanması/kopması PC cihazının canlılık **kanıtıdır** ve yalnızca `ICabinetStatusService`
üzerinden verilir (CLAUDE.md: `Device.DeviceStatusId`/`LastSeen`'e başka yerden yazılmaz; gerekirse
servise bu kanıt için metot eklenir). Mevcut kural gereği kontrol modülü dışındaki cihazın `Offline`'ı
kabine `Warning` olarak yansır — bkz. [§ 17](#17-açık-kararlar).

### 10.5 Uç noktalar (kullanıcı JWT'si)

| Uç | İş |
|---|---|
| `GET /api/RemoteDesk/pcs` | Aktif `DeviceType.Pc` cihazları: ad, kabin, MAC, bağlı mı, istemci sürümü, monitör sayısı, izleyen sayısı |
| `GET /api/RemoteDesk/pcs/{deviceId}` | Aynısı + monitör listesi ve her monitörün yayın durumu |
| `POST /api/RemoteDesk/pcs/{deviceId}/monitors/{index}/view` | İzlemeyi başlat ([§ 8.2](#82-başlatma)) |
| `POST /api/RemoteDesk/views/{viewId}/renew` · `DELETE /api/RemoteDesk/views/{viewId}` | Kiralama |

Kurallar: `Result<T>` → çıplak DTO, hatalar ProblemDetails; controller'lar modülün temel sınıfından türer
(`SignalizationControllerBase` kalıbı); rate limit takılmaz (modül kararı); hiçbir metot `companyId` almaz;
C# DTO değişince TS aynası elle güncellenir.

### 10.6 Frontend (`src/modules/remotedesk/`)

- **PC listesi** (modül menüsü): kamera listesi ekranı kalıbında; bağlantı durumu rozeti, kabin, MAC
  ("tanımlı değil" uyarısı), monitör sayısı, izleyen sayısı. Durum `refetchInterval` ile 10 sn'de bir
  yenilenir (MVP'de ayrı bir tarayıcı hub'ı yok).
- **PC ekranı:** monitör seçici (her monitör: numara, çözünürlük, birincil işareti) + oynatıcı. Mevcut
  `src/lib/camera/whep.ts` + `stream-session.ts` yeniden kullanılır; yalnızca bilet kaynağı ve kiralama
  yenileme hook'u farklıdır. Monitör değiştirmek eski kiralamayı bırakıp yenisini açar.
- **PC yayınında alıcıya `receiver.jitterBufferTarget = 0`** verilir (Chrome): Faz 1'de jitter buffer'ı
  106 ms'den 55 ms'ye indirdi. Bu, `whep.ts`'e isteğe bağlı bir seçenek olarak eklenir; **kamera akışının
  varsayılanı değişmez** (kamera ağında titreme daha fazla, tampon orada işe yarıyor).
- Sorgu anahtarları `['remoteDesk', …]`; sunucu damgaları için `toUtcDate` / `formatUtcDateTime`.

---

## 11. Güvenlik özeti

| Tehdit | Önlem |
|---|---|
| Sahte PC istemcisi | **Kabul edilmiş risk** ([§ 5.3](#53-kabul-edilen-risk-yalnızca-mac)); ilk bağlanan kazanır, loglama, `AllowedNetworks` |
| Hub üzerinden veri sızdırma | `PcHub` hiçbir veri döndürmez; `Hello` öncesi her çağrı reddedilir |
| Biletsiz / başka yola / kamera yoluna yayın | Yayın bileti yol + oturuma bağlı; `cam_*` publish her zaman 401 |
| Okuma biletiyle yayın ya da tersi | Ayrı cache anahtar uzayları |
| Bilet sızıntısı | Loglarda maske; ağ LAN/VPN varsayımı |
| İzlemenin fark edilmemesi | `ScreenViewLog`; PC'de System Tray ve pencere göstergesi |

> **Yetki:** Çekirdekte yetki zorlaması bilinçli olarak yok (CLAUDE.md). Ekran izleme ve özellikle uzaktan
> kontrol bu boşluğun en ağır sonuçlandığı yerdir; Faz 8 öncesi karar gerekir ([§ 17](#17-açık-kararlar)).

---

## 12. Uzaktan kontrol (Faz 8+) — MVP'de yazılmaz, sözleşmesi şimdiden sabit

### 12.1 Kanal

```text
Tarayıcı ── /hubs/remote-desk/viewer (kullanıcı JWT) ──▶ sunucu: RemoteControlSession + yetki + denetim
         ──▶ PcHub.Input ──▶ WPF istemcisi ──▶ SendInput
```

- Kontrol PC başına aynı anda **tek kullanıcıya** verilir; diğerleri izlemeye devam eder.
- Tarayıcı olayları ~60 Hz paketlerle gönderir. **Hareket için son-durum semantiği** (işlenmemiş
  `move`'lardan yalnızca sonuncusu); `down/up/wheel/key` **asla** düşürülmez, sırası korunur.

### 12.2 Olay modeli

```json
{ "controlSessionId": "…", "monitorIndex": 1, "events": [
  { "seq": 18392, "t": 1780301234567, "type": 1, "x": 0.532, "y": 0.381 },
  { "seq": 18393, "t": 1780301234571, "type": 2, "button": 0 },
  { "seq": 18394, "t": 1780301234650, "type": 3, "button": 0 },
  { "seq": 18395, "t": 1780301234700, "type": 4, "deltaY": -120 },
  { "seq": 18396, "t": 1780301234800, "type": 5, "code": "KeyA" }
]}
```

`type` sayıdır: `Move=1, Down=2, Up=3, Wheel=4, KeyDown=5, KeyUp=6`. `button`: `Left=0, Middle=1, Right=2`
(DOM ile aynı).

### 12.3 Koordinatlar ve çoklu monitör

- `x, y` ∈ [0, 1], **izlenen monitörün** alanına göre normalize.
- Tarayıcıda normalizasyon video elemanının kutusuna değil, `object-fit: contain` ile **çizilen görüntü
  dikdörtgenine** göre yapılır (letterbox payı çıkarılır).
- İstemci: `left + x·width`, `top + y·height` (monitörün fiziksel dikdörtgeni, `Hello`/`ReportMonitors`'taki
  değerler) → sanal masaüstü → `SendInput` (`MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`, 0–65535).
  Negatif `left/top`'lu (ana ekranın solunda/üstünde) monitörler bu dönüşümle doğru çalışır.

### 12.4 Klavye

- Tarayıcıdan **`KeyboardEvent.code`** gönderilir (`key` değil); istemci **scan code**'a çevirip
  `KEYEVENTF_SCANCODE` ile basar — tuş PC'nin kendi düzeniyle yorumlanır (Türkçe Q'da `ğ, ş, ı` doğru).
  `code → scan code` tablosu `Scadex.RemoteDesk.Contracts`'tadır.
- Win, Alt+Tab, Ctrl+W tarayıcıda yakalanamaz: tam ekranda Keyboard Lock API (Chromium), yoksa araç
  çubuğunda özel düğmeler.
- Ctrl+Alt+Del `SendInput` ile gönderilemez ([§ 9.7](#97-wpf-seçiminin-bedeli)).

### 12.5 Takılı tuş emniyeti

İstemci basılı tuş/düğmeleri izler; kontrol bittiğinde, bağlantı koptuğunda ya da 5 sn olay gelmezken
basılı bir şey kalmışsa **hepsini bırakır**. Tarayıcı `blur`/`visibilitychange`'de de bırakma gönderir.

### 12.6 UIPI

Normal yetkiyle çalışan istemci **yönetici olarak çalışan pencerelere** tıklayamaz/yazamaz. Çözüm:
oturum açma görevini "en yüksek ayrıcalıkla" çalıştırmak (§ 9.1). Güvenlik yüzeyini büyüttüğü için Faz 8'de
ayrıca karar verilir.

---

## 13. Çözüm yapısı

```text
Modules/RemoteDesk/
  Scadex.RemoteDesk.Contracts/     net10.0, bağımlılıksız — hub metot adları, komut/durum/monitör tipleri,
                                   enum'lar, (Faz 8) giriş olayları + code→scancode tablosu
  Scadex.RemoteDesk/               net10.0 class library — merkez modülü
    Controllers/ Hubs/ Realtime/ Services/{Abstract,Concrete}/ Data/ Model/ ServiceRegistration.cs
    → Contracts + Scadex.Business (Signalization ile aynı referans yönü)
  Scadex.RemoteDesk.Windows/       net10.0-windows, WPF (+ UseWindowsForms: NotifyIcon) → yalnızca Contracts
    App.xaml(.cs)                  Generic Host: DI kayıtları, açılış/kapanış
    appsettings.json               RemoteDesk:CentralApiUrl
    Helpers/                       BaseViewModel, ObservableProperty (+ SetProperty), RelayCommand — projenin
                                   kendi MVVM altyapısı; CommunityToolkit.Mvvm kullanılmaz
    Templates/DataTemplate.xaml    VM → View eşlemesi (DataType ile)
    ViewModels/                    MainWindowVM (CurrentView ile gezinme), HomeVM (durum ekranı), …
    Views/                         MainWindow, Home, … — code-behind yalnızca InitializeComponent
    Services/                      Connection (PcHub istemcisi), Monitors, Streaming (FFmpeg + Job Object),
                                   Network (MAC), Tray, (Faz 8) Input
    tools/ffmpeg/                  LGPL build, git'te değil (Scadex'teki MediaTools gibi)
    installer/                     Inno Setup betiği
RemoteDesk.md                      bu doküman (depo kökü)
```

İstemci kod kuralları:
- **MVVM, projenin kendi altyapısıyla:** ViewModel'ler `BaseViewModel`'den türer, komutlar `RelayCommand`,
  ekran değişimi `MainWindowVM.CurrentView` + `DataTemplate.xaml`. View code-behind'ında iş kodu yok.
- **DI, Generic Host ile** (`Microsoft.Extensions.Hosting`): servisler ve ViewModel'ler singleton;
  View'lardan yalnızca `MainWindow` DI'dadır (diğerleri DataTemplate ile oluşur). Uzun ömürlü işler
  (bağlantı) `IHostedService`.
- **UI iş mantığı taşımaz:** bağlantı, FFmpeg, monitör okuma ve (sonra) giriş servistedir; ViewModel yalnızca
  servis olaylarını ekran durumuna çevirir.
- **İş parçacığı:** SignalR ve süreç olayları iş parçacığı havuzundan gelir. Tekil özellik bildirimleri WPF
  tarafından taşınır; `ObservableCollection` değişiklikleri ve komutların `RaiseCanExecuteChanged`'i
  `Dispatcher` üzerinden yapılır.
- **Kapanış tek yoldan:** System Tray "Çıkış" → `await host.StopAsync()` (yayınlar durur, bağlantı kapanır) →
  `Application.Shutdown()`. Pencere kapatma yalnızca gizler.
- Nullable açık, async + `CancellationToken`, Options, Serilog.

Test: Scadex'te test paketi yoktur; doğrulama `dotnet build Scadex.slnx`, `npm run lint`, `npm run build`
ve **çalıştırarak** yapılır. İstemcinin saf mantığı (idempotency, MAC normalizasyonu, FFmpeg argüman
üretimi, maske) ayrı test projesi açılmadan önce kullanıcıya sorulur.

---

## 14. Fazlar

Her faz bir öncekinin başarı kriteri sağlanmadan başlamaz.

| Faz | İş | Başarı kriteri |
|---|---|---|
| **0** | Proje iskeleti: Contracts bağımlılıksız, modül csproj'u Signalization kalıbında, WPF'te `Microsoft.Extensions.Hosting` + içerik kökü + kapanış yolu + namespace düzeni | `dotnet build Scadex.slnx` temiz (`NU1903` hariç); WPF açılıp temiz kapanıyor |
| **1** ✅ | **Medya hattı kanıtı** (2026-09-29 tamamlandı — § 16.2) — kod yazmadan, elle FFmpeg ile iki monitörden `pc_test_0/1`'e publish (geçici auth'suz yerel yol) → WHEP ile tarayıcı. Kodlayıcı başına filtre zinciri; `output_idx` ↔ DXGI sırası. | İki monitör tarayıcıda; uçtan uca ≈ 130–200 ms (QSV); yazılım yedeği `libopenh264` ~52 ms zincir |
| **2** ✅ | Çekirdek: `DeviceType.Pc` + seed + migration, `IMediaPathAuthorizer` + controller. Modül iskeleti: kayıt, `RemoteDeskDbContext`, şema, migration. Contracts projesi. **2026-09-29:** + bilet deposu (`ScreenTicketStore`), `pc_*` yetkilendiricisi (`AllowedNetworks` dahil), `GET /api/RemoteDesk/pcs`. | Modül kapalıyken `pcs` 404 ve OpenAPI'de yok (JWT'li istekle sınandı); açıkken `remotedesk` şeması + iki tablo oluştu, `pcs` 200; gerçek kamera biletiyle `cam_` okuma 200, aynı biletle `cam_` publish ve `pc_` okuma 401; biletsiz/yanlış biletli `pc_` publish 401. Hub negotiate kriteri Faz 3'e kaldı (hub henüz yok) |
| **3** ✅ | `PcHub` + `Hello`/MAC eşleşmesi + bellek kaydı + canlılık. WPF: tek örnek, System Tray, bağlantı ekranı, `appsettings.json`, yeniden bağlanma (henüz yayın yok). **2026-09-29:** canlılık yalnızca bellekte (`PcConnectionRegistry`), `Device` durumuna yazılmaz (§ 17 #1 önerisi). Yapılmadı: hızlı kullanıcı değiştirme (`SessionSwitch`), System Trayde "günlük klasörü" (Serilog henüz yok), "izleniyor" ikonu (Faz 5). | Sınandı (bu makine): tanımsız PC'de iki fiziksel kartın MAC'i gösterildi; diyagrama farklı biçimde (`50-8d-…`, küçük harf) yazılan MAC eşleşti → "Bağlı — kabin / cihaz", `pcs` `isConnected: true`, 2 monitör; modül kapalıyken negotiate 404 ve istemcide "modül kapalı"; merkez yeniden başlayınca kendiliğinden bağlandı; ikinci örnek mevcut pencereyi öne getirip çıktı; pencereyi kapatmak System Trayye indirdi; gizliyken boşta 30 sn'de 47 ms CPU (≈%0,16), private bellek 60 MB (working set 144 MB). Sınanmadı: `Ambiguous`, `AlreadyConnected`, System Tray "Çıkış" |
| **4** ◐ | WPF yayın: monitör listesi, FFmpeg yöneticisi, kodlayıcı denemesi, `Start/StopScreenStream`, idempotency, Job Object. **2026-09-29: merkezden önce yapıldı** — DXGI monitör/ekran kartı okuma, kodlayıcı sınaması ve seçimi (§ 7.4), monitör başına test yayını (yalnızca kodla / RTSP), geri kalma bekçisi, kilit ekranında bekleme, Job Object ve saha ekranı hazır; `StartScreenStream` komutu Faz 3 (PcHub) ile bağlanacak. | Sunucudan elle tetiklenen komutla seçilen monitör MediaMTX'e yayınlanıyor; uygulama öldürülünce FFmpeg kalmıyor |
| **5** | İzleme akışı: `view` ucu, hazır-bekleme, biletler, kiralama, otomatik durdurma, `ScreenSession`/`ScreenViewLog`. | Tarayıcı kapanınca ≤ 60 sn'de FFmpeg duruyor; iki izleyici aynı yayını; iki izleyici farklı monitörleri |
| **6** | Frontend: PC listesi, PC ekranı (monitör seçici + oynatıcı), `VITE_MODULES`. | `npm run lint` + `npm run build` yeşil; uçtan uca izleme |
| **7** | Kurulum paketi + oturum açma görevi + kaynak ölçümü ([§ 16](#16-kaynak-ve-performans-ölçümü)). `CLAUDE.md` güncellemesi. | Temiz PC'ye kurulup oturum açılınca kendiliğinden bağlanıyor; hedef değerler tutuyor |
| **8** | Fare ([§ 12](#12-uzaktan-kontrol-faz-8--mvpde-yazılmaz-sözleşmesi-şimdiden-sabit)) + viewer hub + `RemoteControlSession` + yetki kararı | |
| **9** | Klavye (scan code, değiştirici tuşlar, takılı tuş emniyeti) | |
| **10** | Tüm monitörler tek görüntü, pano | |
| **11** | Kilit ekranı / UAC için servis bileşeni, otomatik güncelleme, ses, kayıt | |

Her faz sonunda rapor: değişen dosyalar · eklenen özellik · mimari değişiklik · nasıl doğrulandı · nasıl
çalıştırılır · bilinen kısıtlar · sonraki adım.

---

## 15. Test matrisi

**Bağlantı:** config yok/bozuk · merkez erişilemez · tanımsız MAC · iki MAC iki cihazda (`Ambiguous`) ·
MAC farklı ayraçla girilmiş · aynı PC ikinci kez bağlanıyor · ağ kesintisi · uygulama System Trayden kapatılıp
açılıyor · hızlı kullanıcı değiştirme · cihaz diyagramda silinip yeniden çiziliyor.

**Yayın:** başlat · durdur · çift başlat · çift durdur · aynı monitöre farklı oturum · iki monitör aynı anda
· monitör takılıp çıkarılıyor · çözünürlük/DPI değişimi · 4K ekran · negatif koordinatlı monitör ·
kilit ekranı · MediaMTX kapalı · FFmpeg çökmesi · sekme öldürülür (kiralama düşer) · iki izleyici, biri
çıkar.

**Güvenlik:** biletsiz / süresi dolmuş / başka oturumun bileti · okuma biletiyle publish · `cam_*` publish ·
`Hello` öncesi hub çağrısı · `AllowedNetworks` dışından bağlantı · loglarda bilet olmaması.

**Kontrol (Faz 8+):** hareket · tıklama · sürükleme · tekerlek · Türkçe karakterler · Ctrl/Alt/Shift/Win ·
sıra · kopukta takılı tuş · yetkisiz kullanıcı · ikinci kullanıcı kontrol ister · letterbox kenarı · ikinci
monitörde tıklama.

---

## 16. Kaynak ve performans ölçümü

### 16.1 Hedefler

| Durum | Hedef |
|---|---|
| Boşta (bağlı, izlenmiyor) | CPU ≈ %0, bellek < 100 MB, ağ yalnızca keep-alive |
| Bir monitör izleniyor, donanım kodlayıcı | CPU < %15 (2 çekirdek/4 iş parçacığı), ~3 Mbps — `hwdownload`'sız GPU zinciri açılan donanımda daha düşük |
| Bir monitör izleniyor, yazılım (OpenH264) | `speed` ≥ 0,9 sürekli (geri kalma bekçisi, § 7.5) |
| Uçtan uca gecikme (tarayıcı dahil) | < 300 ms |

### 16.2 Faz 1 ölçümleri (2026-09-29)

Makine: Intel i7-7500U (2 çekirdek/4 iş parçacığı), Intel HD 620, 2 × 1920×1080; yayıncı ve izleyici aynı
makinede; Chrome.

**Zincir gecikmesi** — monitör 1'de 1,5 sn'de bir siyah/beyaz değişen 120×120 kare; değişimin RTSP okuyucuya
varış zamanı (duvar saati), 10 ölçüm ortalaması. Tarayıcı hariç.

| Varyant | Ort. | Aralık | CPU |
|---|---|---|---|
| QSV 15 fps, varsayılan `async_depth` | 364 ms | 340–394 | %14,1 |
| **QSV 15 fps, `async_depth 1`** | **58 ms** | 46–67 | %12,2 |
| **QSV 30 fps, `async_depth 1`** | **57 ms** | 45–70 | %17,4 |
| aynı + MediaMTX WHEP çıkışı (WebRTC ayağı dahil) | 66 ms | 47–222 | — |
| `h264_mf` varsayılan | 346 ms | 266–474 | %14,6 |
| `h264_mf` `display_remoting` (yazılım) | 341 ms | 280–385 | %15,6 |
| `h264_mf` `hw_encoding 1` | çıktı yok (takılıyor) | — | — |
| **`libopenh264` 15 fps** | **52 ms** | 41–68 | %10,4 |

**Tarayıcı** (test sayfası, Chrome `getStats`, QSV 30 fps): jitter buffer 106 ms/kare, `jitterBufferTarget = 0`
ile 55 ms; çözme 4–9 ms/kare; düşen kare yok; PC ekranındaki sayaç ile izlenen görüntü aynı saniyede
(uçtan uca ≈ 130–200 ms).

**Geri kalma** — kodlayıcı kasıtlı yavaşlatıldı (OpenH264, 4K'ya büyütülmüş), 30 sn: varsayılan CFR'de
medya zamanı 8,4 sn / `dup=260` (sınırsız büyüyor); `-fps_mode passthrough`'ta 28,2 sn, `dup=0` (sabit ~2 sn).

Ölçüm düzeneği (yanıp sönen kare + RTSP okuyucu + WHEP test sayfası) depoya alınmadı; Faz 4'te istemcinin
kendi test komutu olarak yeniden yazılabilir.

---

## 17. Açık kararlar

Önerilen seçenek ilk sıradadır:

1. **PC bağlı değilken kabin `Warning`'e düşsün mü?** *(Faz 3'te öneri uygulandı — bağlantı yalnızca bellekte, `Device.DeviceStatusId`'e yazılmıyor; onay bekliyor.)* Öneri: hayır — kullanıcı uygulamayı kapatınca ya da PC
   kapalıyken kabin uyarıya geçerdi; Signalization gibi **bilerek** kabin durumuna yansımasın ve bu
   CLAUDE.md'ye yazılsın. (Evet denirse [§ 10.4](#104-canlılık) aynen uygulanır.)
2. **İzlendiğine dair onay.** Öneri: onay yok, yalnızca görünür gösterge (System Tray + pencere) — saha PC'lerinin
   başında çoğu zaman kimse yoktur. Çalışan izleme (KVKK) gerekiyorsa kurulum başına "onay iste" ayarı.
3. **Yetki.** Öneri: MVP'de izleme her giriş yapmış kullanıcıya açık + `ScreenViewLog`; Faz 8'den önce en az
   bir "uzaktan kontrol" rolü zorunlu.
4. ~~**PC şablonu.**~~ **Kapandı (2026-09-29):** yeni tip `DeviceType.Pc`; mevcut "Bilgisayar" sistem şablonu bu tipe
   taşındı (Peripheral kullanılmaz). Admin ekranından başka PC şablonları da açılabilir.
5. ~~**Yazılım yedeğinin lisansı.**~~ **Kapandı (2026-09-29):** yazılım yedeği OpenH264 yerine VP9 (libvpx,
   telifsiz). H.264 yalnızca donanım kodlayıcılarında kullanılır; patenti sürücü/üreticiyle gelir.

---

## 18. Karar günlüğü

| Tarih | Karar |
|---|---|
| 2026-09-28 | Medya yolu: ekran → FFmpeg (`ddagrab`, H.264) → RTSP publish → MediaMTX → WHEP. Mevcut kamera akışı değişmez. |
| 2026-09-28 | .NET 10 (Scadex `net10.0`); kamerada publish olmadığı tespit edildi; `pc_*` için `IMediaPathAuthorizer`. |
| 2026-09-28 | Yayın bileti oturum boyu geçerli (FFmpeg yeniden bağlanınca MediaMTX tekrar sorar); okuma/yayın biletleri ayrı. |
| 2026-09-28 | Stop = kiralama bırakma; son izleyici gidince durur. Başlatmada yol hazır olana kadar sunucu bekler. |
| 2026-09-28 | `libx264` yok (GPL) → `h264_mf` yedek (2026-09-29 Faz 1 ile değişti, aşağıda); 4:2:0 ve B-frame yok zorunlu; klavyede `code` → scan code. |
| 2026-09-29 | Kimlik `Device.MacAddress`; ajan tablosu / `AgentId` / "ajan ekle" ekranı yok; modül şemasında cihaz kopyası yok. |
| 2026-09-29 | Kimlik doğrulama yalnızca MAC (kabul edilen risk, § 5.3); ayrı JWT şeması ve token ucu yok. |
| 2026-09-29 | PC istemcisi WPF (`Scadex.RemoteDesk.Windows`), servis/yardımcı/named pipe yok; minimal kaynak; "Agent" adı kullanılmaz. |
| 2026-09-29 | Çoklu monitör MVP'de: monitör başına yol ve yayın, izleyici monitör seçer. |
| 2026-09-29 | Kontratlar `Scadex.RemoteDesk.Contracts`; projeler `Modules/RemoteDesk/` altında; doküman depo kökünde. |
| 2026-09-29 | WPF istemcisi projenin kendi MVVM altyapısını (`Helpers/`) ve Generic Host DI'ını kullanır; CommunityToolkit.Mvvm yok. |
| 2026-09-29 | Uygulayıcı Claude Code (bu depo). |
| 2026-09-29 | **Tek FFmpeg build'i:** Scadex sunucusu (`MediaTools/ffmpeg`) ve istemci (`tools/ffmpeg`) aynı BtbN 8.1 LGPL exe'yi kullanır; GPL build kaldırıldı. Çekirdeğin anlık görüntü + klip komutları H.264 ve H.265 kaynakta bu build'le doğrulandı (JPEG 1920×1080, klip tam 5,0 sn, ilk paket anahtar kare). |
| 2026-09-29 | **Kodlayıcı seçimi:** donanımda H.264 (monitörün kartı önce: NVENC / QSV / AMF), yoksa yazılımda VP9; her aday gerçek denemeyle sınanır. OpenH264, `h264_mf`, x264 çıkarıldı. Yakalama `-init_hw_device d3d11va=cap:<adaptör>` + `-filter_complex ddagrab`. |
| 2026-09-29 | **PC kendi tipidir (`DeviceType.Pc = 13`), Peripheral değil** (kullanıcı kararı). "Bilgisayar" sistem şablonu Pc'ye taşındı, Id'si korunur. |
| 2026-09-29 | Faz 2: `IMediaPathAuthorizer` — `cam_` yolu modüle hiç sorulmaz; modül `pc_`'yi üstlenir (`read` → okuma bileti, `publish` → `AllowedNetworks` + yayın bileti). Açık oturum tekilliği DB'de filtreli unique index (`[Status] IN (1,2,3,4)`; SQL Server filtreli index'te `NOT IN` yok). Modül uçları `api/RemoteDesk/...`. |
| 2026-09-29 | Faz 3: `PcHub` anonim; `Hello` normalize MAC ile aktif Pc cihazlarını eşler, tek eşleşme kabul, cihaz başına ilk bağlanan kazanır; bağlantı kaydı bellekte. İstemci SignalR'ın otomatik yeniden bağlanmasını kullanmaz (her bağlantı `Hello` ile yeniden eşlenmeli); geri çekilme 1/2/5/10/30 sn, reddedilince 60 sn. Pencere kapatmak System Trayye indirir; `--tray` argümanı pencereyi açmadan başlatır (oturum açma görevi için). |
| 2026-09-29 | Faz 1: FFmpeg 8.1 LGPL; `h264_mf` bırakıldı → yazılım yedeği `libopenh264`; QSV `async_depth 1`; çıkış `-fps_mode passthrough` zorunlu; geri kalma bekçisi; tarayıcıda `jitterBufferTarget = 0` (yalnızca PC yayını). Ölçümler § 16.2. |

---

## 19. Uygulama kuralları

1. İstemci dinleyen port açmaz; MediaMTX adresi/parolası istemci config'inde yoktur.
2. PC kimliği yalnızca `Device.MacAddress`'tir; cihaz bilgisi modül tablolarına kopyalanmaz.
3. Video ve kontrol kanalları ayrıdır.
4. Komutlar `sessionId` ile idempotenttir; monitör başına tek yayın; bağlantılar yeniden bağlanır.
5. Bilet loglanmaz; FFmpeg stderr maskelenir; sunucuya yalnızca sabit hata metinleri gider.
6. H.264, 4:2:0, B-frame yok; RTSP TCP; çıkışta `-fps_mode passthrough`, QSV'de `-async_depth 1` (§ 7.4).
7. Mevcut kamera akışı (`cam_*`, `CameraService`, `MediaPathCleanupWorker`, `LiveRtspUrl`) değişmez.
8. Önce medya hattı kanıtlanır (Faz 1), sonra kod yazılır.
9. Boşta istemci iş yapmaz: zamanlayıcı, yoklama, gizli pencerede UI güncellemesi yok.
10. Fare/klavye MVP'de uygulanmaz; sözleşmesi (§ 12) Contracts'ta hazırdır.
11. Merkezde CLAUDE.md kuralları geçerlidir: modül kalıbı, `Result<T>`, ProblemDetails, enum sayı, TS aynası
    elle, modül uçlarında rate limit yok, `companyId`/`IgnoreQueryFilters` yok.
