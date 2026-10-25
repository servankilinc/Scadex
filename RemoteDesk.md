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
| Dosya transferi (gezme, indir, yükle) — PC'yi izleyen kullanıcı, ayrı izin | | ✅ |
| Panolar arası metin aktarımı (içe/dışa) — PC'yi izleyen kullanıcı | | ✅ |
| Ses, kayıt | ❌ | ✅ |
| Kapatılamayan istemci, oturum değişiminde kendiliğinden yeniden bağlanma (servis + Agent + WPF) | ✅ Faz 11a ([§ 9.8](#98-süreç-modeli--servis--agent--wpf-faz-11a), 2026-10-25) | |
| Oturum açılmadan bağlı olma | ❌ (kullanıcı kararı 2026-10-25 — Agent yalnızca kullanıcı oturumunda, [§ 9.1](#91-süreç-modeli-faz-11a-2026-10-25)) | |
| UAC onay penceresi ve kilit ekranında izleme + kontrol (güvenli masaüstü) | ✅ Faz 11b, dar (2026-10-25, [§ 12.6](#126-uipi-ve-güvenli-masaüstü)) | |
| Giriş ekranı (oturum açılmamış PC), Ctrl+Alt+Del | ❌ ([§ 9.7](#97-bilinen-sınırlar), kullanıcı kararları) | |

"Tüm monitörler tek görüntüde" ve otomatik güncelleme kapsamdan çıktı (kullanıcı kararı, 2026-10-06).

---

## 2. Kilit kararlar

| Konu | Karar | Neden |
|---|---|---|
| PC kimliği | **PC cihazının `DeviceId`'si** (Basic Auth kullanıcı adı; sırrın hash'i çekirdeğin `PcDevice.CredentialHash`'inde) | Merkezde cihaz zaten tanımlı; sır diyagramın özellik panelinde üretilir ve PC'ye girilir. Modülde ek tablo, ek kimlik, "ekle" ekranı yok. (2026-09-29; 2026-10-25'e kadar MAC) |
| Kimlik doğrulama | **HTTP Basic Auth — cihaz Id'si + sistemin ürettiği sır** | Kullanıcı kararı (2026-10-25; önceki "yalnızca MAC" kararının yerine). HTTPS zorunlu değil — kabul edilen risk: [§ 5.3](#53-kabul-edilen-risk-düz-http). |
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
- **Ayrı ajan kimliği / ajan tablosu / "ajan ekle" ekranı:** kimlik zaten cihazın kendisi (Id + sır); cihaz kopyası tablo istenmiyor.
- **WebSocket üzerinden JPEG kareleri:** 1080p'de 5–10 Mbps, tıkanıklık kontrolü sıfırdan yazılır.
- **PC ↔ tarayıcı doğrudan WebRTC:** Her PC'de WebRTC yığını + STUN/TURN; MediaMTX çoklu izleyiciyi zaten
  tek yayından besliyor.

---

## 3. Mimari

```text
 Saha PC'si (Windows 10/11, kullanıcı oturumu)            Scadex merkez sunucusu
┌───────────────────────────────────────┐              ┌───────────────────────────────────────────────┐
│ Scadex.RemoteDesk.Windows  (WPF, System Tray)│── WS/WSS ──▶│ /hubs/remote-desk/pc  (PcHub)                   │
│  · Basic Auth + monitörleri bildirir    │◀── komut ────│   Basic Auth (Id+sır) → Device(Pc), Hello        │
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

**Varsayım: PC'ler merkeze kapalı LAN/VPN üzerinden ulaşır** — HTTPS zorunlu olmadığı için ([§ 5.3](#53-kabul-edilen-risk-düz-http))
bununla birlikte anlamlıdır. PC'ler internetten bağlanacaksa bu doküman yeniden açılır: RTSPS
(`rtspEncryption: "optional"`, `:8322`; `optional` modunda Scadex'in kendi FFmpeg'inin okuduğu
`LiveRtspUrl` = `rtsp://127.0.0.1:8554` değişmez) ve bir istemci sırrı.

İstemciye verilen yayın adresi sunucu ayarıdır: `Modules:RemoteDesk:PublishRtspBaseUrl`
(ör. `rtsp://10.0.0.5:8554`). İstemcinin config'inde MediaMTX adresi, portu ya da parolası **yoktur**.

---

## 5. Kimlik

### 5.1 Basic Auth (2026-10-25 — MAC eşleşmesinin yerine)

PC, `PcHub`'a **HTTP Basic Auth** ile bağlanır (kullanıcı kararı; kontrol kartıyla aynı mekanizma, çekirdekte
`Scadex.Business/Utils/Auth/DeviceAuthentication.cs`):

- **Kullanıcı adı = diyagramdaki PC cihazının Id'si**, **parola = sistemin ürettiği sır** (32 küçük harf onaltılık, 128 bit).
  Sırrı tarayıcı üretir: kabin diyagramında PC'yi seçin → özellik paneli "Kimlik bilgisi → Üret" → kaydedin. Sunucu yalnızca
  SHA-256'sını saklar (`PcDevice.CredentialHash`); sır yalnızca üretildiği anda panelde görünür. Kaybolursa "Yenile" ile yenisi
  üretilir (eskisi kaydedilince geçersiz), "İptal et" PC'nin bağlanmasını keser.
- İstemci kimliği `appsettings.json > RemoteDesk:DeviceId` / `DeviceSecret`'ten okur ve `Authorization` başlığını negotiate'e de
  WebSocket isteğine de koyar (.NET SignalR istemcisi başlık taşıyabilir).
- Doğrulama negotiate'tedir: aktif cihaz + şablon tipi `Pc` + hash eşleşmesi (`IDeviceCredentialService`, tek PK sorgusu).
  Yanlış / eksik kimlik **401** — `Hello`'ya hiç gelinmez; istemci "Kimlik bilgisi reddedildi" gösterir ve 60 sn'de bir dener.
  Reddin nedeni istemciye söylenmez, sunucu günlüğüne cihaz Id'si + IP ile yazılır. Sır asla loglanmaz.
- Kullanıcı adı birincil anahtar olduğu için **tekillik yönetimi yoktur** (eski `IX_PcDevice_MacAddress`, normalizasyon,
  `Ambiguous` / `UnknownDevice` durumları kalktı).
- Açık bağlantı, sır yenilense ya da iptal edilse bile kopana kadar sürer; yeni kimlik bir sonraki bağlantıda geçerlidir.
- Diyagramda cihaz silinip yeniden çizilirse yeni `DeviceId` + yeni sır PC'ye yeniden girilmelidir.

| Sonuç | Sunucu | İstemci |
|---|---|---|
| Kimlik doğru | `Hello` bağlantıyı o `DeviceId`'ye kaydeder, cihaz/kabin adını döner | "Bağlı — {kabin} / {cihaz}" |
| Kimlik yanlış / yok / iptal | Negotiate **401** | "Kimlik bilgisi reddedildi"; 60 sn'de bir yeniden dener |
| Cihaz zaten bağlı | `AlreadyConnected` — **ilk bağlanan kazanır**, deneme loglanır | "Bu cihaz başka bir bağlantıda"; geri çekilerek yeniden dener |

Bu kural yalnızca **PC'deki istemci** içindir (PC başına tek istemci bağlantısı). **İzleyici sayısını sınırlamaz:** aynı PC'yi,
aynı ya da farklı monitörlerini istenen sayıda operatör aynı anda izler ([§ 8](#8-yayın-oturumu-ve-izleyiciler); kullanıcı kararı 2026-09-30).

### 5.2 Hub yetkisi

`PcHub` kullanıcı JWT'si istemez; `[Authorize(Policy = DeviceAuthPolicies.Pc)]` ile yalnızca PC kimliğini kabul eder (policy
`device_type` claim'i ister, kullanıcı token'ı geçmez). Bağlantı `Hello` kabul edilene kadar hiçbir şey yapamaz: kayıtsız
bağlantıdan gelen her çağrı reddedilir; bağlanıp **10 sn** içinde `Hello`'su kabul edilmeyen bağlantı kapatılır. Hub'ın metotları yalnızca
istemcinin kendi durumunu bildirmesine izin verir; hiçbir veri okumaz.

**Rate limit (2026-10-25, readiness P0.2):** negotiate ve bağlantı istekleri IP başına dakikada 60 ile sınırlıdır
(`RemoteDeskModule.PcHubRateLimitPolicy`, kayan pencere, kuyruk yok, aşımda 429). Bir deneme 2 istektir; Agent'ın geri çekilmesi bunun
çok altında kalır, NAT arkasında ~30 PC aynı anda yeniden bağlanabilir. Politika modülün kendi kaydındadır. Modülün HTTP uçları
(PC'nin `push`/`pull`'u dahil, cihaz Id'si başına) 2026-10-26'dan beri çekirdeğin `Default` politikasındadır; `ViewerHub` bilerek
dışarıdadır. Çekirdekte `UseRateLimiter` yetkilendirmeden önce çalışır — kimliksiz istek seli de sayılır ve Basic Auth doğrulamasına
(veritabanı) ulaşmadan 429 alır.

Yine de medya yolu korunur: yayın **bileti** olmadan `pc_` yoluna publish edilemez ([§ 7.3](#73-biletler)).
Bilet yalnızca `Hello`'su kabul edilmiş bağlantıya, yalnızca bir izleme başlatıldığında verilir.

### 5.3 Kabul edilen risk: düz HTTP

**HTTPS zorunlu değildir** (kullanıcı kararı 2026-10-25). Düz HTTP'de sır her istekte base64 ile açık gider; ağı dinleyen biri onu
alıp gerçek PC bağlı değilken onun yerine bağlanabilir (sahte ekran, operatörün yazdığı tuşları toplama). Hafifletmeler: ilk bağlanan
kazanır; reddedilen denemeler IP'siyle loglanır; isteğe bağlı `Modules:RemoteDesk:AllowedNetworks` (CIDR listesi, boş = herkes) ile
hub ve publish yalnızca belirli ağlardan kabul edilir; sır panelden yenilenerek eskisi geçersiz kılınır. 2026-09-29 – 2026-10-25 arasında
kimlik yalnızca MAC'ti (sır değil, taklit edilebilirdi) — bu risk Basic Auth ile kapandı; HTTPS'e geçiş ayrı bir karardır.

---

## 6. Kontrol düzlemi — `PcHub`

### 6.1 Bağlantı ömrü

```text
açılış → appsettings.json oku → /hubs/remote-desk/pc bağlan → Hello → Accepted ise komut bekle
kopunca: 1 → 2 → 5 → 10 → 30 sn (üst sınır 30 sn, sonsuz)
```

- **Ayrı bir heartbeat yok.** Canlılık SignalR'ın `KeepAliveInterval` (15 sn) / `ClientTimeoutInterval`
  (30 sn) mekanizmasıdır. İstemci yalnızca **bir şey değişince** bildirir (yayın durumu, monitör listesi).
- Sunucu bağlantıları **bellekte** tutar: `DeviceId → { connectionId, sürüm, monitörler, yayınlar }`.
  Tek sunucu varsayımı; yatay ölçekleme SignalR backplane ister — kapsam dışı.
- Aynı PC'de birden fazla Windows oturumu varsa (hızlı kullanıcı değiştirme) **yalnızca etkin konsol
  oturumundaki** istemci bağlanır; oturum değişince (`SystemEvents.SessionSwitch`) bırakır/devralır.

### 6.2 Sözleşme (`Scadex.RemoteDesk.Contracts`)

Kodda: `Contracts/Hub/PcHubContract.cs` (hub yolu, metot adları, `IPcHubClient`, kayıtlar). Aşağıdaki tablo özettir; alan adları
koddakidir. Kimlik `Hello`'da değil bağlantının `Authorization` başlığındadır (§ 5.1).

İstemci → sunucu:

| Metot | Gövde → Dönüş |
|---|---|
| `Hello` | `{ clientVersion, osVersion, machineName, userName, monitors[] }` → `{ status, deviceId?, deviceName?, cabinetName? }` (`status`: Accepted 1, AlreadyConnected 4, NetworkNotAllowed 5; 2 ve 3 MAC eşleşmesiyle kalktı — yeniden kullanılmaz) |
| `ReportMonitors` | `{ monitors[] }` — ekran eklenince/çıkınca/çözünürlük değişince |
| `ReportStreamState` | `{ sessionId, monitorIndex, state, encoder?, failureReason? }` — yalnızca durum değişince; PC'de elle durdurulan yayın (`Stopped`) merkezde de biter (`StoppedOnPc`) |
| `ReportControlInput` *(2026-10-25)* | `{ controlSessionId, state }` — `ControlInputState` (Ok 1, SecureDesktop 2, SecureDesktopUnavailable 3, Rejected 4, MonitorMissing 5); yalnızca değişince, kontrol `Ok` varsayımıyla başlar ([§ 12.6](#126-uipi-ve-güvenli-masaüstü)) |

`monitors[]` öğesi: `MonitorInfo` — `{ index, adapterIndex, outputIndex, deviceName, left, top, width, height, isPrimary,
gpuVendor, gpuName }`, piksel cinsinden (fiziksel). Windows istemcisi yakalamayı `(adapterIndex, outputIndex)` çiftiyle yapar (§ 7.4).

**Sözleşme işletim sisteminden bağımsızdır (2026-10-25, kullanıcı hedefi: ileride Linux/Android istemcileri).** Bugün tek istemci
Windows'tur; yeni bir platformun istemcisi aynı hub'a **sunucu değişmeden** bağlanabilmelidir. Bu yüzden:
- Telde yalnızca platformdan bağımsız kavramlar taşınır: DOM tuş kodu (`KeyboardCodes`, § 12.4), [0,1] koordinat, tekerlek protokol birimi
  (120 = bir çentik), RTSP yayın adresi, **opak** dosya yolu metni.
- Platforma özgü alanlar yalnızca bilgidir, sunucu **yorumlamaz**: `MonitorInfo`'nun `adapterIndex`/`outputIndex`/`deviceName`/`gpuVendor`/
  `gpuName`'i (başka platform 0/boş gönderir), `EncoderKind`/`GpuVendor` (yeni platform yeni değer ekler), `osVersion`. Windows'a özgü tablolar
  ve yerel düzen istemcidedir: `WindowsScanCodes` (code → set-1 scan code), `Contracts/Local/AgentPipeContract` (Agent ↔ arayüz pipe'ı).
- **Sunucu PC yolunu kendi OS kuralıyla işlemez** (`Path.GetFileName`/`Combine`/`GetInvalidFileNameChars` sunucunun OS'una göredir):
  ad çıkarma, hedef birleştirme ve güvenli ad `FileTransfer/PcPathText`'tedir (hem `\` hem `/` ayırıcı; birleştirmede klasörün kendi
  ayırıcısı; yasak karakter kümesi sabit). Yazma politikası PC'nindir.
- Bilinçli olarak YOK: platform/yetenek bildirimi (`Hello`'da platform türü, "klavye/dosya/pano destekleniyor mu"). Tek istemci Windows iken
  kullanılmayan yüzey olurdu; ikinci platform gelince eklenir (kullanıcı kararı 2026-10-25).

Sunucu → istemci (`IPcHubClient`):

| Metot | Gövde |
|---|---|
| `StartScreenStream` | `{ sessionId, monitorIndex, publishUrl, profile? }` — bilet `publishUrl`'nin parola alanındadır; `profile` = `VideoProfile { fps, maxWidth, bitrateKbps }`, `null` = seçilen kodlayıcının varsayılanı, GOP her zaman 2 sn |
| `StopScreenStream` | `{ sessionId }` |
| `ControlStarted` *(Faz 8)* | `{ controlSessionId, userName }` — gösterge açılır, bu oturumun girdisi kabul edilir |
| `ControlEnded` *(Faz 8)* | `{ controlSessionId }` — basılı düğmeler bırakılır |
| `Input` *(Faz 8)* | `{ controlSessionId, monitorIndex, events: [...] }` ([§ 12](#12-uzaktan-kontrol-faz-8)) |

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
- **PC tercihi (2026-09-30):** istemcinin "Encoder" kutucuğunda (monitörlerden ayrı; sınama tüm monitörler için yapılır, liste
  birleşiktir) "Tercih et" ile bir aday (ör. VP9) seçilir. Tercih **PC başınadır** ve
  **bellekte** durur: uygulama kapanınca otomatik seçime döner, dosyaya yazılmaz. Bu PC'deki tüm yayınlar
  onu kullanır. Tercih edilen aday bir monitörde çalışmıyorsa o monitör otomatik seçime düşer, özette bu yazar. Tercih
  değişince çalışan yayınlar aynı hedefle yeniden başlar; arada merkeze "durdu" gönderilmez, oturum sürer. "Otomatik
  seçime dön" tercihi kaldırır.

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
  deneme karşılar. Kilit ekranı süresince görüntü yoktur ([§ 9.7](#97-bilinen-sınırlar)).
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
  3. Yol hazır olana kadar bekle (en fazla ~30 sn — ilk yayında istemci kodlayıcıları sınar): IMediaGateway ile MediaMTX'te yol "ready"
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

## 9. PC tarafı — servis + Agent + arayüz

### 9.1 Süreç modeli (Faz 11a, 2026-10-25)

Üç süreç, iki proje (karar 2026-10-06, uygulama 2026-10-25 — gerekçe ve tablo [§ 9.8](#98-süreç-modeli--servis--agent--wpf-faz-11a)):

| Süreç | Proje / exe | Nerede, kim | İşi |
|---|---|---|---|
| **Servis** | `Scadex.RemoteDesk.Windows.Service.exe` (argümansız) | Session 0, LocalSystem; `ScadexRemoteDesk`, otomatik başlar | Yalnızca gözetmen: doğru oturumda tek Agent çalıştırır. Merkezle konuşmaz. |
| **Agent** | aynı exe, `--session` | Kullanıcının oturumu (`winsta0\default`), **SYSTEM** | Merkez hub'ı (Basic Auth), ekran yakalama + FFmpeg, `SendInput`, dosya, pano, monitör izleme, encoder sınaması |
| **Arayüz** | `Scadex.RemoteDesk.Windows.exe` (WPF) | Kullanıcının oturumu, **normal yetki**; `HKLM\…\Run` `--tray` | Agent'ın durumunu gösterir (izleniyor şeridi + tepsi ikonu), üç zararsız komut gönderir |

- **Hedef oturum (kullanıcı kararı 2026-10-25):** aktif ve kullanıcısı olan oturum — önce fiziksel konsol, yoksa en küçük Id'li aktif RDP
  oturumu. Hiçbiri yoksa **Agent çalışmaz**: kimse oturum açmamışken (giriş ekranı) ya da RDP bağlantısı kesilmiş tek oturumda PC merkezde
  "bağlı değil" görünür. Kilitli oturum hâlâ aktiftir: Agent kalır, yayın "ekran kilitli" olur.
- **Servis Agent'ı yeniden başlatır:** Agent'ın oturumunda oturum açılıp kapanınca, hedef oturum değişince (hızlı kullanıcı değiştirme,
  konsol ↔ RDP) ve Agent beklenmedik bitince (geri çekilme 1/2/5/10/30 sn; 60 sn yaşadıysa sıfırlanır). Oturum olayları SCM'den gelir
  (`SessionAwareServiceLifetime`); 15 sn'de bir ucuz WTS uzlaştırması kaçan olayları yakalar. Aynı oturumda RDP bağlan/kes ve kilit
  Agent'ı yeniden başlatmaz — monitör listesini Agent izler.
- **Başlatma:** servis kendi SYSTEM token'ını çoğaltır, oturum Id'sini hedef oturuma çevirir (SeTcbPrivilege — yalnızca LocalSystem'de),
  `CreateProcessAsUser` ile askıda başlatır, Job Object'e (`KILL_ON_JOB_CLOSE`) koyar, durdurma olayını
  (`Global\Scadex.RemoteDesk.Agent.{pid}.Stop`, varsayılan DACL: SYSTEM + Administrators) kurar ve sürdürür. Servis süreci nasıl biterse
  bitsin Agent ve onun FFmpeg'leri (iç içe job) kalmaz.
- **Durdurma:** servis olayı işaretler → Agent yayınları nazikçe durdurur (FFmpeg'e `q`), hub'ı kapatır, çıkar; 8 sn'de çıkmazsa sonlandırılır.
- **Kullanıcı Agent'ı kapatamaz** (SYSTEM süreci; yönetici kapatırsa servis yeniden başlatır). Arayüzün tepsi menüsündeki "Arayüzü kapat"
  yalnızca arayüzü kapatır, izleme sürer.
- **Elle başlatılan Agent çıkar** (SYSTEM değilse, çıkış kodu 2). Geliştirmede `DOTNET_ENVIRONMENT=Development` ile normal kullanıcıyla
  çalışır (launchSettings: "Agent (geliştirme, normal kullanıcı)").
- **Monitörler kendiliğinden güncellenir (2026-10-01):** Agent `SystemEvents.DisplaySettingsChanged` / `SessionSwitch` (RDP bağlan/kes,
  konsol geçişi, kilit açılması) ve 12 sn'lik uzlaştırmayla monitör listesini merkeze yalnızca değişince bildirir.
- **Tek arayüz örneği** oturum başınadır (`Local\Scadex.RemoteDesk.Windows` mutex'i); ikinci örnek mevcut pencereyi öne getirip çıkar.
  Pencere kapatılınca arayüz System Tray'e iner.
- Agent **Per-Monitor DPI Aware v2**'dir (`app.manifest`): monitör dikdörtgenleri ve fare koordinatları fiziksel pikseldir.

### 9.2 Arayüz ve pipe

System Tray ikonu (WinForms `NotifyIcon`) + tek pencere; bütün veri Agent'ın pipe'tan gönderdiği özettir (`AgentSnapshot`):

| Alan | İçerik |
|---|---|
| Bağlantı | Bağlı / Bağlanıyor / Kimlik bilgisi reddedildi / Başka bağlantıda / Ağ izinli değil / Yapılandırma eksik — son hata. Agent yoksa "Servis bağlantısı yok" |
| Merkez | Sunucu adresi, eşleşilen kabin ve cihaz adı |
| Bu PC | Makine adı, cihaz kimliği (`DeviceId`, seçilebilir; sır gösterilmez), Agent sürümü ve oturum kullanıcısı |
| Monitörler | Liste; yayının salt okunur durumu (encoder/zincir/profil, canlı fps/hız/CPU) |
| Encoder | Sınama sonuçları, seçilen encoder, "Tercih et" / "Otomatik seçime dön" |
| İzleniyor | Aktif yayın, uzaktan kontrol ya da dosya aktarımı varken **System Tray ikonu değişir** ve pencerede kırmızı şerit çıkar |

System Tray menüsü: Göster · Yeniden bağlan · Günlük klasörünü aç (`%ProgramData%`, servis + Agent) · Arayüzü kapat (izleme sürer).

**Pipe (`Contracts/Local/AgentPipeContract.cs`):** `Scadex.RemoteDesk.Agent.{oturumId}`, satır başına bir UTF-8 JSON, protokol sürümü 1.
- ACL: SYSTEM tam + **o oturumun logon SID'i** okuma/yazma; başka oturumdaki aynı kullanıcı da açamaz. Agent her bağlantıda istemcinin
  oturumunu doğrular, adı `FirstPipeInstance` ile alır (ad başkasındaysa açmaz ve hata loglar).
- Arayüz bağlanmadan önce **sunucuyu doğrular**: aynı oturumda olmalı ve süreç yolu kurulumdaki `..\Service\Scadex.RemoteDesk.Windows.Service.exe`
  olmalı (geliştirme düzeninde yalnızca exe adı). Değilse bağlanmaz, "Agent doğrulanamadı" gösterir — sahte bir "izlenmiyor" göstergesine karşı.
- Arayüz → Agent **yalnızca** `subscribe` + `reconnect` / `probeEncoders` / `setPreferredEncoder`. Girdi, yayın ve dosya komutu yoktur;
  bilinmeyen tip, bozuk JSON ya da 64 KB'yi aşan satır bağlantıyı kapatır. Sürüm farkında Agent `unsupportedVersion` gönderir.
- Agent → arayüz: `snapshot` (değişince, en çok 500 ms'de bir birleştirilerek). **Arayüz bağlı değilken özet hiç kurulmaz** (§ 9.3).

### 9.3 Minimal kaynak kullanımı

- **Boştayken (Agent):** SignalR WebSocket'i (15 sn keep-alive) + 12 sn'de bir monitör uzlaştırması (ucuz DXGI enumerasyonu, liste
  değişmedikçe ağ trafiği yok). Arayüz bağlı değilken pipe özeti kurulmaz. **Servis:** olay bekler + 15 sn'de bir WTS sorgusu.
- **Arayüz** yalnızca Agent'ın özetiyle güncellenir; gizli pencere çizilmediği için maliyeti yoktur. Zamanlayıcıyla yenilenen kontrol yok.
- Kodlayıcı denemesi ilk yayında (§ 7.4) ya da arayüzden istenince; FFmpeg yalnızca izlenen monitör için, `BelowNormal` öncelikte.
- Hedef: Agent boşta **< 100 MB** bellek, **≈ %0 CPU** (Faz 7 ölçümü tek WPF süreciyle yapıldı — [§ 16.3](#163-faz-7-ölçümleri-2026-09-30);
  Faz 11a sonrası yeniden ölçülmedi).

### 9.4 Yapılandırma

Servis klasöründeki `appsettings.json` (Agent okur; içerik kökü **`AppContext.BaseDirectory`** — servis System32'de başlar):

```json
{ "RemoteDesk": { "CentralApiUrl": "http://10.0.0.5:5208", "DeviceId": "<diyagramdaki PC cihazının Id'si>", "DeviceSecret": "<panelde üretilen sır>" } }
```

`DeviceId` + `DeviceSecret` PC'nin Basic Auth kimliğidir (§ 5.1); biri boşsa Agent "Yapılandırma eksik" der ve bağlanmaz. Dosya cihaz
sırrını taşır ve varsayılan izinlerle standart kullanıcılarca okunabilir — kısmak elle yapılır (§ 9.6); arayüzün yapılandırması yoktur. Video profili ve yayın adresi sunucudan gelir. FFmpeg yolu servis klasörüne göreli sabittir (`tools\ffmpeg\ffmpeg.exe`).

### 9.5 Loglama

Serilog. **Servis ve Agent:** `%ProgramData%\Scadex\RemoteDesk\logs\{service,agent}-YYYYMMDD.log` (SYSTEM'in `LocalAppData`'sı systemprofile
olurdu; satırda süreç Id'si; günlük döner, 14 dosya, dosya başına 10 MB; `%ProgramData%`'nın varsayılan izinleri). **Arayüz:**
`%LocalAppData%\Scadex\RemoteDesk\logs\ui-*.log` (yalnızca arayüz olayları). Seviye Information, `Microsoft`/`System` Warning.

Servis olayları: açılış, oturum olayları, Agent başlatıldı/durdu/beklenmedik bitti (çıkış kodu, geri çekilme). Agent olayları: açılış
(sürüm, oturum, kullanıcı, **kimlik SYSTEM/geliştirme**), merkeze bağlandı/kapandı/kimlik reddedildi (401), merkez yayın istedi/durdurdu,
encoder sınamaları, yayın başladı (encoder, profil, maskeli adres), FFmpeg çıktısı, monitör listesi değişti, arayüzün komutları, dosya
transferi (reddedilen yazmalar nedenle). **Bilet ve cihaz sırrı asla loglanmaz**; FFmpeg adresi `rtsp://***@` ile maskelenir; pano içeriği
loglanmaz.

### 9.6 Kurulum

**Kurulum paketi ve kurulum betiği yok (kullanıcı kararları 2026-09-30, 2026-10-25):** iki projenin yayını alınır, client'a kopyalanır,
servis bir kez `sc create` ile tanıtılır. (Bir süre `Deploy/Publish-Client.ps1` + `Install-Service.ps1` vardı; süreci karmaşıklaştırdığı için
kaldırıldı.)

1. **Yayın (geliştirme makinesi)** — ikisi de self-contained win-x64 (.NET gömülü, client'ta kurulum gerekmez; toplam ≈ 420 MB):
   ```
   dotnet publish Modules\RemoteDesk\Scadex.RemoteDesk.Windows.Service -c Release -r win-x64 --self-contained true -o <paket>\Service
   dotnet publish Modules\RemoteDesk\Scadex.RemoteDesk.Windows -c Release -r win-x64 --self-contained true -o <paket>\Tray
   ```
   **İki ayrı klasör** bilerek: iki self-contained yayın aynı klasöre konursa ortak paket DLL'leri birbirinin üstüne yazılırdı.
   `tools\ffmpeg\ffmpeg.exe` ya da LGPL `LICENSE.txt` geliştirme ortamında yoksa servis yayını bilerek hata verir (`RequireFfmpegForPublish`).
2. **Client'a kopyala:** `Service\` ve `Tray\` **yan yana**, ör. `C:\Program Files\Scadex\RemoteDesk\{Service,Tray}`. Arayüz Agent'ı
   `..\Service\Scadex.RemoteDesk.Windows.Service.exe` yolundan doğrular (§ 9.2); yan yana değilse yalnızca exe adıyla doğrular.
3. **Ayar:** `Service\appsettings.json` → `CentralApiUrl`, `DeviceId`, `DeviceSecret` (§ 9.4).
4. **Servis (yönetici komut istemi, bir kez):**
   ```
   sc create ScadexRemoteDesk binPath= "C:\Program Files\Scadex\RemoteDesk\Service\Scadex.RemoteDesk.Windows.Service.exe" start= auto DisplayName= "Scadex RemoteDesk"
   sc failure ScadexRemoteDesk reset= 86400 actions= restart/5000/restart/10000/restart/30000
   sc start ScadexRemoteDesk
   ```
   Servis LocalSystem'dir (varsayılan) ve kullanıcı oturumunda Agent'ı birkaç saniyede başlatır. `sc failure` isteğe bağlıdır: servis sürecinin
   kendisi çökerse Windows yeniden başlatsın diye (Agent'ın çökmesini servis zaten kendisi toparlar). Servis exe'si elle çalıştırılmaz —
   Agent yalnızca LocalSystem servisten SYSTEM olarak başlayabilir.
5. **Arayüz (isteğe bağlı, yalnızca gösterge):** `Tray\Scadex.RemoteDesk.Windows.exe --tray` kısayolu ortak Başlangıç klasörüne
   (`shell:common startup`). İzleme ve kontrol arayüz olmadan da çalışır.

- **Güncelleme:** `sc stop ScadexRemoteDesk` → klasörleri yenisiyle değiştir (`appsettings.json`'ı koru) → `sc start ScadexRemoteDesk`.
  **Kaldırma:** `sc stop ScadexRemoteDesk` + `sc delete ScadexRemoteDesk`, klasörü ve kısayolu sil. Eski sürümden (Görev Zamanlayıcı ile
  yükseltilmiş WPF) geçen PC'de "Scadex RemoteDesk Client" görevi ve eski kısayol elle kaldırılır — yoksa eski istemci hub'ı tutar, yeni Agent
  "başka bağlantıda" kalır.
- **Kabul edilen eksik (kullanıcı kararı):** `appsettings.json` ve günlük klasörü varsayılan izinlerle kalır — `Program Files` altında standart
  kullanıcılar cihaz sırrını **okuyabilir**. İstenirse elle kısılır:
  `icacls "…\Service\appsettings.json" /inheritance:r /grant:r *S-1-5-18:F *S-1-5-32-544:F`.
- Binary'ler kod imzalı olmalı (uzaktan erişim yazılımları AV'lerce sık işaretlenir; Agent SYSTEM + ekran + girdi) — sertifika gelince.
- Otomatik güncelleme **yapılmayacak** (kullanıcı kararı, 2026-10-06).
- Temiz (geliştirme araçsız) bir PC'de kurulum denemesi Faz 11a'nın açık kalan maddesidir (§ 14).

### 9.7 Bilinen sınırlar

UAC onay penceresi ve kilit ekranı (güvenli masaüstü, `Winlogon`) **2026-10-25'ten beri desteklenir** (Faz 11b, dar — § 12.6). Hâlâ
**yapılamayanlar:** oturum açılmamış PC (giriş ekranı — Agent yalnızca kullanıcı oturumunda çalışır, kullanıcı kararı) ve Ctrl+Alt+Del
(kullanıcı kararı; gerekirse `SendSAS` + `SoftwareSASGeneration` ilkesiyle eklenir). RDP bağlantısı kesilmiş tek oturumda da Agent durur
(yakalanacak masaüstü yok). Geliştirme modunda (normal kullanıcı) güvenli masaüstü açılamaz; davranış "ekran kilitli"dir.

### 9.8 Süreç modeli — servis + Agent + WPF (Faz 11a)

> **Durum (2026-10-25): uygulandı** (kullanıcı kararı 2026-10-06). Görev Zamanlayıcı yolu ve `Install-StartupTask.ps1` kaldırıldı.
> Oturum açılmamış PC'de bağlanma kapsamdan çıktı (kullanıcı kararı 2026-10-25 — Agent yalnızca kullanıcı oturumunda).

**Neden (kullanıcı önerisi):** logout/login'de kendiliğinden yeniden bağlanmak; çökünce yeniden başlamak; kullanıcının istemciyi
kapatamaması; kurulumun kullanıcıdan bağımsız olması (`-ForUser` yok); yönetici pencerelerine görev/UAC olmadan girdi; Görev
Zamanlayıcı'nın kalkması.

**Temel kısıt:** Windows servisi Session 0'da çalışır, kullanıcı masaüstünü göremez ve girdi gönderemez. Bu yüzden ekran ve girdi
işini servis değil, servisin kullanıcı oturumunda başlattığı ayrı bir süreç (**Agent**) yapar. WPF kullanıcı yetkisinde kalır:
SYSTEM yetkili bir pencere (ör. dosya diyaloğu) yerel yetki yükseltme açığı olurdu.

| | **Servis** | **Agent** | **WPF arayüz** |
|---|---|---|---|
| Uygulama türü | Windows Service (.NET Worker) | Penceresiz süreç (Generic Host) | WPF + System Tray |
| Kim başlatır | Windows, açılışta otomatik | Servis | Oturum açılışında `HKLM\…\Run` |
| Nerede / kimlik | Session 0, LocalSystem | Kullanıcının oturumu, SYSTEM | Kullanıcının oturumu, normal yetki |
| Merkez hub bağlantısı | — | **Var** (`PcHub`, Basic Auth, Hello) | — |
| Ekran yakalama (ddagrab/FFmpeg) | — | **Var** | — |
| Fare/klavye (`SendInput`) | — | **Var** (yükseltilmiş pencereler dahil) | — |
| Dosya transferi, pano | — | **Var** (yol politikası § 12.8) | — |
| Monitör listesi, encoder sınaması | — | **Var** | Agent'tan gösterir |
| Oturum değişimi (login/logout/RDP) | **İzler**, Agent'ı yeni oturumda kurar | Kapanır, yenisi başlar | Yeni oturumda yeniden açılır |
| Çökme kurtarma | SCM kurtarma ayarı | Servis yeniden başlatır (geri çekilmeli) | Gerekmez |
| Kapatma | Yalnızca yönetici durdurur | Kullanıcı kapatamaz | "Arayüzü kapat" yalnızca arayüzü kapatır |
| "İzleniyor / kontrol ediliyor" | — | Durumu üretir | **Gösterir** (şerit + tepsi ikonu) |
| Kullanıcıdan komut | — | Pipe'tan yalnızca Yeniden bağlan / Encoder sına / Tercih et | Bu üçünü gönderir |
| Günlük | `%ProgramData%\…\logs\service-*` | `%ProgramData%\…\logs\agent-*` | `%LocalAppData%\…\ui-*` |
| Kod | `Supervisor/`, `Native/` | `Agent/`, `Services/` (eski WPF `Services/`) | `Views/`, `ViewModels/`, `Services/Agent`, `Services/Shell` |

**SYSTEM kimliği yüzünden değişenler:** günlük `%ProgramData%` altında; Hello'daki kullanıcı adı oturumdaki kullanıcıdan
(`WTSQuerySessionInformation`); dosya transferinin reddettiği Startup klasörü oturumdaki kullanıcınınki (`SHGetKnownFolderPath` + kullanıcı
token'ı); pano WPF dispatcher yerine Agent'ın kendi STA iş parçacığında; ağ/aygıt yolları reddedilir (SYSTEM ağa makine hesabıyla çıkar).

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
| `RemoteControlSession` *(Faz 8)* | `Id`, `DeviceId`, `UserId`, `StartedUtc`, `EndedUtc`, `EndReason`, `InputEventCount` | Kim, hangi PC'yi, ne kadar kontrol etti; girdinin kendisi kaydedilmez |

`DeviceId`/`UserId` FK değildir (modül kuralı). PC'nin adı, kabini ve kimlik durumu her zaman çekirdekteki
`Device` / `PcDevice`'tan okunur; bağlantı durumu, istemci sürümü ve monitör listesi **bellektedir** — hiçbiri tabloya
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

**Karar (2026-09-30): istemci bağlantısı kabin durumuna YANSIMAZ.** Bağlı olup olmadığı yalnızca bellekte
(`PcConnectionRegistry`) tutulur ve PC ekranlarında gösterilir; `Device.DeviceStatusId` / `LastSeen`'e yazılmaz,
`ICabinetStatusService`'e kanıt olarak verilmez. Gerekçe: kullanıcı uygulamayı kapatınca ya da PC kapalıyken kabin
`Warning`'e düşerdi (Signalization'ın alarmı gibi bilerek ayrı). PC cihazında çekirdeğin TCP yoklaması açılırsa
(`IsMonitoringEnabled`) o çekirdek kuralıyla ayrıca işler — bu modülün kararı değildir.

### 10.5 Uç noktalar (kullanıcı JWT'si)

İzleme uçları `RemotePcView`, rapor uçları `RemotePcAudit` iznini ister; izin yoksa 403 ([§ 11](#11-güvenlik-özeti)).

| Uç | İş |
|---|---|
| `GET /api/RemoteDesk/pcs` | Aktif `DeviceType.Pc` cihazları: ad, kabin, kimlik bilgisi var mı (`hasCredential`), bağlı mı, istemci sürümü, monitör sayısı, izleyen sayısı |
| `GET /api/RemoteDesk/pcs/{deviceId}` | Aynısı + monitör listesi ve her monitörün yayın durumu |
| `POST /api/RemoteDesk/pcs/{deviceId}/monitors/{index}/view` | İzlemeyi başlat ([§ 8.2](#82-başlatma)) |
| `POST /api/RemoteDesk/views/{viewId}/renew` · `DELETE /api/RemoteDesk/views/{viewId}` | Kiralama |
| `GET /api/RemoteDesk/reports/filters` | *(Audit)* Üç denetim tablosunda geçen PC'ler (kabin kimliği ve adıyla) ve kullanıcılar (silinmiş/pasif dahil) |
| `POST /api/RemoteDesk/reports/views` · `…/controls` · `…/transfers` | *(Audit)* `ScreenViewLog` / `RemoteControlSession` / `FileTransferLog`, sayfalı, yeniden eskiye; gövde `{ deviceId?, cabinetId?, userId?, fromUtc?, toUtc?, page, pageSize ≤ 100 }`, tarih aralığı kaydın başlangıcına. `cabinetId` PC'nin BUGÜNKÜ kabinidir (satır kabini taşımaz; kabindeki cihazlar çekirdekten okunur — taşınan PC'nin geçmişi yeni kabinde görünür, 2026-10-12) |
| `POST /api/RemoteDesk/reports/summary` | *(Audit)* Aynı filtreyle oturum sayıları, toplam süreler, en çok izlenen PC, en çok izleyen kullanıcı |

Rapor fare/klavye girdisi göstermez: girdi zaten saklanmıyor, `RemoteControlSession.InputEventCount` da raporda bilerek yok
(kullanıcı kararı, 2026-10-05). Adlar çekirdekten projeksiyonla çözülür; çekirdekte artık olmayan kimlik `null` ad ile listelenir.
Dışa aktarma (2026-10-12) sunucuda ayrı uç DEĞİLDİR: ekran etkin sekmenin listesini aynı uçtan 100'lük sayfalarla sonuna kadar çekip
tarayıcıda CSV yazar (`lib/csv-export.ts`, 10.000 satır tavanı — aşılırsa uyarı, filtre daraltılır).

Kurallar: `Result<T>` → çıplak DTO, hatalar ProblemDetails; controller'lar modülün temel sınıfından türer
(`SignalizationControllerBase` kalıbı); taban çekirdeğin `Default` rate limit'ini taşır (2026-10-26; hub'lar hariç); hiçbir metot `companyId` almaz;
C# DTO değişince TS aynası elle güncellenir.

### 10.6 Frontend (`src/modules/remotedesk/`)

- **PC listesi** (modül menüsü): kamera listesi ekranı kalıbında; bağlantı durumu rozeti, kabin, kimlik durumu
  ("tanımlı değil" uyarısı), monitör sayısı, izleyen sayısı. Durum `refetchInterval` ile 10 sn'de bir
  yenilenir (MVP'de ayrı bir tarayıcı hub'ı yok).
- **PC ekranı:** monitör seçici (her monitör: numara, çözünürlük, birincil işareti) + oynatıcı. Mevcut
  `src/lib/camera/whep.ts` + `stream-session.ts` yeniden kullanılır; yalnızca bilet kaynağı ve kiralama
  yenileme hook'u farklıdır. Monitör değiştirmek eski kiralamayı bırakıp yenisini açar.
- **PC yayınında alıcıya `receiver.jitterBufferTarget = 0`** verilir (Chrome): Faz 1'de jitter buffer'ı
  106 ms'den 55 ms'ye indirdi. Bu, `whep.ts`'e isteğe bağlı bir seçenek olarak eklenir; **kamera akışının
  varsayılanı değişmez** (kamera ağında titreme daha fazla, tampon orada işe yarıyor).
- **İzleme Raporu** (`/remote-desk/reports`, ayrı menü maddesi, `RemotePcAudit`): PC / kullanıcı / tarih filtresi (URL'de),
  dört özet kartı, "İzleme kayıtları" ve "Kontrol kayıtları" sekmeleri (ortak `DataTable` + `DataTablePager`). PC ekranındaki
  "Kayıtlar" düğmesi raporu `?deviceId=` ile süzülü açar. İzin rotada çocuk başınadır: yalnızca rapor izni olan kullanıcı PC listesine düşmez.
- Sorgu anahtarları `['remoteDesk', …]`; sunucu damgaları için `toUtcDate` / `formatUtcDateTime`.

---

## 11. Güvenlik özeti

| Tehdit | Önlem |
|---|---|
| Sahte PC istemcisi | Basic Auth sırrı gerekir (2026-10-25); düz HTTP'de sırrın dinlenmesi **kabul edilmiş risk** ([§ 5.3](#53-kabul-edilen-risk-düz-http)); ilk bağlanan kazanır, loglama, `AllowedNetworks` |
| Hub üzerinden veri sızdırma | `PcHub` hiçbir veri döndürmez; `Hello` öncesi her çağrı reddedilir |
| Biletsiz / başka yola / kamera yoluna yayın | Yayın bileti yol + oturuma bağlı; `cam_*` publish her zaman 401 |
| Okuma biletiyle yayın ya da tersi | Ayrı cache anahtar uzayları |
| Bilet sızıntısı | Loglarda maske; ağ LAN/VPN varsayımı. Dosya transferi bileti PC kimliğine bağlı, 2 dk, tek kullanımlık (§ 12.8) |
| Bağlantı seli | `PcHub` IP başına dakikada 60 istek (429), Hello'suz bağlantı 10 sn'de kapanır (§ 5.2) |
| Uzaktan UAC onayı / kilit açma | **Bilinçli izinli** (kullanıcı kararı 2026-10-25): `RemotePcControl` izni + canlı izleme şartı; Agent zaten SYSTEM. Her kontrol `RemoteControlSession` denetimidir (§ 12.6) |
| İzlemenin fark edilmemesi | `ScreenViewLog`; PC'de System Tray ve pencere göstergesi |
| Dosya ile veri sızdırma / sistem bozma | Ayrı izin + izleme şartı; sistem klasörlerine yazma kapalı; `FileTransferLog` denetimi; 1 GB sınır |

> **Yetki (2026-09-30):** Modülün tüm HTTP uçları `RemotePcView` iznini ister (`Permission.RemotePcView = 10`,
> seed'de Admin rolüne verilir); yoksa 403. Policy modülün kaydında tanımlanır (`RemoteDeskModule.ViewPolicy`), claim
> girişte token'a yazıldığı için rol izni değişince kullanıcı yeniden giriş yapmalıdır. Okuma bileti yalnızca `view`
> ucundan çıktığı için WHEP de dolaylı olarak bu izne bağlıdır. `PcHub` (PC istemcisi) kullanıcı JWT'si değil PC'nin Basic Auth kimliğini ister (§ 5.2).
>
> **Uzaktan kontrol ayrı izindir (Faz 8):** `RemotePcControl = 11` (Admin'e seed). Viewer hub'ı (`/hubs/remote-desk/viewer`) bu izni
> ister — izinsiz kullanıcı bağlanamaz (negotiate 403). Kontrol için izlemek de gerekir (`RemotePcView` + canlı kiralama).
>
> **Denetim raporu ayrı izindir (2026-10-05):** `RemotePcAudit = 12` (Admin'e seed, `RemoteDeskModule.AuditPolicy`). İzleyen operatör
> başkalarının izleme geçmişini görmez. Policy bu yüzden **controller başınadır**: `RemoteDeskControllerBase` yalnızca kimlik ister,
> `PcController`/`ViewController` `ViewPolicy`, `ReportController` `AuditPolicy` taşır — tabana policy koymayın, `[Authorize]`'lar
> VE ile birleşir ve rapor izleme iznini de isterdi.
>
> **Dosya transferi ayrı izindir (2026-10-05):** `RemotePcFileTransfer = 13` (Admin'e seed, `RemoteDeskModule.FileTransferPolicy`).
> Viewer hub metotları (`ListPcDirectory`/`RequestDownload`/`RequestUpload`) ve `FileTransferController` bu izni ister; ayrıca her işlem,
> kullanıcı o PC'yi **o an izliyorsa** kabul edilir (kontrol ALMAK gerekmez — izlemeye bağlı, 2026-10-05). Yükleme sistem klasörlerine PC
> tarafında reddedilir ([§ 12.8](#128-dosya-transferi-2026-10-05)).
>
> **Viewer hub'ın yetki modeli (2026-10-05):** `/hubs/remote-desk/viewer` sınıf politikası **`RemotePcView`** (bağlanmak için en az
> izleme izni); kontrol metotları ayrıca `RemotePcControl`, dosya metotları `RemotePcFileTransfer` (metot politikaları, sınıfla VE ile
> birleşir). Önceden sınıf `RemotePcControl`'dü — dosya transferi kontrole bağlıyken yetiyordu; izlemeye bağlanınca View+FileTransfer
> (kontrolsüz) kullanıcının da bağlanabilmesi için taban `RemotePcView`'e indirildi (regresyon yok: kontrol zaten izlemeyi gerektirir).

---

## 12. Uzaktan kontrol (Faz 8+)

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
- **Tablo ikiye bölünür (2026-10-25):** telde kabul edilen kod kümesi OS'tan bağımsızdır ve sözleşmededir
  (`Contracts/Hub/KeyboardCodes.cs`; sunucu yalnızca `IsKnown` ile süzer). Kodun platform tuş kimliğine çevrilmesi istemcinindir — Windows:
  `Scadex.RemoteDesk.Windows.Service/Services/Input/WindowsScanCodes.cs` (set 1); ileride Linux evdev, Android `KeyEvent`. İstemci tablosu
  kümedeki her kodu karşılamalıdır; küme genişlerse istemci tabloları da genişler.
- Win, Alt+Tab, Ctrl+W tarayıcıda yakalanamaz: tam ekranda Keyboard Lock API (Chromium), yoksa araç
  çubuğunda özel düğmeler.
- Ctrl+Alt+Del `SendInput` ile gönderilemez ([§ 9.7](#97-bilinen-sınırlar)).

### 12.5 Takılı tuş emniyeti

İstemci basılı tuş/düğmeleri izler; kontrol bittiğinde, bağlantı koptuğunda ya da 5 sn olay gelmezken
basılı bir şey kalmışsa **hepsini bırakır**. Tarayıcı `blur`/`visibilitychange`'de de bırakma gönderir.

### 12.6 UIPI ve güvenli masaüstü

Normal (medium integrity) yetkiyle çalışan istemci **yönetici olarak çalışan (high integrity) pencerelere** `SendInput` ile girdi
gönderemez; Windows bunu çoğu zaman **sessizce yutar** (`SendInput` başarı döner, hiçbir şey olmaz). Yönetici hesabında Görev
Yöneticisi kendiliğinden yükseltilmiş açıldığı için öndeyken uzaktan fare/klavye ona ulaşmaz. Normal yetkiden kod ile aşılamaz (OS
güvenlik sınırı).

**Çözüm (2026-10-25, Faz 11a): girdiyi SYSTEM yetkili Agent gönderir** (§ 9.1). SYSTEM, yönetici pencerelerinden yüksek bütünlüktedir;
UIPI engeli olmaz — görev, UAC ya da manifest gerekmez. Agent'ın kimliği açılış günlüğüne yazılır ("kimlik SYSTEM"). `SendInput`
reddedilirse operatöre neden gösterilir (aşağıda "Girdi durumu"). Önceki çözüm (2026-10-01: istemciyi Görev
Zamanlayıcı'da "en yüksek ayrıcalıkla" başlatan `Install-StartupTask.ps1`) Faz 11a ile kaldırıldı; `requireAdministrator` ve `uiAccess`
zaten elenmişti (biri her açılışta UAC sorar, biri kod imzası ister).

**Güvenlik:** SYSTEM Agent, `RemotePcControl` iznine sahip kullanıcıya o PC'de fiilen yönetici düzeyi verir — bilinçli kabul.
**Güvenli masaüstü (Faz 11b, dar — 2026-10-25, kullanıcı kararı):** UAC onay penceresi ve kilit ekranı ayrı bir masaüstündedir
(`winsta0\Winlogon`). DXGI yakalaması ve `SendInput` çağıran iş parçacığının/sürecin masaüstüne gider; güvenli masaüstüne yalnızca SYSTEM
bağlanabilir. Agent:
- **`InputDesktopWatcher`** — `OpenInputDesktop` ile aktif masaüstünü (`Default` / `Winlogon` / `Screen-saver`) yalnızca yayın ya da
  kontrol sürerken 300 ms'de bir yoklar (boşta yoklamaz).
- **Yayın:** FFmpeg `DesktopProcess` ile aktif masaüstüne bağlı başlatılır (`CreateProcessW` + `lpDesktop`; `Process.Start` masaüstü
  seçemez). Masaüstü değişince aynı hedefle yeni masaüstünde yeniden başlar — merkez oturumu kapanmaz, tarayıcı WHEP'i kendiliğinden
  yeniden bağlar (birkaç yüz ms kesinti). Agent'taki bütün FFmpeg başlatmaları (sınama, sürüm sorgusu dahil) tek kilitle bu başlatıcıdan
  geçer: kalıtılan pipe uçları başka bir çocuğa sızmasın.
- **Girdi:** işçi kendi iş parçacığındadır ve her paketten önce aktif masaüstüne `SetThreadDesktop` ile bağlanır; geçişte basılı kalanlar
  bırakılır.

**Güvenlik (bilinçli karar):** operatör UAC onayı verebilir ve parolayı biliyorsa kilidi açabilir. Readiness raporu "güvenli masaüstünü
uzaktan tıklamayın" diyordu; kullanıcı AnyDesk davranışını istedi. Agent zaten SYSTEM olduğundan bu yeni bir yetki değildir; `RemotePcControl`
izni o PC'de fiilen yönetici yetkisi demektir. **Hâlâ yok:** giriş ekranı (Agent kullanıcı oturumunda) ve Ctrl+Alt+Del.

**Girdi durumu operatöre gösterilir (2026-10-25, readiness P1.5).** `SendInput` reddin nedenini söylemez; Agent her girdi turunun sonunda
durumu çıkarır ve yalnızca **değişince** `ReportControlInput` ile bildirir, merkez kontrol eden tarayıcıya `ControlInputState` olarak iletir
(başka/bitmiş oturumun ya da başka PC bağlantısının bildirimi yok sayılır). Araç çubuğunda, kontrol sürerken:

| Durum | Ne zaman | Operatörün gördüğü |
|---|---|---|
| `Ok` | Normal masaüstü | (gösterim yok) |
| `SecureDesktop` | Aktif masaüstü `Winlogon`/`Screen-saver` ve Agent oraya bağlı | Sarı: "PC'de kilit ekranı ya da UAC onayı açık — tıklama ve tuşlarınız oraya gider." |
| `SecureDesktopUnavailable` | Güvenli masaüstü açık ama Agent bağlanamadı / giriş masaüstü açılamıyor | Kırmızı: "…PC istemcisi oraya erişemiyor." |
| `Rejected` | Gönderim reddedildi, masaüstü erişilebilir (nedeni bilinmiyor) | Kırmızı: "PC girdiyi reddetti — öndeki pencere ya da bir güvenlik yazılımı engelliyor olabilir." |
| `MonitorMissing` | İzlenen monitör PC'de yok | Kırmızı: "İzlenen monitör PC'de artık yok — monitörü yeniden seçin." |

Ek sistem çağrısı yalnızca bir gönderim reddedilince yapılır (giriş masaüstü açılabiliyor mu); masaüstü değişimi (`InputDesktopWatcher.Changed`)
kontrol sürerken girdi beklemeden değerlendirilir, böylece UAC açılır açılmaz sarı uyarı görünür. Red bir sonraki başarılı turda kalkar.
**Bilinçli olarak yok:** öndeki pencerenin bütünlük düzeyini okuyan "yükseltilmiş pencere" tanısı — üretimde Agent hep SYSTEM'dir
(`Program.cs` SYSTEM olmayan Agent'ı geliştirme dışında çıkarır), UIPI engeli oluşmaz; geliştirmede bu durum `Rejected` görünür.

### 12.7 Uygulama (fare Faz 8, 2026-09-30; klavye Faz 9, 2026-10-01)

**Merkez** (`Control/RemoteControlCoordinator`, singleton, bellekte; `Hubs/ViewerHub`):

- `RequestControl(deviceId)` → `RequestControlResponse { status, controlSessionId, controllerName, message, idleTimeoutSec }`.
  Red istisna değil yanıttır: `Busy` (başka kullanıcı; adı döner), `NotViewing` (canlı kiralama yok — **görmeden kontrol verilmez**),
  `PcNotConnected`, `NotFound`. Aynı bağlantıdan tekrar istek aynı oturumu döndürür; **aynı kullanıcının başka sekmesi devralır**
  (eskisine `Replaced`), başka kullanıcı devralamaz.
- `SendInput(batch)`: tarayıcı `send` ile (yanıtsız) gönderir. Sunucu yalnızca o bağlantının oturumunu kabul eder, monitörün PC'de
  olduğunu doğrular, olayları süzer (Move/Down/Up/Wheel; KeyDown/KeyUp yalnızca `code` Contracts'taki `KeyboardCodes` kümesindeyse;
  `x,y` [0,1]'e, tekerlek ±20 çentiğe kırpılır; paket
  başına ≤ 256 olay) ve PC'ye hemen iletir — kuyruk ve saklama yoktur. Geçersiz paket sessizce atılır.
- Bitiş (`RemoteControlEndReason`): `Released` (bırak / ekrandan çıkış), `ViewerDisconnected`, `PcDisconnected`, `Idle` (**5 dk** girdi
  yok), `ServerRestart`, `ViewEnded` (kullanıcının o PC'deki son kiralaması düştü; süpürmede, ≤ 5 sn), `Replaced`. Sunucu bitirirse
  tarayıcıya `ControlEnded { controlSessionId, reason }`, PC'ye `ControlEnded` gider.
- Denetim: `remotedesk.RemoteControlSession` (kim, hangi PC, başlangıç/bitiş, neden, olay sayısı). Girdinin kendisi kaydedilmez.
  Açılışta açık kalan satırlar `ServerRestart` ile kapanır. `GET pcs/{id}` → `control: { userId, userName, startedUtc } | null`.

**Tarayıcı** (`lib/remote-control-session.ts` + `hooks/use-remote-control.ts`, PC ekranında "Kontrolü al / bırak"):

- Hub bağlantısı yalnızca kontrol sırasında açıktır; otomatik yeniden bağlanma yok (koparsa sunucu zaten bitirir).
- Olaylar video üstündeki şeffaf katmandan (yalnızca kontrol sizdeyken olay yakalar) alınır, 16 ms'de bir paketlenir; kuyruktaki son
  olay da hareketse yenisi onun yerine yazılır. `pointerdown`'da pointer capture (görüntü dışında bırakılan düğme de `up` üretir).
- Tekerlek Windows birimine çevrilir: piksel modunda ×1,2 (Chromium'da bir çentik ≈ 100 px → 120), satır ×40, sayfa ×120. `deltaY`
  DOM yönündedir (pozitif = aşağı); işareti istemci çevirir.
- Klavye: katman `tabIndex=-1` ile odak alır (kontrol verilince ve her tıklamada). `keydown/keyup` → `KeyDown/KeyUp { code }`
  (`KeyboardEvent.code`, karakter değil); tarayıcının varsayılanı engellenir, basılı tutulan tuşun tekrarlanan `keydown`'ları da gider
  (Windows enjekte edilen tuşu kendisi tekrarlamaz). IME bileşimindeki olaylar atlanır.
- Tarayıcının / Windows'un önce aldığı tuşlar için çubukta **Win**, **Alt+Tab**, **Ctrl+Shift+Esc** düğmeleri (`sendCombo`: sırayla bas,
  ters sırayla bırak) ve **Tam ekran**: oynatıcı tam ekrana geçer, Chromium'da `navigator.keyboard.lock()` ile Win/Alt+Tab/Ctrl+W da
  sayfaya gelir (çıkış: Esc basılı tutulur); tam ekrandan çıkınca ya da kontrol bitince kilit kalkar. **Ctrl+Alt+Del gönderilemez.**
- Katman odağı ya da pencere odağı kaybolunca, sekme gizlenince basılı düğme ve tuşlar için `Up/KeyUp` gönderilir. Monitör değiştirmek
  kontrolü bırakır.

**PC** (`Services/Input/RemoteInputService`, hosted service + tek kuyruk):

- `SendInput` + `MOUSEEVENTF_ABSOLUTE | VIRTUALDESK`. Sanal masaüstü monitörlerin **DXGI dikdörtgenlerinin birleşimi**dir
  (`GetSystemMetrics` işlemin DPI farkındalığına göre ölçeklendiği için kullanılmaz; istemcide `app.manifest` yoktur). `Down/Up` konum
  taşıyorsa hareketle tek `INPUT`'ta gider.
- Tuşlar `KEYEVENTF_SCANCODE` (+ `0xE0` önekliler için `EXTENDEDKEY`) ile basılır; `INPUT` fare/klavye union'ıdır (x64'te 40 bayt).
  `Pause` (E1 dizisi), medya tuşları ve F13+ tabloda bilerek yok.
- Ardışık hareketlerden yalnızca sonuncusu uygulanır; düğme/tekerlek/tuş sırası korunur.
- Takılı düğme/tuş emniyeti: `ControlEnded`, merkez bağlantısının kopması ya da basılı bir şey varken 5 sn olay gelmemesi → basılı düğme ve
  tuşların hepsi bırakılır.
- Gösterge: pencerede kırmızı şerit "… {kullanıcı} PC'yi uzaktan kontrol ediyor (fare ve klavye)", tepside kırmızı noktalı ikon ve
  "KONTROL EDİLİYOR".

### 12.8 Dosya transferi (2026-10-05)

Operatör, ekranını izlediği PC'nin dosya sistemini gezer, dosya indirir ve yükler. Ayrı izindir: **`RemotePcFileTransfer = 13`**
(Admin'e seed; `RemoteDeskModule.FileTransferPolicy`). Kontrol izninden bağımsız — dosya transferi en tehlikeli yetenektir, ayrı
verilmeli. **Transfer İZLEMEYE bağlıdır, kontrole değil (2026-10-05 kararı):** her işlem **viewer hub**'ından gelir ve yalnızca kullanıcı
o PC'nin ekranını **o an izliyorsa** kabul edilir (`FileTransferCoordinator` → `ScreenStreamCoordinator.IsViewingAsync`); izleme biterse
transfer imkânı da düşer ("görmeden iş yapılmaz"). Fare/klavye **kontrolünü ele geçirmek gerekmez** — salt bir log dosyasını indirmek için
PC'yi tek kullanıcıya kilitlemek (kontrol şeridi) aşırı olurdu. Viewer hub'ına bağlanmak `RemotePcView` ister (sınıf politikası); kontrol
metotları ayrıca `RemotePcControl`, dosya metotları `RemotePcFileTransfer` ister (metot politikaları) — biri olup diğeri olmayan kullanıcı
yalnızca hakkı olan metotları çağırır.

**Mimari — baytlar hub'dan GEÇMEZ.** PC dinleyen port açmaz; her şey merkez üzerinden gider. Küçük veri (dizin listesi, komut) hub'da
taşınır; **dosyanın baytları biletli HTTP** ile akar (`FileTransferStore` — 1 GB sınır, yarım kalan transfer 30 dk'da süpürülür, merkez
`%TEMP%\scadex-remotedesk-ft`). Tarayıcı uçları `FileTransferController`'da (kullanıcı JWT'si + `RemotePcFileTransfer`): yükleme
`POST filetransfer?fileName=` (gövde = ham baytlar, transfer id döner) + indirme `GET filetransfer/{id}` (`DeleteOnClose`). PC uçları
`PcFileTransferController`'da: `PUT filetransfer/{id}/push?ticket=` ve `GET filetransfer/{id}/pull?ticket=`. 1 GB akışta zorlanır
(`FileTransferStore.CopyWithLimitAsync`), aşılırsa reddedilir. **IIS'te ayrıca istek filtrelemesi** vardır (varsayılan 30.000.000 bayt —
~28,6 MB üstü gövde uygulamaya ulaşmadan 404.13 alır; `[DisableRequestSizeLimit]` bunu aşmaz): `Scadex.WebAPI/web.config`
`requestLimits maxAllowedContentLength` = 1 GB + 1 MB (2026-10-25). İki sınır birlikte değişir.

**Sahipsiz geçici dosyalar süpürülür (2026-10-25, readiness P1.2).** Geçici dosyanın sahibi yalnızca bellekteki kayıttır; sunucu aktarım
ortasında yeniden başlarsa dosya sahipsiz kalırdı. `FileTransferStore.Sweep` 5 dakikada bir (ilki açılışın hemen ardından) klasörü tarar:
kaydı olmayan **ve son yazımı 10 dakikadan eski** dosyalar silinir, adet ve boyut günlüğe yazılır. "Açılışta klasörü boşalt" bilerek
yapılmaz: IIS havuz geri dönüşümünde yeni süreç, eski süreç birkaç saniye daha aktarım sürdürürken açılır; eski sürecin yazdığı dosya bizim
kayıtlarımızda yoktur ama sürekli tazelenir. Tarayıcıya akmakta olan indirme (kayıt düşmüş, `DeleteOnClose`) silinemez ve atlanır.

**İzleme biterse PC ayağı kesilir (2026-10-25, readiness P1.3).** İzleme yalnızca istek anında denetlenseydi operatör izlemeyi bırakınca
süren aktarım tamamlanırdı. `FileTransferCoordinator.SweepAsync` (`ScreenStreamWorker` turunda, 5 sn — kontrolün `ViewEnded` kalıbı) PC'nin
dahil olduğu açık transferi (PC gönderiyor ya da PC'ye yazma komutu verilmiş) kullanıcının o PC'deki son kiralaması düşünce keser:
`FileTransferEntry.Abort` iptal edilir → `push`/`pull` akışı kopar (yarım geçici dosya silinir; `pull` elle akıtıldığı için ortasında
kesilebilir, PC `Content-Length` tutmadığı için parçayı atar), kayıt ve bileti düşer (başlamamış ayak 401), denetim başarısız kapanır
("İzleme bittiği için aktarım durduruldu."), tarayıcıya `Failed`. PC'nin sonradan gelen sonucu yok sayılır. Tarayıcı ayakları (merkeze
yükleme, merkezden indirme) PC'ye dokunmadığı için kesilmez.

**PC uçlarının yetkisi (2026-10-25, readiness P0.5):** iki şart birden — PC'nin Basic Auth kimliği (`DeviceAuthPolicies.Pc`, `PcHub` ile
aynı; kullanıcı JWT'si ya da kontrol kartının kimliği geçmez) VE transferin bileti. Bilet transfer açılırken değil **PC'ye komut
gönderilirken** üretilir (`IssueTicket`; tarayıcıdan 1 GB yükleme uzun sürebilir), **2 dk** geçerlidir, yalnızca **o transferin cihazınca**
ve **bir kez** kullanılır (`TryConsumeTicket` — atomik, sabit zamanlı karşılaştırma; ikinci istek 401). 2 dk isteğin başlaması içindir,
başlamış aktarım sürer. PC aktarımı yeniden denemediği için tek kullanım güvenlidir. Red 401'dir; neden günlüğe yazılır (transfer, cihaz,
IP), bilet yazılmaz. İmzalı (HMAC/JWT) bilet seçilmedi: cihaz kimliğine bağlamak daha sade ve aynı korumayı sağlıyor. Agent bu isteklere
hub'daki Basic Auth başlığının aynısını koyar (`RemoteDeskClientOptions.BasicAuthorization`).

**Akış.** Gezme: viewer hub `ListPcDirectory(deviceId, path)` → sunucu SignalR **client-result** ile PC'ye sorar
(`IPcHubClient.ListDirectory` → `DirectoryListing`; path boşsa kökler — Windows'ta sürücüler —, 20 sn zaman aşımı). İndirme (PC→tarayıcı): hub
`RequestDownload` → `FileTransferStore` transfer açar, bilet üretir → PC'ye `UploadFile(id, path, ticket)` → PC dosyayı `push` ucuna
akıtır → tarayıcı `GET filetransfer/{id}` ile indirir. Yükleme (tarayıcı→PC): tarayıcı `POST filetransfer` ile merkeze akıtır
(transferId) → hub `RequestUpload(id, destPath)` → PC'ye `DownloadFile(id, destPath, ticket)` → PC `pull` ucundan çekip `destPath`'e
yazar. PC her iki yönde sonucu `ReportFileTransfer(FileTransferResult)` ile bildirir; sunucu tarayıcıya `IViewerHubClient.FileTransferState`
yollar (`FileTransferStatus`: CentralReceiving/ReadyForPc/PcSending/ReadyForBrowser/Completed/Failed).

**Yazma sistem klasörlerine kapalı (kullanıcı kararı).** Gezme/okuma/indirme serbest; ama **yükleme** `C:\Windows`,
`Program Files`/`Program Files (x86)`, başlangıç klasörleri (CommonStartup + **oturumdaki kullanıcının** Startup'ı), kurulum klasörü ve
`%ProgramData%\Scadex`'e PC tarafında (yazan taraf) reddedilir; nedeni sonuç olarak döner ("Bu klasöre yazma kapalı (sistem klasörü).").
**Agent SYSTEM çalıştığı için (Faz 11a, 2026-10-25) denetim sertleşti** — `Services/FileTransfer/FilePathPolicy.cs`:
- Ağ ve aygıt yolları (`\\sunucu\paylaşım`, `\\?\`, `\\.\`) **gezme ve okumada da** reddedilir: SYSTEM ağa makine hesabıyla çıkar
  (kullanıcının göremediği paylaşımlar açılırdı), aygıt yoluyla ham disk okunabilirdi. ADS (`dosya:akış`) yolları da reddedilir.
- Yazmada dosya adı yalın olmalı (ayraç, `..`, `:`, ayrılmış ad `CON`/`NUL`/`COM1`… yok); hedef tam nitelikli yerel yol olmalı; hedef
  klasörden köke kadar ya da hedef dosyada **junction / sembolik bağ** olmamalı (OneDrive gibi bulut yer tutucuları bağ sayılmaz).
- Yazma **atomiktir:** önce `<ad>.scadex-part`'a yazılır; açılan dosyanın gerçek yolu (`GetFinalPathNameByHandle`) korumalı köklere karşı
  yeniden denetlenir (yarış / bilinmeyen yönlendirme); `Content-Length` tutmazsa reddedilir; diske boşaltılıp hedefin yerine taşınır.
  Hata ya da iptalde parça silinir — mevcut dosya bozulmaz. Bütünlük için SHA-256 merkez desteği istediğinden yapılmadı.

**Denetim.** Dizin listeleme kaydedilmez; her **indirme/yükleme** `remotedesk.FileTransferLog` (`IRemoteAccessRecord`: kim, PC, yön,
yol, boyut, başlangıç/bitiş, sonuç, hata). Açılışta açık kalan satırlar `ScreenStreamWorker` ile kapatılır; store süpürmesi aynı
worker'da. Rapor ekranında `reports/transfers` sekmesi (`RemotePcAudit` iznine bağlı, diğer denetim raporlarıyla aynı).

**PC göstergesi.** Aktarım sürerken pencere şeridi + tepsi "DOSYA AKTARILIYOR" (izleniyor/kontrol ediliyor kalıbı, `ConnectionVM`).

**Tarayıcı — iki panelli pencere (2026-10-05).** PC ekranında (PC **izlenirken** + `RemotePcFileTransfer` izniyle) bir "Dosya aktarımı"
düğmesi büyük bir modal açar (`components/file-transfer-dialog.tsx`): **sol = yerel**, **sağ = uzak (PC)** gezgin, **ortada Yükle →** (yerel
seçili dosya → uzak aktif klasör) **ve ← İndir** (uzak seçili dosya → yerel aktif klasör). Sağ panel (`remote-file-pane.tsx`) dizin listesini
react-query ile çeker (yükleme sonrası dialog aynı anahtarı invalidate eder).
- **Yerel panel (`local-file-pane.tsx`, `lib/local-fs.ts`) iki modludur (2026-10-26, kullanıcı kararı: kapalı ağda HTTPS zorunlu değil —
  2026-10-06'daki "yedek yok" kararının yerine):**
  - **Gezgin** — güvenli bağlamda (HTTPS, `localhost` ya da tarayıcı ayarıyla güvenli sayılan adres) ve Chromium/Edge'de File System Access
    API: "Klasör seç" ile seçilen kök altında yerel ağaç gezilir, indirme seçili yerel klasöre yazılır.
  - **Klasik** — güvenli bağlam ya da API yoksa (HTTP kurulumu, Firefox): dosya `<input type="file">` ya da sürükle-bırak ile seçilir, indirme
    tarayıcının indirme mekanizmasıyla (`saveBlob`: blob URL + `<a download>`) İndirilenler'e ya da tarayıcı sorarsa seçilen yere kaydedilir.
    Klasör gezgini yoktur.
  Mod pencere açılırken `fsaAvailable()` ile bir kez belirlenir. HTTP'de tam deneyim için tarayıcı ayarı README § 1.4'tedir.
- **İndirme iki adıma ayrıldı** (`FileTransferApi`): `prepareDownload(pcPath)` (hub `RequestDownload` + `FileTransferState` bekler) +
  `fetchDownloadBlob(transferId)` (biletli `GET`, tek kullanımlık). Çağıran baytı gezginde seçili yerel klasöre FSA ile yazar, klasikte
  `saveBlob` ile kaydeder. `upload(file, destPath)` seçili yerel dosyayı (`LocalSelection.getFile()`) alır.
- Dosya transferi **kontrol oturumundan ayrı** kendi viewer hub bağlantısını taşır: `lib/file-transfer-session.ts`,
  `hooks/use-file-transfer.ts` (izleme + izin varken açar, kapanınca kapatır), `models/fileTransfer.ts`.

### 12.9 Panolar arası aktarım (2026-10-06)

Operatör yerelde kopyaladığı **metni** uzak PC'ye ve tersine aktarır. Üst-orta araç çubuğunda (§ 12.7 sonrası) iki düğme: **Dışa aktar**
(yerel pano → uzak PC panosu) ve **İçe aktar** (uzak PC panosu → yerel pano). Dosya transferiyle **aynı yetki/kapı**: `RemotePcFileTransfer`
izni + PC'yi **o an izlemek** (kontrol almak gerekmez; `ClipboardCoordinator` → `IsViewingAsync`, `FileTransferCoordinator.ResolveAsync` ile
aynı). **Manuel** düğme (oto-senkron yok). İçerik **LOGLANMAZ/denetlenmez** (parola olabilir).

- **Akış.** Dışa: tarayıcı `navigator.clipboard.readText()` → viewer hub `SetRemoteClipboard(deviceId, text)` → `ClipboardCoordinator.SetAsync`
  → PC'ye `SetClipboard` → istemci Windows panosuna yazar (`ClipboardAccess`, WPF panosu STA → `Application.Current.Dispatcher`). İçe: hub
  `GetRemoteClipboard(deviceId)` → PC client-result `GetClipboard` → uzak pano metni → `navigator.clipboard.writeText(text)`.
- **Metin küçük olduğu için hub'dan geçer** (dosya baytları gibi HTTP'ye gerek yok). Sınır **1.000.000 karakter**: sunucu
  (`ClipboardCoordinator`), istemci okuması (`ClipboardAccess`) ve tarayıcı (dışa aktarma guard'ı) kapar. Varsayılan 32 KB SignalR alım sınırı
  yetmediği için **ViewerHub + PcHub `MaximumReceiveMessageSize` 4 MB'a** çıkarıldı (`ServiceRegistration`; diğer hub'lar varsayılanda).
- **İki yol (2026-10-26):** güvenli bağlamda tek tık `navigator.clipboard`. Yoksa (HTTP kurulumu) ya da tarayıcı izni reddedilirse iki adımlı
  pencere (`components/clipboard-dialog.tsx`): dışa aktarmada metin kutuya Ctrl+V ile yapıştırılıp gönderilir; içe aktarmada uzak metin
  seçili gelir, "Kopyala" (`document.execCommand('copy')` — güvenli bağlam istemez) ya da Ctrl+C. Düğmeler `canFiles` (izleme + izin) iken
  görünür; aynı viewer hub bağlantısını (`file-transfer-session.ts`) kullanır. Sonuç/hata sonner `toast` ile bildirilir.

---

## 13. Çözüm yapısı

```text
Modules/RemoteDesk/
  Scadex.RemoteDesk.Contracts/     net10.0, bağımlılıksız — hub metot adları, komut/durum/monitör tipleri,
                                   enum'lar, giriş olayları (RemoteInput.cs) + OS'tan bağımsız tuş kodu kümesi (KeyboardCodes.cs)
  Scadex.RemoteDesk/               net10.0 class library — merkez modülü
    Controllers/ Hubs/ Realtime/ Services/{Abstract,Concrete}/ Data/ Model/ ServiceRegistration.cs
    → Contracts + Scadex.Business (Signalization ile aynı referans yönü)
    Local/AgentPipeContract.cs     Agent ↔ arayüz pipe sözleşmesi + AgentSnapshot (§ 9.2)
  Scadex.RemoteDesk.Windows.Service/  net10.0-windows Worker — servis + Agent tek exe (§ 9.1) → yalnızca Contracts
    Program.cs                     argümansız = servis, --session = Agent; DI kayıtları
    app.manifest                   Per-Monitor DPI Aware v2
    appsettings.json               RemoteDesk:CentralApiUrl / DeviceId / DeviceSecret (kurulumda ACL'i kısılır)
    Supervisor/                    SessionAwareServiceLifetime (SCM oturum olayları), SessionSupervisorService, AgentLauncher
    Native/                        Win32 (WTS, token, CreateProcessAsUser, pipe), Sessions (hedef oturum seçimi)
    Agent/                         SessionContext, AgentStateService (özet + 3 komut), AgentPipeServer, AgentLifetimeService
    Services/                      Connection (PcHub istemcisi), Monitors, Encoding, Streaming (FFmpeg + Job Object),
                                   Input (SendInput), FileTransfer (+ FilePathPolicy), Clipboard (STA iş parçacığı)
    Shell/AgentLog.cs              %ProgramData% günlüğü
    tools/ffmpeg/                  LGPL build, git'te değil (Scadex'teki MediaTools gibi)
  Scadex.RemoteDesk.Windows/       net10.0-windows, WPF (+ UseWindowsForms: NotifyIcon) — YALNIZCA arayüz → yalnızca Contracts
    App.xaml(.cs)                  Generic Host: DI kayıtları, açılış/kapanış (yapılandırma dosyası yok)
    Helpers/                       BaseViewModel, ObservableProperty (+ SetProperty), RelayCommand — projenin
                                   kendi MVVM altyapısı; CommunityToolkit.Mvvm kullanılmaz
    Templates/DataTemplate.xaml    VM → View eşlemesi (DataType ile)
    ViewModels/                    MainWindowVM (CurrentView ile gezinme), HomeVM (durum ekranı), …
    Views/                         MainWindow, Home, … — code-behind yalnızca InitializeComponent
    Services/                      Agent (AgentPipeClient: pipe + sunucu doğrulaması), Shell (TrayIcon, UiLog)
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
ve **çalıştırarak** yapılır. İstemcinin saf mantığı (idempotency, FFmpeg argüman
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
| **4** ✅ | WPF yayın: monitör listesi, FFmpeg yöneticisi, kodlayıcı denemesi, `Start/StopScreenStream`, idempotency, Job Object. **2026-09-30:** merkez komutları bağlandı — aynı oturum tekrar gelirse yok sayılır, aynı monitöre yeni oturum (ya da sahadaki test yayını) varsa önce o durur; yayın durumu merkeze yalnızca değişince bildirilir; bilet reddedilirse (401) yeniden denemeden biter; merkez bağlantısı kopunca merkezin yayınları durur. PC'de "izleniyor" göstergesi: pencerede kırmızı şerit + tepside kırmızı noktalı ikon. | Sınandı: sunucunun komutuyla monitör MediaMTX'e yayınlandı (1080p, Intel QSV); istemci zorla öldürülünce FFmpeg kalmadı (Job Object); API öldürülünce istemci FFmpeg'ini 1,1 sn'de durdurdu |
| **5** ✅ | İzleme akışı: `view` ucu, hazır-bekleme, biletler, kiralama, otomatik durdurma, `ScreenSession`/`ScreenViewLog`. **2026-09-30:** `ScreenStreamCoordinator` (bellekte, tek kilit), `ScreenStreamWorker` (açılışta yetim oturumları kapatır, 5 sn'de bir süpürür), `GET pcs/{id}` (monitörler + yayın durumu + izleyici sayısı). Çekirdeğe `IMediaGateway.GetRuntimePathAsync` / `KickPublisherAsync`. | Sınandı: izleme 1,3–2,8 sn'de hazır (ilkinde kodlayıcı sınaması dahil); aynı monitöre ikinci izleyici 15 ms'de aynı yayına katıldı, farklı monitör ayrı yayın; yenilenmeyen kiralama 47 sn'de düştü; bırakınca 14 sn'de durdu; **tarayıcı öldürülünce 51 sn'de durdu**; okuyucusuz kiralama 60 sn'de durduruldu; gerçek WebRTC izleyicisi 75 sn boyunca kesilmedi; kick ucu (`v3/rtspsessions/kick`) doğrulandı; API yeniden başlayınca yetim oturum `Failed/ServerRestart`; istemci koparsa `Failed/ClientDisconnected`. Sınanmadı: istemcinin durdurmaya UYMADIĞI durum (kick bu yol için yazıldı) |
| **6** ✅ | Frontend: PC listesi, PC ekranı (monitör seçici + oynatıcı), `VITE_MODULES`. **2026-09-30:** `src/modules/remotedesk/` (liste, PC ekranı, `pc-stream-session.ts` — kiralama yenileme/bırakma, kopunca yeni kiralamayla yeniden başlama), `whep.ts`'e `lowLatency` seçeneği (yalnızca PC). `.env.development` → `signalization,remotedesk`; `.env.production` DEĞİŞMEDİ. İzleme `RemotePcView` iznine bağlı: menü maddesi ve rota izinsiz kullanıcıda gizli. | `npm run lint` + `npm run build` yeşil. Aynı akış (giriş → izleme → WHEP → kiralama) başlıksız Chrome'da test sayfasıyla sınandı: 1920×1080, 28–30 fps, jitter tamponu 39–52 ms. **React ekranları (2026-09-30), başlıksız Chrome + DevTools protokolüyle:** giriş → menü → PC listesi (bağlı, 2 monitör) → PC ekranı 4,3 sn'de 1920×1080 görüntü, monitör kartı "Yayında · 1 izleyici" → 2. monitöre geçiş 3,7 sn → listeye dönünce iki yayın da durdu, FFmpeg kalmadı; konsolda hata yok. Sınanmadı: bağlı olmayan PC ekranı, "Tekrar dene" |
| **7** ✅ | ~~Kurulum paketi + oturum açma görevi~~ → **2026-09-30 (kullanıcı kararı):** kurulum paketi yok, klasöre publish + elle dağıtım; başlangıç uygulamalarına `--tray` ile elle eklenir (§ 9.1, § 9.6). Yapılan: istemciye Serilog (§ 9.5, System Tray'de "Günlük klasörünü aç"), publish'e LGPL `LICENSE.txt`, FFmpeg yoksa publish'i durduran kontrol, kaynak ölçümü ([§ 16.3](#163-faz-7-ölçümleri-2026-09-30)). `CLAUDE.md` güncellendi. | Sınandı: self-contained publish (≈ 300 MB) scratchpad'e alındı, çalışma klasörü `System32` iken `--tray` ile açılıp merkeze bağlandı; günlükte açılış → bağlantı → yayın isteği → sınama → yayın (maskeli adres) → durdurma satırları, bilet yok; boşta ve yayında hedefler tuttu. Sınanmadı: temiz (geliştirme araçsız) PC, başlangıç kısayoluyla oturum açılışı, System Tray "Çıkış" satırı |
| **8** ✅ | Fare ([§ 12](#12-uzaktan-kontrol-faz-8)) + viewer hub + `RemoteControlSession` + yetki kararı. **2026-09-30:** yeni izin `RemotePcControl = 11`; § 12.7. Klavye olayları sözleşmede var, sunucu Faz 9'a kadar iletmez. | Sınandı (bu makine, başlıksız Chrome + DevTools fare olayları → gerçek imleç): "Kontrolü al" 0,3 sn'de verildi; görüntüde (0.25, 0.25) → imleç 480,270, (0.75, 0.5) → 1440,540 (1920×1080 monitör 0, beklenen 480,270 / 1439,540); 22 hızlı hareket → PC'ye 20 olay, imleç son noktada; bırakınca hareket PC'ye gitmedi; sayfa kapanınca `ViewerDisconnected`; izleme bırakılınca 1,9 sn'de `ViewEnded`; PC istemcisi kapanınca 0,6 sn'de `PcDisconnected`; izlemeden istek `NotViewing`; jetonsuz negotiate 401, izinsiz 403. Başlıkta "… kontrol ediyor" rozeti. **Sınanmadı:** gerçek tıklama/tekerlek (kullanıcının masaüstünde tıklamamak için), takılı düğme emniyeti, `Busy` (ikinci kullanıcı), 5 dk `Idle`, ikinci monitör ve negatif koordinatlı monitör, yönetici penceresi (UIPI) |
| **9** ✅ | Klavye (scan code, değiştirici tuşlar, takılı tuş emniyeti). **2026-10-01:** Contracts `KeyboardScanCodes` (Chromium `code` ↔ set-1), sunucu yalnızca tablodaki kodu iletir, PC `KEYEVENTF_SCANCODE` ile basar; tarayıcıda Win / Alt+Tab / Ctrl+Shift+Esc düğmeleri ve tam ekran + Keyboard Lock; § 12.7. | Sınandı (bu makine, Türkçe Q, başlıksız Chrome + DevTools tuş olayları): Not Defteri'ne uzaktan **tıklayıp** odaklandı, `Shift+M e r h a b a ␣ [ ] ; ' i , . ␣ 1 2 3` → metin birebir "Merhaba ğüşiıöç 123" (UTF-16 kod noktalarıyla doğrulandı); yalnızca Shift basılı bırakılınca PC 5 sn sonra bıraktı ("Basılı kalanlar bırakıldı (5 sn olay gelmedi): ShiftLeft"); özel tuş ve tam ekran düğmeleri görünüyor. **Sınanmadı:** Win / Alt+Tab / Ctrl+Shift+Esc düğmelerinin PC'deki etkisi (kullanıcının masaüstünü bozmamak için), gerçek tam ekran + Keyboard Lock (başlıksız tarayıcıda yok), AltGr kombinasyonları (@, €), sayısal tuş takımı |
| **Dosya T.** ✅ | Dosya transferi ([§ 12.8](#128-dosya-transferi-2026-10-05)): yeni izin `RemotePcFileTransfer = 13`; her iki yön, PC dosya sistemini gezme, dosya başına 1 GB, **PC'yi izleyen** kullanıcı (kontrol almak gerekmez); baytlar biletli HTTP ile (hub'dan değil); yükleme sistem klasörlerine kapalı; `FileTransferLog` denetimi + rapor sekmesi. **2026-10-05.** | Sınandı (bu makine, node + SignalR, konsol oturumu, **kontrol ALMADAN**): izlemeden önce dosya işlemi reddedildi ("ekranını izliyor olmalısınız"); `POST view` sonrası `ListPcDirectory` klasör/dosya ve sürücü köklerini döndürdü; 2 KB indirme byte-byte eşleşti (ReadyForBrowser); 3 KB yükleme hedefe yazıldı, byte-byte eşleşti (Completed); `C:\Windows`'a yükleme reddedildi (Failed, "sistem klasörü", dosya oluşmadı); üç işlem de `FileTransferLog`'a doğru yön/boyut/sonuçla düştü, `reports/transfers` listeledi. Regresyon: `RequestControl` hâlâ Granted + `SendInput` kabul. **Sınanmadı:** ~1 GB sınır aşımı, izleme transferin ortasında düşerse, gerçek tarayıcı arayüzü (gezgin paneli), izinsiz kullanıcı reddi (tasarımda) |
| **10** | ~~Tüm monitörler tek görüntü~~ (kapsamdan çıktı, 2026-10-06) · pano: metin olarak yapıldı ([§ 12.9](#129-panolar-arası-aktarım-2026-10-06)) | |
| **11a** | Servis + Agent + WPF ([§ 9.8](#98-süreç-modeli--servis--agent--wpf-faz-11a)) — **2026-10-25 uygulandı.** Servis (gözetmen, SCM oturum olayları, `CreateProcessAsUser`, Job Object, geri çekilmeli yeniden başlatma), Agent (eski WPF servisleri, SYSTEM, `SessionContext`, STA pano, `FilePathPolicy` + atomik yazma), arayüz yalnızca pipe istemcisi; kurulum `dotnet publish` × 2 + `sc create` (§ 9.6; betikler 2026-10-25'te kaldırıldı); Görev Zamanlayıcı yolu kaldırıldı. "Oturum açmadan bağlı" kapsamdan çıktı (kullanıcı kararı). | **Sınandı (bu makine, geliştirme modu — Agent normal kullanıcıyla, merkez kapalı):** `dotnet build Scadex.slnx` temiz; Agent pipe'ı açtı, arayüz sunucuyu doğrulayıp bağlandı (Debug düzeninde exe adıyla, paket düzeninde **tam yolla**); özet geldi (makine, oturum kullanıcısı, FFmpeg LGPL, 2 monitör); pipe'tan "Encoder'ları sına" iki monitörü sınadı (NVENC / QSV / VP9 sonuçları), "Tercih et" VP9 → özet `preferred=10`, "Otomatik" → `null`; yanlış sürüm `unsupportedVersion` aldı, izin verilmeyen mesaj (`sendInput`) bağlantıyı yanıtsız kapattı; Development'sız elle başlatılan Agent çıkış kodu 2 ile kapandı; self-contained yayın paketi üretildi (≈ 420 MB, `tools\ffmpeg` dahil). **Sınanmadı (yönetici + merkez gerekir):** servis modu (`sc create` ile kurulum, SCM, `CreateProcessAsUser` ile SYSTEM Agent), login/logout ve RDP'de yeniden başlatma, Agent öldürülünce kurtarma, SYSTEM'le yükseltilmiş pencereye girdi, pipe'a başka oturumun erişememesi, pano (STA), `FilePathPolicy` (junction/UNC/ADS reddi, atomik yazma), temiz PC'de kurulum, kaynak ölçümü |
| **11b** (dar) | UAC onay penceresi + kilit ekranı — **2026-10-25 uygulandı** (§ 12.6): `InputDesktopWatcher`, `DesktopProcess` (FFmpeg aktif masaüstünde, masaüstü değişince yeniden başlar), girdi işçisi kendi iş parçacığında masaüstüne bağlanır. Giriş ekranı ve Ctrl+Alt+Del kapsam dışı (kullanıcı kararları). | **Sınandı (bu makine, geliştirme modu):** regresyon — yeni başlatıcıyla sınama + yayın (masaüstü Default), iki yönde dosya aktarımı bayt bayt, girdi işçisi Default'a bağlanıp `SendInput` reddedilmedi, izleme bırakılınca FFmpeg 14 sn'de temiz kapandı. **Sınanmadı (SYSTEM Agent = servis kurulumu gerekir):** UAC onay penceresinin görüntüsü ve uzaktan "Evet", kilit ekranında görüntü ve uzaktan parola, dönüşte kesintisiz devam, yönetici pencerelerine (Görev Yöneticisi, IIS) girdi |
| **Ses** | PC sesinin aktarımı | |
| **Kayıt** | İzleme oturumunun video kaydı | |

Her faz sonunda rapor: değişen dosyalar · eklenen özellik · mimari değişiklik · nasıl doğrulandı · nasıl
çalıştırılır · bilinen kısıtlar · sonraki adım.

---

## 15. Test matrisi

**Bağlantı:** config yok/bozuk · merkez erişilemez · yanlış/eksik/iptal edilmiş sır (401) · sır yenilendi ·
aynı PC ikinci kez bağlanıyor · ağ kesintisi · uygulama System Trayden kapatılıp
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

### 16.3 Faz 7 ölçümleri (2026-09-30)

Aynı makine; publish edilmiş (Release, self-contained) istemci `--tray` ile, pencere hiç açılmadan. Süreç başına
`TotalProcessorTime` farkı / 30 sn / 4 mantıksal çekirdek. Yayında: monitör 0, Intel QSV kopyasız zincir, 30 fps · ≤1920 px ·
3000 kbps; ekran büyük ölçüde durağan (IDE açık, hareket az) — hareketli içerikte FFmpeg payı artar; Faz 1'deki %12–17
(1,5 sn'de bir değişen test karesiyle ölçüldü) üst sınır için daha iyi bir tahmindir.

| Durum | Süreç | CPU (makine) | Private / working set |
|---|---|---|---|
| Boşta (bağlı, izlenmiyor) | istemci | %0,13 (156 ms / 30 sn) | 29 MB / 91 MB |
| Yayında | istemci | %0,64 | 32 MB / 98 MB |
| Yayında | FFmpeg | %2,88 | 115 MB / 107 MB |

İzleme bırakılınca yayın durdu, FFmpeg kalmadı. Hedefler (§ 16.1: boşta ≈ %0 ve < 100 MB, yayında < %15) tuttu.

---

## 17. Açık kararlar

Önerilen seçenek ilk sıradadır:

1. ~~**PC bağlı değilken kabin `Warning`'e düşsün mü?**~~ **Kapandı (2026-09-30): hayır.** Bağlantı yalnızca bellekte;
   kabin durumuna bilerek yansımaz ([§ 10.4](#104-canlılık), CLAUDE.md).
2. ~~**İzlendiğine dair onay.**~~ **Kapandı (2026-09-30): onay alınmaz.** Yalnızca görünür gösterge (pencere şeridi +
   System Tray ikonu) ve `ScreenViewLog` denetim kaydı.
3. ~~**Yetki.**~~ **Kapandı (2026-09-30):** izleme `RemotePcView` iznine bağlı ([§ 11](#11-güvenlik-özeti)). Uzaktan kontrol
   (Faz 8) için ayrı izin gerekecek.
4. ~~**PC şablonu.**~~ **Kapandı (2026-09-29):** yeni tip `DeviceType.Pc`; mevcut "Bilgisayar" sistem şablonu bu tipe
   taşındı (Peripheral kullanılmaz). Admin ekranından başka PC şablonları da açılabilir.
5. ~~**Yazılım yedeğinin lisansı.**~~ **Kapandı (2026-09-29):** yazılım yedeği OpenH264 yerine VP9 (libvpx,
   telifsiz). H.264 yalnızca donanım kodlayıcılarında kullanılır; patenti sürücü/üreticiyle gelir.

---

## 18. Karar günlüğü

| Tarih | Karar |
|---|---|
| 2026-10-26 | **HTTPS zorunlu değil — kapalı ağda HTTP kurulumu** (kullanıcı kararı; 2026-10-06 "güvenli bağlam, yedek kod yok" kararının yerine). Dosya aktarımının yerel paneli güvenli bağlam yoksa klasik moda (dosya seç / sürükle-bırak, tarayıcı indirmesi), pano iki adımlı pencereye düşer; HTTPS'te ya da tarayıcı ayarıyla (`OverrideSecurityRestrictionsOnInsecureOrigin`, README § 1.4) bugünkü davranış kalır. Ayrıca HTTP'de girişin hiç yapılamamasına yol açan `crypto.randomUUID` (yalnızca güvenli bağlam) `newId()` ile değiştirildi. README'ye HTTP kurulum varyantı ve riskleri eklendi. Sınandı: lint + build; derlenmiş pakette korumasız `randomUUID` kalmadı. Sınanmadı: HTTP adresinden gerçek tarayıcıda klasik panel ve pano penceresi (tarayıcı otomasyonu yok). |
| 2026-10-26 | **Modül HTTP uçlarına rate limit** (kullanıcı kararı; 2026-09-11 "modül uçlarına rate limit yok" kararının yerine): `RemoteDeskControllerBase` ve `SignalizationControllerBase` çekirdeğin `Default` politikasını taşır (kullanıcı başına; PC uçlarında cihaz Id'si başına). Politika adları modüllerin erişebilmesi için Business'a taşındı (`Scadex.Business/Utils/RateLimiting/RateLimiterKey.cs`), kayıt `Program.cs`'te kaldı. Hub'lar bilerek dışarıda: uzun yoklamaya düşen bağlantıda 60 Hz girdi sınıra takılırdı. |
| 2026-10-25 | **Readiness P1.2 + P1.5 kapandı.** Geçici transfer klasöründe kaydı olmayan ve 10 dk'dan eski dosyalar 5 dk'da bir (ilki açılışta) silinir — "açılışta boşalt" IIS'in eski/yeni süreç çakışması yüzünden seçilmedi. Uzaktan kontrolde Agent girdi durumunu (`ControlInputState`: kilit ekranı/UAC, erişilemeyen güvenli masaüstü, red, monitör yok) yalnızca değişince `ReportControlInput` ile bildirir, merkez kontrol eden tarayıcıya iletir, araç çubuğunda gösterilir. "Yükseltilmiş pencere" tanısı (bütünlük düzeyi okuma) yazılmadı: üretimde Agent hep SYSTEM. Sınandı (bu makine): açılışta 6 eski dosya silindi, 10 dk'dan genç dosya kaldı; sahte PC'nin 6 bildiriminden yalnızca 3 değişim tarayıcıya ulaştı (aynı durum, başka oturum, tanımsız durum atıldı); aktarım regresyonu temiz. Sınanmadı: gerçek Agent'ın kilit ekranı/UAC'de durumu üretmesi (servis kurulumu ister). |
| 2026-10-25 | **Sunucu ve sözleşme OS'tan bağımsız + iki production açığı kapandı** (kullanıcı hedefi: ileride Linux/Android istemcileri). Klavye tablosu bölündü: sözleşmede OS'tan bağımsız `KeyboardCodes` (DOM kod kümesi), Windows set-1 değerleri istemcide `WindowsScanCodes`. Sunucu PC yolunu kendi OS kuralıyla işlemez (`PcPathText`: Windows sunucuda Linux yolu `\` ile birleşir, Linux sunucuda Windows yolu hiç bölünmezdi). Platforma özgü alanlar (DXGI ayrıntıları, `EncoderKind`, `AgentPipeContract`) belgelendi, sunucu yorumlamaz. Platform/yetenek bildirimi bilerek eklenmedi (ikinci platformla gelir). **Readiness P1.3:** izleme biten kullanıcının PC ayağı süpürmede kesilir (`FileTransferCoordinator.SweepAsync`). **IIS gövde sınırı:** `Scadex.WebAPI/web.config` `requestLimits` 1 GB + 1 MB (varsayılan 30 MB'ta transferler 404.13 alıyordu). Sınandı (bu makine, sahte PC): izleme bırakılınca `pull` 40 MB'ın 22,9 MB'ında, `push` 34,5 MB'ta kesildi, tarayıcıya `Failed` + neden; Windows/Linux yolundan ad ve hedef birleştirme doğru; tablo dışı tuş sunucuda atıldı. IIS'te 1 GB aktarım sınanmadı. |
| 2026-10-25 | **Kurulum betikleri kaldırıldı** (kullanıcı kararı — süreci karmaşıklaştırıyordu): `Deploy/Publish-Client.ps1` ve `Install-Service.ps1` silindi. Kurulum elle: iki `dotnet publish` → `Service` + `Tray` klasörlerini yan yana kopyala → `appsettings.json` → yönetici komut isteminde `sc create` / `sc failure` / `sc start` → isteğe bağlı arayüz kısayolu (§ 9.6). Kabul edilen eksik: `appsettings.json` (cihaz sırrı) ve günlük klasörü varsayılan izinlerle kalır, standart kullanıcı sırrı okuyabilir; istenirse `icacls` ile elle kısılır. |
| 2026-10-25 | **Faz 11b (dar): UAC onay penceresi + kilit ekranında izleme ve kontrol** (kullanıcı kararı; readiness raporunun "güvenli masaüstünü uzaktan tıklamayın" önerisinin bilerek tersi — AnyDesk davranışı). Agent aktif masaüstünü yalnızca iş varken izler (`InputDesktopWatcher`); FFmpeg `DesktopProcess` ile aktif masaüstünde başlar ve masaüstü değişince aynı hedefle yeniden başlar; girdi işçisi kendi iş parçacığında `SetThreadDesktop` ile aktif masaüstüne bağlanır. Kilit ekranı aynı masaüstünde olduğu için kapsama girdi (kullanıcı kararı). Giriş ekranı ve Ctrl+Alt+Del kapsam dışı. Yönetici pencereleri (Görev Yöneticisi, IIS) 11a'daki SYSTEM Agent ile zaten kapsanıyor. Servis kurulumuyla sınanacak. |
| 2026-10-25 | **Merkez sertleştirmesi (readiness P0.5 + P0.2):** dosya transferinin PC uçları `PcFileTransferController`'a taşındı ve PC'nin Basic Auth kimliğini ister; bilet PC'ye komut gönderilirken üretilir, 2 dk, cihaza bağlı, tek kullanımlık (imzalı token yerine cihaz kimliğine bağlama seçildi). `PcHub`: IP başına dakikada 60 istek rate limit (modülün kendi politikası) + Hello'suz bağlantı 10 sn'de kapanır. Çekirdekte `UseRateLimiter` yetkilendirmeden önceye alındı (kimliksiz sel de sayılsın). Sınandı (bu makine, Scadex2): gerçek Agent'la iki yönde transfer bayt bayt; bilet ikinci kullanımda, kimliksiz, kart kimliğiyle, kullanıcı JWT'siyle ve yanlış biletle 401; 70 kimliksiz negotiate → 60 × 401 + 10 × 429; Hello'suz bağlantı 10,0 sn'de kapandı; JWT uçları ve SCADA ingest regresyonsuz. Sınanmadı: süresi dolmuş bilet (kodda gözden geçirildi). |
| 2026-10-25 | **Faz 11a uygulandı — servis + Agent + WPF** (§ 9.1/§ 9.8): `Scadex.RemoteDesk.Windows.Service` tek exe (servis = gözetmen, `--session` = SYSTEM Agent); eski WPF `Services/` Agent'a taşındı; WPF yalnızca pipe istemcisi (durum + üç komut, sunucu yolu doğrulanır). **Agent yalnızca aktif, kullanıcılı oturumda** (kullanıcı kararı — giriş ekranında PC bağlı değil; "oturum açmadan bağlı" kapsamdan çıktı). Görev Zamanlayıcı yolu ve `Install-StartupTask.ps1` kaldırıldı; dağıtım `Deploy/Publish-Client.ps1` + `Install-Service.ps1` (`Service\` + `Tray\` ayrı klasör, `appsettings.json` ACL'i SYSTEM + Administrators). Dosya transferi SYSTEM için sertleşti (kullanıcı kararı): ağ/aygıt yolları her yönde red, yazmada yalın ad + bağsız yol + gerçek yol denetimi + atomik yazma (`FilePathPolicy`); gezme/okuma yerel diskte serbest kaldı. Pano Agent'ın STA iş parçacığında. |
| 2026-10-25 | **PC kimliği MAC değil HTTP Basic Auth** (kullanıcı kararı; kontrol kartıyla birlikte): kullanıcı adı PC cihazının Id'si, parola tarayıcının ürettiği 32 karakterlik onaltılık sır, sunucuda yalnızca SHA-256 (`PcDevice.CredentialHash`). `PcHub` `DeviceAuthPolicies.Pc` ister; yanlış kimlik negotiate'te 401. `PcDevice.MacAddress`, `IX_PcDevice_MacAddress`, `Contracts/Hub/MacAddress.cs`, istemcinin `NetworkAdapterService`'i, `HelloRequest.MacAddresses`, `HelloResponse.MatchedMacAddresses`, `HelloStatus.UnknownDevice/Ambiguous` silindi. Kesin geçiş (geçiş dönemi yok). HTTPS zorunlu değil — düz HTTP'de sırrın dinlenmesi kabul edilen risk (§ 5.3). 2026-09-29 "yalnızca MAC" kararlarının yerine geçer. |
| 2026-09-28 | Medya yolu: ekran → FFmpeg (`ddagrab`, H.264) → RTSP publish → MediaMTX → WHEP. Mevcut kamera akışı değişmez. |
| 2026-09-28 | .NET 10 (Scadex `net10.0`); kamerada publish olmadığı tespit edildi; `pc_*` için `IMediaPathAuthorizer`. |
| 2026-09-28 | Yayın bileti oturum boyu geçerli (FFmpeg yeniden bağlanınca MediaMTX tekrar sorar); okuma/yayın biletleri ayrı. |
| 2026-09-28 | Stop = kiralama bırakma; son izleyici gidince durur. Başlatmada yol hazır olana kadar sunucu bekler. |
| 2026-09-28 | `libx264` yok (GPL) → `h264_mf` yedek (2026-09-29 Faz 1 ile değişti, aşağıda); 4:2:0 ve B-frame yok zorunlu; klavyede `code` → scan code. |
| 2026-09-29 | Kimlik `Device.MacAddress` (2026-10-22'den beri çekirdeğin `PcDevice` ek tablosunda); ajan tablosu / `AgentId` / "ajan ekle" ekranı yok; modül şemasında cihaz kopyası yok. |
| 2026-09-29 | Kimlik doğrulama yalnızca MAC (kabul edilen risk, § 5.3); ayrı JWT şeması ve token ucu yok. |
| 2026-09-29 | PC istemcisi WPF (`Scadex.RemoteDesk.Windows`), servis/yardımcı/named pipe yok; minimal kaynak; "Agent" adı kullanılmaz. **→ 2026-10-06 ile değişti** (§ 9.8). |
| 2026-09-29 | Çoklu monitör MVP'de: monitör başına yol ve yayın, izleyici monitör seçer. |
| 2026-09-29 | Kontratlar `Scadex.RemoteDesk.Contracts`; projeler `Modules/RemoteDesk/` altında; doküman depo kökünde. |
| 2026-09-29 | WPF istemcisi projenin kendi MVVM altyapısını (`Helpers/`) ve Generic Host DI'ını kullanır; CommunityToolkit.Mvvm yok. |
| 2026-09-29 | Uygulayıcı Claude Code (bu depo). |
| 2026-09-29 | **Tek FFmpeg build'i:** Scadex sunucusu (`MediaTools/ffmpeg`) ve istemci (`tools/ffmpeg`) aynı BtbN 8.1 LGPL exe'yi kullanır; GPL build kaldırıldı. Çekirdeğin anlık görüntü + klip komutları H.264 ve H.265 kaynakta bu build'le doğrulandı (JPEG 1920×1080, klip tam 5,0 sn, ilk paket anahtar kare). |
| 2026-09-29 | **Kodlayıcı seçimi:** donanımda H.264 (monitörün kartı önce: NVENC / QSV / AMF), yoksa yazılımda VP9; her aday gerçek denemeyle sınanır. OpenH264, `h264_mf`, x264 çıkarıldı. Yakalama `-init_hw_device d3d11va=cap:<adaptör>` + `-filter_complex ddagrab`. |
| 2026-09-29 | **PC kendi tipidir (`DeviceType.Pc = 13`), Peripheral değil** (kullanıcı kararı). "Bilgisayar" sistem şablonu Pc'ye taşındı, Id'si korunur. |
| 2026-09-29 | Faz 2: `IMediaPathAuthorizer` — `cam_` yolu modüle hiç sorulmaz; modül `pc_`'yi üstlenir (`read` → okuma bileti, `publish` → `AllowedNetworks` + yayın bileti). Açık oturum tekilliği DB'de filtreli unique index (`[Status] IN (1,2,3,4)`; SQL Server filtreli index'te `NOT IN` yok). Modül uçları `api/RemoteDesk/...`. |
| 2026-09-29 | Faz 3: `PcHub` anonim; `Hello` normalize MAC ile aktif Pc cihazlarını eşler, tek eşleşme kabul, cihaz başına ilk bağlanan kazanır; bağlantı kaydı bellekte. İstemci SignalR'ın otomatik yeniden bağlanmasını kullanmaz (her bağlantı `Hello` ile yeniden eşlenmeli); geri çekilme 1/2/5/10/30 sn, reddedilince 60 sn. Pencere kapatmak System Trayye indirir; `--tray` argümanı pencereyi açmadan başlatır (oturum açma görevi için). |
| 2026-09-30 | **İstemcide test yayını kaldırıldı** ("Test yayını başlat", "Durdur", "yalnızca kodla" hedefi, `TestPublishUrl`): yayın yalnızca merkezin komutuyla. Sınama ve tercih monitör kartlarından ayrı bir "Encoder" kutucuğunda (tüm monitörler birlikte sınanır); monitör kartlarında yayın durumu salt okunur kalır. Arayüzde "kodlayıcı" yerine "encoder". |
| 2026-09-30 | **Kodlayıcı tercihi PC başına ve bellekte** (kullanıcı kararı): "Tercih et" rozetini ve tüm yayınları (merkez dahil) değiştirir, çalışmadığı monitörde otomatik seçim; tercih değişince yayın merkez oturumu kapanmadan yeniden başlar. Eski "Bu adayla yayınla" kaldırıldı: merkez yayınını durdurup test yayını açıyordu. Aynı işte yakalanan hata: `StreamSession.StopAsync` önce iptal ettiği için FFmpeg sahipsiz kalabiliyordu → önce nazik durdurma, sonra iptal. |
| 2026-10-05 | **Dosya transferi** (§ 12.8): yeni izin `RemotePcFileTransfer` (kullanıcı kararı — kontrol izninden ayrı); her iki yön, PC dosya sistemini gezme, dosya başına 1 GB. Baytlar hub'dan değil tek-kullanımlık biletli HTTP ile merkez üzerinden akar (`FileTransferStore`, `ScreenTicketStore` kalıbı); dizin listesi hub client-result. Yükleme sistem klasörlerine (Windows / Program Files / Startup) PC tarafında reddedilir, gezme/okuma/indirme serbest (kullanıcı kararı). Her indir/yükle `FileTransferLog` denetimi + rapor sekmesi; dizin listeleme kaydedilmez. |
| 2026-10-06 | **Panolar arası metin aktarımı** (§ 12.9): üst-orta araç çubuğunda Dışa/İçe aktar; `RemotePcFileTransfer` + izleme (yeni izin yok); metin hub'dan (küçük), 1.000.000 karakter sınırı, ViewerHub/PcHub `MaximumReceiveMessageSize` 4 MB; içerik loglanmaz; tek-tık `navigator.clipboard` (güvenli bağlam). İstemci panosu WPF STA dispatcher ile. |
| 2026-10-06 | **Güvenli-bağlam fallback kodu kaldırıldı** (kullanıcı kararı, "sade kod"): hem dosya transferi yerel paneli (klasik `<input type=file>` + tarayıcı indirmesi) hem pano yalnızca güvenli bağlamda (HTTPS/localhost) çalışır; `http://LAN-IP` için yedek yazılmaz, saha dağıtımında HTTPS/localhost sağlanır. `FSA_SUPPORTED` dallanması, `LocalClassicPane`, `save-blob.ts` silindi; `LocalState` tek biçime indi. |
| 2026-10-06 | **Kapsam:** Faz 10 "tüm monitörler tek görüntü" ve otomatik güncelleme kapsamdan çıktı (kullanıcı kararı). Faz 11 dörde ayrıldı: 11a (servis + Agent + WPF), 11b (kilit ekranı / UAC / Ctrl+Alt+Del), ses, kayıt. |
| 2026-10-06 | **PC istemcisi servis + Agent + WPF'e geçecek** (§ 9.8, kullanıcı önerisi; uygulanmadı): servis gözetmen, Agent kullanıcı oturumunda SYSTEM ile hub/yakalama/girdi/dosya/pano, WPF kullanıcı yetkisinde yalnızca gösterge. Hub bağlantısını Agent tutar; kullanıcı Agent'ı kapatamaz; dosya transferi Agent kimliğiyle, sistem klasörü reddi aynen; pipe yalnızca durum + üç komut. Görev Zamanlayıcı kalkacak (`Install-Service.ps1` + `HKLM\…\Run`). Seviye karma: paket yok, kod imzası sertifika gelince. 2026-09-29 "servis/yardımcı/named pipe yok" kararının yerine geçer. |
| 2026-10-06 | Faz 11a projesi: `Scadex.RemoteDesk.Windows.Service` (servis + Agent tek exe; "Agent" `--session` modunun adı). WPF `Scadex.RemoteDesk.Windows` adıyla kalır. |
| 2026-10-05 | **Dosya transferi iki panelli pencereye taşındı** (kullanıcı kararı): inline tek panel yerine büyük modal — sol yerel / sağ uzak gezgin, ortada Yükle/İndir. Yerel panel **hibrit** (kullanıcı kararı): File System Access API güvenli bağlamda (HTTPS/localhost, Chromium/Edge) tam gezilebilir + indirme seçili yerel klasöre; yoksa klasik "dosya seç" + tarayıcı indirmesine düşer (düz `http://LAN-IP` tarayıcıda yerel gezme yok — web platform kısıtı). İndirme `prepareDownload`+`fetchDownloadBlob` olarak ayrıldı (bayt tarayıcıya ya da yerel klasöre). `file-browser.tsx` kalktı → `file-transfer-dialog.tsx` + `local-file-pane.tsx` + `remote-file-pane.tsx` + `lib/local-fs.ts`. |
| 2026-10-05 | **Dosya transferi KONTROLE değil İZLEMEYE bağlandı** (kullanıcı kararı, ilk uygulamadan sonra): salt dosya indirmek için fare/klavye kontrolünü ele geçirmek (PC'yi tek kullanıcıya kilitleyen kontrol şeridi) aşırıydı. Artık kullanıcı PC'yi **o an izliyorsa** yeter (`FileTransferCoordinator` → `IsViewingAsync`; eski `GetControlForViewerAsync`/`ControlLease` silindi). Viewer hub sınıf politikası `RemotePcControl` → **`RemotePcView`**; kontrol metotlarına `RemotePcControl`, dosya metotlarına `RemotePcFileTransfer` metot politikası eklendi. Frontend: dosya transferi kontrol oturumundan ayrıldı — kendi viewer hub bağlantısı (`lib/file-transfer-session.ts` + `hooks/use-file-transfer.ts`); panel izlerken görünür. Kontrol (fare/klavye) yolu değişmedi. |
| 2026-10-05 | **İzleme Raporu** (§ 10.5/§ 10.6/§ 11): yeni izin `RemotePcAudit` (kullanıcı kararı — izleme izninden ayrı); `ScreenViewLog` ve `RemoteControlSession` sayfalı listelenir, özet kartları ve PC ekranından bağlantı var. Girdi sayısı raporda gösterilmez (kullanıcı kararı). Taban controller'dan policy kaldırıldı, controller başına verildi. CSV dışa aktarım yok. |
| 2026-10-01 | **UIPI çözümü** (§ 12.6): yönetici pencerelerine (Görev Yöneticisi) kontrol için istemci yükseltilmiş çalışır — Görev Zamanlayıcı "en yüksek ayrıcalıkla" oturum-açılışı görevi (`Deploy/Install-StartupTask.ps1`, UAC sormaz). `requireAdministrator` ve `uiAccess` elendi (biri UAC sorar, biri imza ister). Yetki başlangıç günlüğüne yazılır (`ProcessElevation`). |
| 2026-10-01 | **Monitörler kendiliğinden yenilenir** (§ 9.1/§ 9.3): istemci `SessionSwitch` (RDP/konsol geçişi) + 12 sn'lik uzlaştırmayla monitör listesini merkeze yalnızca değişince bildirir. RDP ile ayarlayıp ayrılınca fiziksel monitörlere dönüş elle "yenile" gerekmeden yansır. |
| 2026-10-01 | **Faz 9 — klavye** (§ 12.7): tuş `KeyboardEvent.code` ile gider, PC scan code ile basar (karakteri PC'nin düzeni belirler; tablo Contracts'ta, sunucu tablodışı kodu atar). Tarayıcının yakalayamadığı tuşlar için düğmeler + tam ekranda Keyboard Lock; Ctrl+Alt+Del bilerek yok. Takılı tuş emniyeti düğmelerle aynı kural (5 sn). |
| 2026-09-30 | **Faz 8 — uzaktan fare** (§ 12.7): ayrı izin `RemotePcControl`; kontrol PC başına tek kullanıcı, yalnızca izleyene; aynı kullanıcının başka sekmesi devralır, başka kullanıcı devralamaz; 5 dk girdi yoksa düşer; sanal masaüstü DXGI dikdörtgenlerinden; UIPI için yükseltme yok. Açılışta yetim izleme kayıtları da kapanır (önceden yalnızca oturumlar kapanıyordu). |
| 2026-09-30 | **Faz 7 daraldı** (kullanıcı kararı): kurulum paketi (Inno Setup/MSI) ve Görev Zamanlayıcı görevi yazılmadı — istemci klasöre publish alınıp elle dağıtılır, başlangıç uygulamalarına `--tray` ile eklenir. İstemci günlüğü kullanıcı başına `%LocalAppData%` altında; FFmpeg'siz publish hata verir. |
| 2026-09-30 | **§ 17 kapandı** (kullanıcı kararları): PC bağlantısı kabin durumuna yansımaz; izlendiğine dair onay alınmaz (gösterge + denetim kaydı yeter); izleme yeni `RemotePcView` iznine bağlı — projede **zorlanan ilk izin**, modül uçlarında policy (yoksa 403), frontend'de menü maddesi ve rota da izne bağlı. |
| 2026-09-30 | Faz 4–5: yayın ve izleyiciler bellekte (`ScreenStreamCoordinator`), DB yalnızca denetim. Hazır bekleme 30 sn; bekleyen izleme isteği süpürmede izleyici sayılır (ilk yayın beklerken durdurulmasın). Durdurmada bilet hemen silinir, uymayan yayıncı 10 sn sonra atılır (kick). PC'de "izleniyor" göstergesi zorunlu (pencere şeridi + tepsi ikonu). |
| 2026-09-29 | Faz 1: FFmpeg 8.1 LGPL; `h264_mf` bırakıldı → yazılım yedeği `libopenh264`; QSV `async_depth 1`; çıkış `-fps_mode passthrough` zorunlu; geri kalma bekçisi; tarayıcıda `jitterBufferTarget = 0` (yalnızca PC yayını). Ölçümler § 16.2. |

---

## 19. Uygulama kuralları

1. İstemci dinleyen port açmaz; MediaMTX adresi/parolası istemci config'inde yoktur.
2. PC kimliği çekirdeğin Basic Auth kimliğidir (kullanıcı adı `DeviceId`, hash `PcDevice.CredentialHash` — modül salt okur, doğrulama çekirdekte); cihaz bilgisi modül tablolarına kopyalanmaz.
3. Video ve kontrol kanalları ayrıdır.
4. Komutlar `sessionId` ile idempotenttir; monitör başına tek yayın; bağlantılar yeniden bağlanır.
5. Bilet loglanmaz; FFmpeg stderr maskelenir; sunucuya yalnızca sabit hata metinleri gider.
6. H.264, 4:2:0, B-frame yok; RTSP TCP; çıkışta `-fps_mode passthrough`, QSV'de `-async_depth 1` (§ 7.4).
7. Mevcut kamera akışı (`cam_*`, `CameraService`, `MediaPathCleanupWorker`, `LiveRtspUrl`) değişmez.
8. Önce medya hattı kanıtlanır (Faz 1), sonra kod yazılır.
9. Boşta istemci iş yapmaz: zamanlayıcı, yoklama, gizli pencerede UI güncellemesi yok.
10. Fare/klavye MVP'de uygulanmaz; sözleşmesi (§ 12) Contracts'ta hazırdır.
11. Merkezde CLAUDE.md kuralları geçerlidir: modül kalıbı, `Result<T>`, ProblemDetails, enum sayı, TS aynası
    elle, modül HTTP uçlarında çekirdeğin `Default` rate limit'i (2026-10-26; hub'lar hariç), `companyId`/`IgnoreQueryFilters` yok.
