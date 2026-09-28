# GoraDesk — PC Ekran İzleme ve Uzaktan Kontrol

> **Durum:** Mimari şartname — uygulama başlamadı. Tarih: 2026-09-28.
> **Kapsam:** Sahadaki Windows PC'lere kurulan `GoraDesk` ajanı + Scadex merkezinde onu karşılayan
> `Scadex.RemoteDesk` modülü.
> **Kaynak:** ChatGPT taslağı, Scadex kodunun incelenmesiyle düzeltildi. Taslaktan ayrılan noktalar
> [§ 18](#18-taslaktan-farklar)'de gerekçesiyle listelidir.

Bu doküman Scadex'in kurallarına tabidir: [CLAUDE.md](../CLAUDE.md) ve
[PROJECT_OVERVIEW.md](../PROJECT_OVERVIEW.md) bununla çelişirse **onlar kazanır**. Yorum, log, hata mesajı
ve dokümanlar Türkçe; tip/metot/DTO adları İngilizce.

---

## 1. Amaç ve kapsam

Operatör, Scadex arayüzünden bir PC cihazını seçip ekranını canlı izler (MVP); sonraki aşamada aynı ekrandan
fare ve klavye komutu gönderir.

| | MVP (Faz 0–7) | Sonra (Faz 8+) |
|---|---|---|
| Ekran izleme, birincil monitör | ✅ | |
| İsteğe bağlı yayın (izleyen yoksa FFmpeg çalışmaz) | ✅ | |
| Birden fazla izleyici aynı yayını izler | ✅ | |
| Ajan kimliği, yayın bileti, izleme denetim kaydı | ✅ | |
| Fare / klavye | Sözleşme ve arayüzler hazır, **uygulama yok** | ✅ |
| Kilit ekranı, UAC, Ctrl+Alt+Del | ❌ | ✅ |
| Çoklu monitör, pano, dosya aktarımı, ses, kayıt, otomatik güncelleme | ❌ | ✅ |

---

## 2. Kilit kararlar

| Konu | Karar | Neden |
|---|---|---|
| Ajan dili | **.NET 10** (C#, `net10.0-windows`) | Scadex `net10.0`; SignalR istemcisi, Windows Service ve P/Invoke aynı yığında. Self-contained tek dosya yayın. |
| Süreç modeli | **Windows Service + kullanıcı oturumunda helper** | Servis Session 0'dadır: masaüstünü göremez, `SendInput` yapamaz. |
| Ekran yakalama | **DXGI Desktop Duplication**, FFmpeg `ddagrab` üzerinden | GPU tarafında, düşük CPU, yalnızca değişen kareler. `gdigrab` yalnızca teşhis/geri dönüş. |
| Kodlama | **H.264**, FFmpeg; donanım kodlayıcı öncelikli | Tarayıcı WebRTC'de her yerde çözer. |
| Medya taşıma | **Ajan → MediaMTX, RTSP/TCP publish** (internette **RTSPS**) | MediaMTX zaten çalışıyor, `paths.all_others` zaten `source: publisher`. |
| İzleme | **Mevcut WHEP/WebRTC oynatıcı** (`src/lib/camera/whep.ts`) | Kamera ile aynı kod. |
| Kontrol düzlemi | **SignalR**, ajan dışarı bağlanır (`/hubs/agent`) | NAT/CGNAT sorunu yok; ajan port açmaz. |
| Fare/klavye (sonra) | Tarayıcı → SignalR → **sunucu (yetki + denetim)** → SignalR → ajan → `SendInput` | Yetki ve kayıt tek noktada; P2P WebRTC data channel gereksiz karmaşıklık. |
| Merkezdeki yeri | **Müşteri modülü: `Scadex.RemoteDesk`**, `Modules:RemoteDesk:Enabled` | Kurulum başına açılır; kapalıyken RTSPS dışa açılmaz, uçlar 404. (Açık karar: [§ 17](#17-açık-kararlar)) |

Reddedilen alternatifler (tekrar açılmasın diye):

- **WebSocket üzerinden JPEG/PNG kareleri:** 1080p'de 5–10 Mbps, delta/tıkanıklık kontrolü sıfırdan yazılır.
- **PC ↔ tarayıcı doğrudan WebRTC (P2P):** Her PC'de WebRTC yığını, STUN/TURN, sinyalleşme gerekir. MediaMTX
  zaten SFU gibi davranıp çoklu izleyiciyi tek yayından besliyor.
- **Tek WPF uygulaması:** Session 0 ayrımı yüzünden servis olamaz; kullanıcı kapatınca ajan ölür.

---

## 3. Mimari

```text
 Uzak PC (Windows 10/11)                                  Scadex merkez sunucusu
┌──────────────────────────────────┐                   ┌──────────────────────────────────────────┐
│ GoraDesk.Service  (SYSTEM, S0)    │ ── HTTPS ───────▶ │ POST /api/RemoteDesk/agent/token          │
│  kimlik · SignalR · komutlar      │ ── WSS  ───────▶ │ /hubs/agent            (AgentHub)          │
│  oturum/helper yönetimi           │ ◀── komut ─────── │   StartScreenStream / StopScreenStream     │
│            │ named pipe           │                   │                                            │
│            ▼                      │                   │ /api/MediaGateway/auth (mevcut)             │
│ GoraDesk.Session (kullanıcı ot.)  │                   │   read    cam_* → CameraService (değişmez)  │
│  ddagrab → FFmpeg → H.264         │ ── RTSPS/TCP ──▶ │   publish pc_*  → RemoteDesk yetkilendirici │
│  (sonra) SendInput                │    publish        │                                            │
└──────────────────────────────────┘                   │ MediaMTX  pc_{agentId:N}                   │
                                                       │     │ WHEP (8889) + ICE/UDP (8189)          │
                                                       │     ▼                                      │
                                                       │ Tarayıcı — mevcut whep.ts oynatıcı          │
                                                       └──────────────────────────────────────────┘
```

**Video kanalı ile kontrol kanalı birbirine bağlanmaz.** Video RTSP → MediaMTX → WebRTC; komutlar
SignalR. Fare komutu video paketine gömülmez, pano/dosya RTSP kanalından gitmez.

---

## 4. Ağ

Ajan **hiçbir dinleyen port açmaz**; PC'nin public IP'si bilinmez, NAT/CGNAT/dinamik IP sorun değildir.

| Yön | Protokol | Merkez portu | Not |
|---|---|---|---|
| Ajan → merkez | HTTPS + WSS | 443 (ya da API portu) | Token + AgentHub |
| Ajan → MediaMTX | **RTSPS/TCP** | `:8322` (yeni) | İnternette zorunlu. Aynı LAN/VPN'de RTSP `:8554` kabul edilebilir. |
| Tarayıcı → MediaMTX | WHEP + ICE | 8889/TCP, 8189/UDP | Mevcut, değişmez |

RTSPS gerekçesi: yayın bileti RTSP parolası olarak taşınır. `rtspEncryption: "no"` iken bilet internette
açık metin gider.

`mediamtx.yml` değişikliği: `rtspEncryption: "optional"` + sertifika (`rtspServerKey`/`rtspServerCert`).
`optional` modunda **8554 düz RTSP dinleyicisi aynen kalır**, dolayısıyla Scadex'in kendi FFmpeg'inin
okuduğu `IMediaGateway.LiveRtspUrl` (`rtsp://127.0.0.1:8554`) değişmez. Güvenlik duvarında dışarıya
**yalnızca 8322** açılır, 8554 yerel kalır. yml başındaki "SCADEX SENKRON LISTESI"ne `rtspsAddress` eklenir.

Risk: Yalnızca 443'e izin veren kurumsal ağlarda 8322 kapalı olabilir. O durum MVP kapsamında değil
(çözüm adayı: 443 arkasında TCP proxy ya da WHIP ile publish).

---

## 5. Kimlik ve kimlik doğrulama

### 5.1 AgentId ≠ DeviceId

Her ajanın **kendi** Guid'i vardır (`PcAgent.Id`, "GoraDeskId"). Ajan, diyagramdaki bir PC `Device`'ına
**bağlanır** (`PcAgent.DeviceId`, FK değil — modül kuralı).

Neden `Device.Id` doğrudan kullanılmıyor: `Device`'ın Guid'ini diyagram editörü (istemci) üretir, cihaz
silinip yeniden çizilebilir ve tek yazım yolu diyagram deltasıdır. Kurulumu o kimliğe gömmek, PC başka
kabine taşındığında ya da cihaz yeniden çizildiğinde ajanın yeniden kurulmasını gerektirir. Ayrı kimlikte
yalnızca bağ değişir.

`AgentId` bir **sır değildir**; loglarda, URL'lerde, MediaMTX yol adında görünür.

### 5.2 Ajan sırrı

- Admin ekranında "Ajan ekle" → sunucu `AgentId` + 32 bayt rastgele **sır** üretir, sırrı **bir kez**
  gösterir. Kolaylık için ikisi tek bir "kurulum anahtarı" olarak verilir: `base64url(agentId:secret)`.
- DB'de yalnızca `SHA-256(secret)` tutulur (256 bit entropi; yavaş hash gerekmez).
- "Sırrı yenile" eskisini anında geçersiz kılar; "Devre dışı bırak" ajanın açık bağlantısını da koparır
  (§ 6.3).
- Ajan tarafında sır, `%ProgramData%\GoraDesk\credential.bin` içinde **DPAPI (LocalMachine)** ile şifreli,
  dosya ACL'i SYSTEM + Administrators. Sır ve bilet **asla loglanmaz**.
- Sonraki aşama: tek kullanımlık eşleştirme kodu (`POST /api/RemoteDesk/agent/enroll`) ya da istemci
  sertifikası. MVP'de gerek yok.

### 5.3 Ajan token'ı — kullanıcı JWT'sinden ayrı

`POST /api/RemoteDesk/agent/token` `{ agentId, secret }` → 15 dk ömürlü JWT (`sub = agentId`,
`role = agent`).

> **Kritik:** Bu token mevcut `TokenSettings.Audience` ile basılırsa, `[Authorize]` taşıyan **bütün
> çekirdek uçlar ajan token'ını kabul eder** (varsayılan JwtBearer yalnızca imza/issuer/audience bakar).

Bu yüzden:
- Ajan token'ı **ayrı audience** ile basılır (ör. `scadex-agent`). Varsayılan şema onu reddeder.
- İkinci bir `JwtBearer` şeması (`"Agent"`) yalnızca bu audience'ı kabul eder; `AgentHub`
  `[Authorize(AuthenticationSchemes = "Agent")]` taşır. Kullanıcı token'ı AgentHub'a giremez.
- Query string'den token okuma yalnızca `/hubs` yolları için (mevcut `OnMessageReceived` kuralı);
  `/hubs/agent` buna uyar, genişletmeye gerek yok. Agent şeması da aynı kısıtla yazılır.
- Token uç noktası anonimdir. 256 bitlik sır kaba kuvveti pratikte imkânsız kıldığı için modül kuralına
  uygun olarak rate limit takılmaz; takılırsa politika adı **modülün kendi kaydında** tanımlanmalı (yoksa
  her istek 500).

### 5.4 TLS

Ajan merkeze yalnızca HTTPS ile bağlanır, sertifika doğrulaması açıktır. Kendinden imzalı iç CA
kullanılıyorsa config'e `CertificateThumbprint` (pinning) eklenir; doğrulamayı kapatan bir anahtar
**konmaz**.

---

## 6. Kontrol düzlemi — `AgentHub`

### 6.1 Bağlantı ömrü

```text
config yükle → sırrı çöz → token al → /hubs/agent bağlan → Register(durum) → komut bekle
kopunca: 1 → 2 → 5 → 10 → 30 sn (üst sınır 30 sn, sonsuz) + token süresi dolmuşsa yenile
```

- **Ayrı bir 30 sn heartbeat mesajı yok.** Canlılık, SignalR'ın kendi `KeepAliveInterval` (15 sn) /
  `ClientTimeoutInterval` (30 sn) mekanizmasıdır; bağlantı = temas. Ajan, durumu **değiştiğinde**
  `ReportStatus` gönderir (DB'ye 30 sn'de bir yazılmaz).
- Sunucu ajan bağlantılarını bellekte tutar (`agentId → connectionId`). **Tek sunucu varsayımı**; yatay
  ölçekleme gerekirse SignalR backplane gerekir — kapsam dışı.

### 6.2 Sözleşme

Sunucu → ajan (`IAgentHubClient`):

| Metot | Gövde |
|---|---|
| `StartScreenStream` | `{ sessionId, publishUrl, publishTicket, video: { maxWidth, fps, bitrateKbps, keyframeSec } }` |
| `StopScreenStream` | `{ sessionId }` |
| `RequestStatus` | — |
| *(Faz 8+)* `Input` | `{ controlSessionId, events: [...] }` (§ 12) |

Ajan → sunucu (`AgentHub` metotları):

| Metot | Gövde |
|---|---|
| `Register` | `{ agentVersion, osVersion, interactiveSession: bool, userName?, screens: [{ index, width, height, primary }], encoders: [...] }` |
| `ReportStatus` | `{ state, sessionId?, failureReason? }` — `state`: `Idle, Starting, Streaming, Stopping, Failed, NoInteractiveSession` |

Kurallar:
- `publishUrl`'yi **sunucu verir** (`rtsps://merkez:8322/pc_{agentId:N}`); ajan config'inde MediaMTX
  adresi/portu/parolası **yoktur**. Ajanın tek bildiği `CentralApiUrl` ve kendi kimliğidir.
- Kontrat tipleri `GoraDesk.Contracts` projesindedir; hem ajan hem modül referans alır.
- JSON Scadex kurallarıyla aynı: camelCase, **enum sayı olarak**, `null` alanlar gövdede kalır. Ajanın
  SignalR istemcisi aynı `JsonSerializerOptions`'la kurulur. Sunucudan gelen `...Utc` damgalarında `Z`
  **yoktur**; ajan bunları UTC kabul eder.

### 6.3 Doğrulama ve idempotency

- Komut yalnızca doğrulanmış bağlantıdan gelir; ajan yine de `sessionId`'yi ve `publishUrl` yolunun
  kendi `agentId`'sine ait olduğunu doğrular.
- `StartScreenStream(S)` S zaten aktifken → yeni FFmpeg açılmaz, mevcut durum raporlanır.
- `StartScreenStream(S2)` S1 aktifken → S1 durdurulur, S2 başlar (ajan başına **tek** yayın).
- `StopScreenStream(bilinmeyen/eski S)` → sessizce başarılı.
- Ajan devre dışı bırakılır ya da sırrı yenilenirse sunucu o ajanın bağlantısını **aktif olarak koparır**
  (bağlantı açıldığında token bir kez doğrulanır; kopartılmazsa token süresi dolsa bile bağlantı yaşar).

---

## 7. Medya düzlemi

### 7.1 Yol adı

`pc_{agentId:N}` — ör. `pc_2e9f4c8e4f8c4a2d8a9d1d7e5f7c1234`. Kameradaki `cam_{id:N}_{profil}` ile aynı
biçim (tiresiz). `mediamtx.yml`'e yol eklenmez: `all_others` zaten `source: publisher`.

`MediaPathCleanupWorker` yalnızca `cam_` önekli yolları aday sayar (`IMediaGateway.IsManagedPathName`);
publisher kaynaklı `pc_` yolları ona görünmez. **Bu worker'a dokunulmaz.**

### 7.2 MediaMTX yetkilendirmesi

Bugün `MediaGatewayController.Auth` `read` dışındaki **her** eylemi reddediyor; kamera tarafında publish
diye bir şey **yoktur** (kameralar MediaMTX tarafından çekilir). Değişiklik:

```text
action=read,    path=cam_*  → CameraService.ValidateStreamTokenAsync   (DEĞİŞMEZ)
action=read,    path=pc_*   → RemoteDesk: okuma bileti
action=publish, path=pc_*   → RemoteDesk: yayın bileti
diğer her şey (cam_* publish dahil) → 401
```

Çekirdek modülü bilmediği için bu, bir genişleme noktasıyla yapılır (`IScadaEventObserver` kalıbı):
Business'ta `IMediaPathAuthorizer { bool CanHandle(string path); Task<bool> AuthorizeAsync(action, path,
password, ct); }`. Controller `cam_` için bugünkü yolu izler, diğerlerini kayıtlı yetkilendiricilere sorar;
modül kapalıyken kayıt yoktur → 401.

### 7.3 Biletler

| | Okuma bileti | Yayın bileti |
|---|---|---|
| Kim alır | Tarayıcı (izleyici) | Ajan (`StartScreenStream` içinde) |
| Bağlı olduğu | yol | yol + `sessionId` + `agentId` |
| Ömür | kısa (WHEP el sıkışması bir kez doğrulanır) | **oturum boyunca geçerli**, oturum bitince silinir |
| Cache anahtar öneki | kameradakinden ayrı | okuma biletinden ayrı |

- Okuma bileti ile publish, yayın bileti ile okuma yapılamaz: **anahtar uzayları ayrıdır**.
- Yayın bileti **tek kullanımlık değildir**: FFmpeg koparsa yeniden bağlanır ve MediaMTX her
  yeniden bağlanmada auth'u tekrar çağırır. Oturum durdurulunca bilet cache'ten silinir → sonraki bağlantı
  401 alır.
- Bilet RTSP parolasıdır: `rtsps://agent:{bilet}@merkez:8322/pc_…`. MediaMTX parolayı
  `MediaMtxAuthDto.Password` alanına koyar (kameradaki okuma biletiyle aynı alan). Bilet base64url'dir,
  URL'de kaçış gerektirmez.
- `pathDefaults.overridePublisher: true` — geçerli bileti olan ikinci bir yayıncı birincisini düşürebilir;
  bilet oturuma bağlı olduğu için kabul edilebilir. Sertleştirmede, durdurulan oturumun hâlâ yayında olan
  bağlantısı MediaMTX API'siyle koparılır.

### 7.4 FFmpeg hattı (ajan)

Yazılım kodlayıcılı referans komut (Faz 1'de doğrulanacak):

```text
ffmpeg -hide_banner -nostats -loglevel warning
  -f lavfi -i "ddagrab=output_idx=0:framerate=30:draw_mouse=1,hwdownload,format=bgra"
  -vf "scale='min(1920,iw)':-2,format=yuv420p"
  -c:v h264_mf -rate_control cbr -b:v 3M -g 60 -bf 0
  -rtsp_transport tcp -f rtsp "rtsps://agent:<BILET>@merkez:8322/pc_<agentId>"
```

Donanım kodlayıcılarda `ddagrab`'ın D3D11 kareleri doğrudan kodlayıcıya verilir (`hwdownload`
kaldırılır, `h264_qsv` için `hwmap`); tam filtre zincirleri Faz 1'de her kodlayıcı için sabitlenir.

Kodlayıcı seçim sırası — ajan açılışta 1 karelik denemeyle hangilerinin çalıştığını bulur, sonucu
`Register`'da bildirir:

```text
h264_nvenc → h264_qsv → h264_amf → h264_mf (Windows Media Foundation)
```

- **`libx264` varsayılan geri dönüş DEĞİLDİR.** GPL'dir; libx264 içeren FFmpeg build'ini müşteriye
  dağıtmak GPL yükümlülüğü doğurur. Ajan **LGPL FFmpeg build'i** taşır; `h264_mf` her Windows'ta vardır.
  GPL build'e geçmek ayrı bir lisans kararıdır.
- **FFmpeg ≥ 6.1** (`ddagrab` 6.0'da geldi).

Tarayıcı uyumluluğu için zorunlu ayarlar:
- **B-frame yok** (`-bf 0`) — WebRTC B-frame taşımaz.
- **4:2:0** (`yuv420p`/`nv12`). Ekran yakalama BGRA verir; 4:4:4 ya da `high444` profili tarayıcıda
  **oynamaz**.
- Profil `main` (sorun çıkarsa `baseline`); GOP ≈ 2 sn — yeni izleyici en geç 2 sn'de görüntü alır.
- 4K ekran 1920 genişliğe küçültülür. Başlangıç: 30 fps, 2–4 Mbps CBR. Değerler ölçümle ayarlanır (§ 16).

### 7.5 FFmpeg süreç yönetimi

- Başlat / nazik durdur (stdin'e `q`) / süre aşımında öldür / beklenmedik çıkışta yeniden dene
  (1 → 2 → 5 → 10 → 30 sn, oturum boyunca).
- stderr satır satır okunur ve structured log'a yazılır; **kaynak adresi (bilet) maskelenir**
  (`rtsps://***@…`) — Scadex'teki `CameraCaptureGateway` ile aynı kural. Sunucuya giden
  `failureReason` sabit metinlerden seçilir, ham stderr gönderilmez.
- stderr'de `401` görülürse (bilet geçersiz / oturum kapandı) yeniden deneme **durur**, `Failed`
  raporlanır.
- Ekran çözünürlüğü değişimi, UAC/kilit ekranına geçiş gibi durumlarda Desktop Duplication erişimi kaybolur
  ve FFmpeg çıkar; yeniden deneme bunu karşılar.
- Ajan kapanırken ve helper ölürken **sahipsiz FFmpeg bırakılmaz** (Windows Job Object:
  `KILL_ON_JOB_CLOSE`).

---

## 8. Yayın oturumu ve izleyiciler

### 8.1 Ayrım

```text
ScreenStreamSession   ajan başına en fazla 1 — FFmpeg'in yaşam süresi
ViewerLease           kullanıcı başına 1+ — "ben izliyorum" kiralaması
RemoteControlSession  (Faz 8+) — "ben kontrol ediyorum", aynı anda tek kullanıcı
```

Bir izleyicinin "Durdur"u **yalnızca kendi kiralamasını bırakır**; başka izleyici varken yayını kesmez.

### 8.2 Başlatma

```text
Tarayıcı ── POST /api/RemoteDesk/devices/{deviceId}/view
Sunucu:
  1. Cihaza bağlı, aktif, bağlı (online) ajanı bul        yoksa 404 / 409-benzeri Validation
  2. Ajanın aktif yayını yoksa: ScreenStreamSession oluştur, yayın bileti üret,
     AgentHub → StartScreenStream
  3. Yol hazır olana kadar bekle (en fazla ~15 sn):
       MediaMTX API'si (IMediaGateway) → pc_… yolu "ready"
     ajan Failed / NoInteractiveSession raporlarsa → anlamlı hata ile dön
  4. ViewerLease oluştur, okuma bileti üret
  → 200 { viewId, whepUrl, token, expirationUtc, leaseRenewSec }
```

Adım 3 zorunludur: publisher yolu yayıncı gelmeden **yoktur**, erken WHEP isteği 404 alır (kamerada
`sourceOnDemand` bekletir, burada öyle bir şey yok). Kamera yanıtı (`StreamTokenDto`) ile aynı biçim
kullanılırsa frontend oynatıcısı değişmeden çalışır.

### 8.3 Kiralama ve otomatik durdurma

- Tarayıcı `POST …/view/{viewId}/renew` çağrısını 15 sn'de bir yapar; `DELETE …/view/{viewId}` ile bırakır.
- 45 sn yenilenmeyen kiralama düşer (sekme çökmesi/kapanması `DELETE` göndermeyebilir).
- Kiralaması kalmayan yayın 10 sn bekleme sonrası `StopScreenStream` alır.
- Yedek emniyet: MediaMTX API'sinde `readers == 0` olan `pc_` yolu 60 sn sonra durdurulur.
- Ajan bağlantısı koparsa aktif oturum `Failed` olur; izleyicinin oynatıcısı kopar ve kullanıcıya
  bildirilir.

### 8.4 Oturum durumu

```text
Created → CommandSent → Streaming → Stopping → Stopped
             └──────────────┴──→ Failed (failureReason)
```

---

## 9. Ajan iç yapısı

### 9.1 Süreçler

| Süreç | Çalıştığı yer | Sorumluluk |
|---|---|---|
| `GoraDesk.Service` | Windows Service, LocalSystem, Session 0, Otomatik başlatma + kurtarma politikası | Config, kimlik, token, SignalR, komutlar, durum makinesi, helper'ın yaşamı |
| `GoraDesk.Session` | Aktif konsol oturumu, kullanıcı token'ıyla, `winsta0\default` | FFmpeg yönetimi, (sonra) `SendInput` |
| `GoraDesk.UI` *(opsiyonel, MVP dışı)* | Kullanıcı oturumu, tepsi ikonu | Durum gösterimi, **"ekranınız izleniyor" göstergesi** |

Servis ekran yakalamaz; helper internete komut için bağlanmaz (yalnızca FFmpeg MediaMTX'e bağlanır). UI iş
mantığı taşımaz; RTSP/FFmpeg/SignalR/SendInput yönetmez.

### 9.2 Windows oturum yönetimi

- Helper başlatma: `WTSGetActiveConsoleSessionId` → `WTSQueryUserToken` → `CreateEnvironmentBlock` →
  `CreateProcessAsUser` (`lpDesktop = "winsta0\\default"`).
- Oturum açık kullanıcı yoksa → `NoInteractiveSession` raporlanır, yayın başlamaz; izleyiciye
  "PC'de açık oturum yok" gösterilir.
- Oturum değişiklikleri (logon/logoff/kullanıcı değiştirme/kilitleme) servis tarafından dinlenir
  (`SERVICE_CONTROL_SESSIONCHANGE`; .NET Worker'da `WindowsServiceLifetime` alt sınıfı ile
  `OnSessionChange`) → helper yeni oturumda yeniden başlatılır, aktif yayın yeniden kurulur.
- Helper kullanıcı oturumu boyunca açık kalır (hızlı başlatma; fare aşamasında da gerekli), çökerse
  servis yeniden başlatır.
- RDP oturumları MVP kapsamında değil (yalnızca konsol oturumu).

### 9.3 Servis ↔ helper: named pipe

- **Sunucu servistir**; ad her helper başlatmada rastgele üretilir (`GoraDesk.{sessionId}.{random}`) ve
  helper'a argümanla verilir.
- ACL: SYSTEM + **o oturumun kullanıcı SID'i** (yalnızca SYSTEM verilirse kullanıcı token'lı helper
  bağlanamaz).
- Servis bağlanan istemcinin PID'ini (`GetNamedPipeClientProcessId`) **kendi başlattığı helper PID'iyle**
  karşılaştırır; eşleşmezse bağlantıyı kapatır. Böylece aynı kullanıcının başka bir süreci helper
  kılığına giremez.
- Komutlar yalnızca servis → helper yönündedir (`StartStream`, `StopStream`, `GetStatus`, sonra
  `Input`); helper yalnızca durum bildirir. Helper servise hiçbir şey yaptıramaz.
- Mesaj biçimi: uzunluk önekli JSON, `GoraDesk.Contracts` tipleri.

### 9.4 Durum makinesi (servis)

```text
Starting → Connecting → Connected ─┬─ Idle ⇄ StartingStream → Streaming → StoppingStream → Idle
                ▲                   └─ Error
                └──── Reconnecting ◀── Disconnected
```

Bağlantı koptuğunda aktif yayın **durdurulur** (sunucu oturumu zaten `Failed` sayar; bağlantı dönünce
izleyici yeniden başlatır).

### 9.5 Yapılandırma

`appsettings.json` (kurulum klasörü):

```json
{
  "GoraDesk": {
    "AgentId": "2e9f4c8e-4f8c-4a2d-8a9d-1d7e5f7c1234",
    "CentralApiUrl": "https://scadex.firma.com",
    "CertificateThumbprint": null,
    "FfmpegPath": "tools/ffmpeg/ffmpeg.exe"
  },
  "Serilog": { "MinimumLevel": "Information" }
}
```

Sır burada **değil**, DPAPI dosyasındadır (§ 5.2). Video profili sunucudan komutla gelir; ajan
config'inde tutulmaz.

### 9.6 Loglama ve teşhis

Serilog, dosyaya (`%ProgramData%\GoraDesk\logs`, dönen dosya). Olaylar: başlama, config, token alındı/
reddedildi, bağlantı kuruldu/koptu, komut alındı/reddedildi, helper başladı/çöktü, FFmpeg
başladı/çıktı (çıkış kodu), yeniden deneme. Durum raporu: sürüm, OS, aktif oturum/kullanıcı, bağlantı,
aktif oturum, kodlayıcı, FFmpeg durumu. (Sonra: CPU/GPU, bitrate, düşen kare.)

### 9.7 Kurulum ve güncelleme

- **Inno Setup** (ya da WiX MSI): dosyalar → kurulum anahtarı sorulur → DPAPI dosyası yazılır →
  `sc create GoraDesk start= auto` + kurtarma politikası (`sc failure`) → servis başlatılır.
  (.NET Worker servis protokolünü uygular; MediaMTX'teki 1053 sorunu burada yok.)
- Binary'ler kod imzalı olmalı (SmartScreen / AV yanlış pozitifleri — uzaktan erişim yazılımları sık
  işaretlenir).
- Otomatik güncelleme MVP dışı: sonra "yeni sürüm" komutu → imzalı paket indir → imzayı doğrula → kur.
  İmzasız binary çalıştırılmaz.

---

## 10. Merkez: `Scadex.RemoteDesk` modülü

Signalization modülünün kalıbı birebir izlenir (CLAUDE.md § "Müşteri modülleri").

### 10.1 Kayıt

- `Modules:RemoteDesk:Enabled` (tanımsız = kapalı). `Program.cs` → `AddControllers()` zincirinde
  `.AddRemoteDeskModule(configuration)` (servisler + ApplicationPart çıkarma) ve `MapHub` yanında
  `app.MapRemoteDeskModule(...)` (`/hubs/agent`).
- Frontend aynası: `VITE_MODULES` içinde `remote-desk`, `src/modules/remote-desk/`.
- Kendi context'i: `RemoteDeskDbContext`, şema `remotedesk`, kendi migration geçmişi
  (`--context RemoteDeskDbContext --output-dir Data/Migrations`).

### 10.2 Tablolar (şema `remotedesk`)

| Tablo | Alanlar | Not |
|---|---|---|
| `PcAgent` | `Id` (=AgentId), `DeviceId?`, `Name`, `SecretHash`, `SecretRotatedUtc`, `IsActive`, `AgentVersion`, `OsVersion`, `LastConnectedUtc`, `LastDisconnectedUtc` | `DeviceId` FK değil; bağlama servis doğrulamasıyla (cihaz var, aktif, `DeviceType.Pc`, başka ajana bağlı değil) |
| `ScreenStreamSession` | `Id`, `AgentId`, `MediaPath`, `Status`, `CreatedUtc`, `StartedUtc`, `StoppedUtc`, `StopReason`, `FailureReason` | |
| `ScreenViewLog` | `Id`, `AgentId`, `SessionId`, `UserId`, `StartedUtc`, `EndedUtc` | **Kim, hangi PC'yi, ne zaman izledi** — MVP'de var |
| `RemoteControlSession` *(Faz 8+)* | `Id`, `AgentId`, `UserId`, `StartedUtc`, `EndedUtc`, `EndReason` | |

Kiralamalar ve ajan bağlantı kaydı **bellektedir**, DB'de değil.

### 10.3 Çekirdekte gereken minimum değişiklik

| Yer | Değişiklik |
|---|---|
| `Scadex.Model/Enums/EntityEnums.cs` | `DeviceType.Pc = 13` + seed'de sistem şablonu (PC diyagramda bir `Device`'tır) |
| `Scadex.Business` | `IMediaPathAuthorizer` genişleme noktası (§ 7.2) |
| `Scadex.WebAPI/Controllers/MediaGatewayController.cs` | `cam_` yolu aynen; diğer yollar yetkilendiricilere; `MediaMtxAuthDto` XML doc'u güncellenir ("yalnızca read" artık doğru değil) |
| `Scadex.WebAPI/Program.cs` | `"Agent"` JwtBearer şeması (§ 5.3), modül kayıt satırları |
| `mediamtx.yml` + `appsettings.json` | RTSPS (§ 4); `Modules:RemoteDesk:PublishBaseUrl` (ör. `rtsps://scadex.firma.com:8322`) |

Çekirdeğe başka dokunulmaz. Modül çekirdek tablolarına **yazmaz**; `Device`'ı `IUnitOfWork` ile
projeksiyonla okur.

### 10.4 Canlılık

Ajan bağlandı/koptu olayı PC cihazının canlılık **kanıtıdır** ve yalnızca `ICabinetStatusService`
üzerinden verilir (CLAUDE.md: `Device.DeviceStatusId`/`LastSeen`'e başka yerden yazılmaz). Gerekirse
servise ajan kanıtı için metot eklenir. Kural gereği kontrol modülü dışındaki cihazın `Offline`'ı kabine
`Warning` olarak yansır — istenmiyorsa bkz. [§ 17](#17-açık-kararlar).

### 10.5 Uç noktalar

| Uç | Kim |
|---|---|
| `POST /api/RemoteDesk/agent/token` | Ajan (anonim + sır) |
| `GET/POST/PUT /api/RemoteDesk/agents…` (listele, ekle→sır, bağla, sır yenile, devre dışı) | Admin |
| `POST /api/RemoteDesk/devices/{deviceId}/view` | İzleyici |
| `POST /api/RemoteDesk/views/{viewId}/renew` · `DELETE /api/RemoteDesk/views/{viewId}` | İzleyici |

Kurallar: `Result<T>` → çıplak DTO, hatalar ProblemDetails; controller'lar `BaseController` değil modülün
temel sınıfından türer; hiçbir metot `companyId` almaz; C# DTO değişince TS aynası elle güncellenir.

### 10.6 Frontend

- `src/modules/remote-desk/`: `api/`, `models/`, `hooks/`, `views/` — sorgu anahtarları
  `['remoteDesk', …]` altında.
- İzleme ekranı mevcut `src/lib/camera/whep.ts` + `stream-session.ts`'i yeniden kullanır; yalnızca bilet
  kaynağı farklıdır. Kiralama yenileme hook'u ekranda yaşar.
- Admin ekranı: ajan listesi, bağlantı durumu, sürüm, "kurulum anahtarı üret".
- Sunucu damgaları için `toUtcDate` / `formatUtcDateTime` kullanılır.

---

## 11. Güvenlik özeti

| Tehdit | Önlem |
|---|---|
| Sahte ajan | Sır + ayrı audience'lı kısa ömürlü JWT; `AgentHub` yalnızca Agent şeması |
| Ajan token'ıyla kullanıcı uçlarına erişim | Ayrı audience — varsayılan şema reddeder |
| Başkasının yoluna yayın / kamera yoluna yayın | Yayın bileti yola + oturuma bağlı; `cam_*` publish her zaman 401 |
| Okuma biletiyle yayın ya da tersi | Ayrı cache anahtar uzayı |
| Bilet dinleme | RTSPS; loglarda maske |
| Yerel süreçten ajana komut | Pipe ACL + PID doğrulaması; komut yönü tek |
| Devre dışı ajan açık bağlantıyla devam | Sunucu bağlantıyı aktif koparır |
| İzlemenin fark edilmemesi | `ScreenViewLog` denetimi; PC'de görünür gösterge (bkz. § 17) |

> **Yetki:** CLAUDE.md'ye göre çekirdekte yetki zorlaması bilinçli olarak yok. Ekran izleme ve
> özellikle uzaktan kontrol bu boşluğun en ağır sonuçlandığı yerdir. Fare/klavye (Faz 8) **en az bir
> yetki kontrolü olmadan açılmamalıdır**; bu, proje sahibinin kararıdır ([§ 17](#17-açık-kararlar)).

---

## 12. Uzaktan kontrol (Faz 8+) — MVP'de yazılmaz, sözleşmesi şimdiden sabit

### 12.1 Kanal

```text
Tarayıcı ── /hubs/remote-desk (kullanıcı JWT) ──▶ sunucu: RemoteControlSession + yetki + denetim
         ──▶ AgentHub.Input ──▶ servis ──pipe──▶ helper ──▶ SendInput
```

- Kontrol aynı anda **tek kullanıcıya** verilir; diğerleri izlemeye devam eder.
- Tarayıcı olayları 16 ms'lik (~60 Hz) paketler halinde gönderir.
- **Hareket için son-durum semantiği:** işlenmemiş `move`'lardan yalnızca sonuncusu gönderilir.
  `down/up/wheel/key` olayları **asla** düşürülmez ve sırası korunur.
- Gerekirse SignalR MessagePack protokolü (daha az bayt); ilk sürümde JSON yeterli.

### 12.2 Olay modeli

```json
{ "controlSessionId": "…", "events": [
  { "seq": 18392, "t": 1780301234567, "type": 1, "x": 0.532, "y": 0.381 },
  { "seq": 18393, "t": 1780301234571, "type": 2, "button": 0 },
  { "seq": 18394, "t": 1780301234650, "type": 3, "button": 0 },
  { "seq": 18395, "t": 1780301234700, "type": 4, "deltaY": -120 },
  { "seq": 18396, "t": 1780301234800, "type": 5, "code": "KeyA" }
]}
```

`type` sayıdır (Scadex enum kuralı): `Move=1, Down=2, Up=3, Wheel=4, KeyDown=5, KeyUp=6`.
`button`: `Left=0, Middle=1, Right=2` (DOM `button` ile aynı).

### 12.3 Koordinatlar

- `x, y` ∈ [0, 1], **yayınlanan monitörün** alanına göre normalize.
- Tarayıcıda normalizasyon video elemanının kutusuna değil, `object-fit: contain` ile **çizilen
  görüntü dikdörtgenine** göre yapılır (letterbox payı çıkarılır).
- Ajan: monitör dikdörtgeni → sanal masaüstü koordinatı → `SendInput`
  (`MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK`, 0–65535 ölçeği).

### 12.4 Klavye

- Tarayıcıdan **`KeyboardEvent.code`** (fiziksel tuş, `"KeyA"`) gönderilir, `key` değil. Ajan bunu
  **scan code**'a çevirip `KEYEVENTF_SCANCODE` ile basar. Böylece tuş, PC'nin kendi klavye düzenine göre
  yorumlanır (Türkçe Q'da `ğ, ş, ı` doğru çıkar). `code → scan code` tablosu `GoraDesk.Contracts`'ta açıkça
  tanımlanır.
- Tarayıcı Win, Alt+Tab, Ctrl+W gibi kombinasyonları yakalayamaz; tam ekranda Keyboard Lock API
  (Chromium) kullanılır, yoksa araç çubuğunda özel düğmeler.
- **Ctrl+Alt+Del `SendInput` ile gönderilemez**: SYSTEM süreçten `SendSAS` + "SoftwareSASGeneration"
  politikası gerekir — ayrı iş.

### 12.5 Takılı tuş emniyeti

Ajan basılı tuş/düğmeleri izler; kontrol oturumu bittiğinde, bağlantı koptuğunda ya da 5 sn olay
gelmezken basılı bir şey kalmışsa **hepsini bırakır**. Tarayıcı `blur`/`visibilitychange`'de de bırakma
gönderir.

### 12.6 Yetki seviyeleri (UIPI)

Kullanıcı token'lı helper, **yönetici olarak çalışan pencerelere** tıklayamaz/yazamaz (UIPI) ve UAC
ekranını göremez. Bunun için helper'ın kullanıcı oturumunda **SYSTEM token'ıyla** (servis token'ı
çoğaltılıp oturum kimliği değiştirilerek) başlatılması ve `winlogon` masaüstüne geçebilmesi gerekir —
AnyDesk/RustDesk'in yaptığı budur. Güvenlik yüzeyini büyüttüğü için Faz 11'e bırakılır.

---

## 13. Çözüm yapısı

```text
Scadex.RemoteDesk/                  merkez modülü — class library, Scadex.slnx'e girer (bu doküman burada)
  Controllers/ Hubs/ Realtime/ Services/ Data/ Model/ ServiceRegistration.cs

GoraDesk/                           ajan — ayrı GoraDesk.slnx, Scadex.slnx'e GİRMEZ
  GoraDesk.Contracts/               net10.0 — hub + pipe mesajları, code→scancode tablosu
                                    (Scadex.RemoteDesk bunu ProjectReference ile alır)
  GoraDesk.Service/                 net10.0-windows — Worker Service
    Identity/ Communication/ Commands/ SessionHost/ State/
  GoraDesk.Session/                 net10.0-windows — helper (WinExe, penceresiz)
    Streaming/ Ffmpeg/ Input/ (Input: arayüz var, uygulama Faz 8)
  GoraDesk.UI/                      opsiyonel, MVP dışı
  GoraDesk.Tests/                   saf mantık: durum makinesi, idempotency, FFmpeg argüman üretimi, maske
  tools/ffmpeg/                     LGPL build, git'te değil (Scadex'teki MediaTools gibi)
  installer/                        Inno Setup betiği
```

Ajan kod kuralları: nullable açık, async + `CancellationToken` her yerde, DI, Options pattern,
`IHttpClientFactory`, Serilog. Scadex'in yorum/dil kuralları ajanda da geçerlidir.

Test notu: **Scadex'te test paketi yoktur**; merkez değişiklikleri `dotnet build Scadex.slnx`,
`npm run lint`, `npm run build` ve uygulamayı çalıştırarak doğrulanır. `GoraDesk.Tests` yalnızca ajan
çözümündedir.

---

## 14. Fazlar

Her faz bir öncekinin başarı kriteri sağlanmadan başlamaz.

| Faz | İş | Başarı kriteri |
|---|---|---|
| **0** | Mevcut altyapıyı oku (bu dokümanın dayandığı dosyalar) | Değişecek/yeniden kullanılacak dosya listesi onaylı |
| **1** | **Medya hattı kanıtı** — ajan yazmadan, elle FFmpeg ile `pc_test`'e publish (geçici olarak auth'suz test yolu ya da yerel MediaMTX) → WHEP ile tarayıcıda izle. Her kodlayıcı için filtre zincirini sabitle. | Görüntü tarayıcıda; gecikme < 500 ms; `h264_mf` ve en az bir donanım kodlayıcı çalışıyor |
| **2** | Modül iskeleti + `PcAgent` + token ucu + Agent JWT şeması + `IMediaPathAuthorizer` + publish bileti | Geçerli bilet 200; süresi dolmuş/yanlış yol/yanlış oturum/okuma bileti 401; `cam_*` publish 401; kamera izleme bozulmadı |
| **3** | `GoraDesk.Service`: config, DPAPI, token, SignalR, yeniden bağlanma, durum raporu | Ajan admin ekranında "bağlı" görünüyor; ağ kesilip gelince yeniden bağlanıyor |
| **4** | `GoraDesk.Session`: FFmpeg yönetimi, yerel test komutuyla yayın | Helper tek başına `pc_…`'ya yayın yapıyor |
| **5** | Servis ↔ helper: oturum algılama, `CreateProcessAsUser`, named pipe, oturum değişimi | Logoff/logon sonrası yayın yeniden kurulabiliyor |
| **6** | Uçtan uca: `view` ucu, hazır-bekleme, kiralama, otomatik durdurma, `ScreenViewLog` | Tarayıcı kapatılınca ≤ 60 sn'de FFmpeg duruyor; iki izleyici aynı yayını görüyor |
| **7** | Frontend: izleme ekranı + admin ajan ekranı; kurulum paketi | Temiz bir PC'ye kurulum anahtarıyla kurulup izlenebiliyor |
| **8** | Fare (§ 12) + yetki kararı + `RemoteControlSession` | |
| **9** | Klavye (scan code, değiştirici tuşlar, takılı tuş emniyeti) | |
| **10** | Çoklu monitör (`output_idx`), pano | |
| **11** | Kilit ekranı / UAC / SendSAS, otomatik güncelleme, ses, kayıt | |

---

## 15. Test matrisi

**Ajan:** geçersiz config · geçersiz AgentId · merkez erişilemez · token reddi (yanlış sır, devre dışı
ajan) · yeniden bağlanma · servis yeniden başlatma · açık oturum yok · logoff/logon/kullanıcı değiştirme
· helper çökmesi · FFmpeg çökmesi · sahipsiz FFmpeg kalmaması.

**Yayın:** başlat · durdur · çift başlat · çift durdur · farklı oturumla başlat · MediaMTX kapalı ·
ağ kesintisi · tarayıcı sekmesi öldürülür (kiralama düşer) · iki izleyici, biri çıkar · çözünürlük
değişimi · 4K ekran.

**Güvenlik:** geçersiz/süresi dolmuş/başka oturumun yayın bileti · okuma biletiyle publish · `cam_*`
publish · kullanıcı JWT'siyle AgentHub · ajan JWT'siyle çekirdek uç · yabancı süreçle pipe bağlantısı ·
loglarda bilet/sır olmaması.

**Kontrol (Faz 8+):** hareket · tıklama · sürükleme · tekerlek · Türkçe karakterler · Ctrl/Alt/Shift/Win ·
sıra · kopuk bağlantıda takılı tuş · yetkisiz kullanıcı · ikinci kullanıcının kontrol istemesi · letterbox
kenarında koordinat.

---

## 16. Performans ölçümü

Ölçülecekler: CPU, GPU, bellek, bant genişliği, fps, düşen kare, uçtan uca gecikme.

Gecikme: PC'de milisaniye gösteren bir saat açılır; PC ekranı ile tarayıcıdaki görüntü aynı karede
fotoğraflanır. Başlangıç hedefi **< 500 ms** (LAN'da 150–300 ms beklenir). Üretim hedefi gerçek ağ ve
donanım ölçümünden sonra konur.

---

## 17. Açık kararlar

Proje sahibinin vermesi gereken kararlar — önerilen seçenek ilk sıradadır:

1. **Modül mü çekirdek mi?** Öneri: modül (`Modules:RemoteDesk:Enabled`). Çekirdek olursa `IMediaPathAuthorizer`
   genişleme noktası gereksizleşir, fakat her kurulumda RTSPS/AgentHub gelir.
2. **PC çevrimdışı olunca kabin `Warning`'e düşsün mü?** Öneri: evet (mevcut cihaz kuralı). Hayırsa,
   Signalization gibi kabin durumuna **bilerek** yansımaz ve bu CLAUDE.md'ye yazılır.
3. **PC'de izlendiğine dair görünür gösterge / onay.** Öneri: MVP'de en azından tepsi göstergesi ya da ekran
   kenarı çerçevesi (çalışan izleme — KVKK). Onay istemek saha PC'lerinde (kimse başında değilken)
   işlevi öldürebilir; kurulum başına ayar olabilir.
4. **Yetki.** Öneri: MVP'de izleme herkese açık + `ScreenViewLog`; Faz 8'den önce en az "uzaktan kontrol"
   rolü zorunlu.
5. **Ağ.** PC'ler merkeze internetten mi, VPN/LAN'dan mı ulaşıyor? İnternetse RTSPS + 8322 dışa açık
   (öneri); VPN/LAN ise RTSP 8554 yeterli.
6. **FFmpeg lisansı.** Öneri: LGPL build + `h264_mf`. `libx264` kalitesi istenirse GPL yükümlülükleri
   ayrıca değerlendirilir.

---

## 18. Taslaktan farklar

ChatGPT taslağının Scadex koduna karşı düzeltilen yerleri:

| Taslak | Bu doküman | Gerekçe |
|---|---|---|
| .NET 9 | **.NET 10** | Scadex projeleri `net10.0` |
| "Kamera publish davranışı bozulmamalı" | Kamerada publish **yok**; bugün tüm publish reddediliyor | `MediaGatewayController.Auth` yalnızca `read` kabul ediyor; kameralar çekiliyor |
| GoraDeskId = merkezdeki cihaz Guid'i | Ayrı `AgentId`, cihaza **bağlanır** | Device Guid'i editör üretir, cihaz yeniden çizilebilir |
| `pc_{deviceId}` (tireli) | `pc_{agentId:N}` | Kamera yol biçimiyle aynı; yayın ajanın |
| Yayın bileti "single-purpose" | Oturum boyu geçerli, tek kullanımlık değil; okuma/yayın anahtar uzayları ayrı | FFmpeg yeniden bağlanınca MediaMTX auth'u tekrar sorar |
| Ajan için JWT, ayrıntı yok | **Ayrı audience + ayrı şema** | Aynı audience'la mevcut `[Authorize]` uçların hepsi ajan token'ını kabul ederdi |
| 30 sn heartbeat | SignalR keep-alive + değişince durum; canlılık `ICabinetStatusService` | DB'ye periyodik yazım gereksiz; CLAUDE.md canlılık kuralı |
| Frontend "Stop" yayını durdurur | Kiralama; son izleyici gidince durur | Çoklu izleyicide başkasının görüntüsü kesilirdi |
| Başlat → hemen WHEP | Yol hazır olana kadar sunucu bekler | Publisher gelmeden yol yok, WHEP 404 |
| RTSP/TCP | İnternette **RTSPS** | Bilet parolada açık metin giderdi |
| Fallback `libx264` | `h264_mf`, LGPL build | libx264 GPL |
| — | 4:2:0 ve B-frame yok zorunlu | BGRA yakalama 4:4:4'e kayarsa tarayıcı oynatmaz |
| Pipe ACL: SYSTEM + Admins | SYSTEM + oturum kullanıcısı + PID doğrulaması | Kullanıcı token'lı helper aksi halde bağlanamaz |
| Klavye `key: "A"` | `code` → scan code | Klavye düzeni (Türkçe Q) PC'de uygulanmalı |
| — | UIPI, SendSAS, takılı tuş emniyeti | AnyDesk benzeri kontrolün bilinen engelleri |
| Her fazda unit + integration test | Merkezde build/lint + çalıştırarak doğrulama; testler yalnızca ajanda | Scadex'te test paketi yok |
| WPF UI mimaride | MVP dışı; ileride yalnızca tepsi/gösterge | İş mantığı taşımaz |
| Merkez tarafı yeri belirsiz | `Scadex.RemoteDesk` modülü | Signalization kalıbı; kurulum başına açma |

---

## 19. Uygulayıcı için kurallar

1. Ajan dinleyen port açmaz; PC public IP gerektirmez.
2. `AgentId` sır değildir; MediaMTX için kalıcı parola ajana gömülmez.
3. Video ve kontrol kanalları ayrıdır.
4. Servis ekran yakalamaz; yakalama ve `SendInput` kullanıcı oturumundaki helper'dadır.
5. Komutlar `sessionId` ile idempotenttir; bağlantılar yeniden bağlanır.
6. Sır ve bilet loglanmaz; FFmpeg stderr maskelenir.
7. H.264, 4:2:0, B-frame yok; RTSP(S) TCP.
8. Mevcut kamera akışı (`cam_*`, `CameraService`, `MediaPathCleanupWorker`, `LiveRtspUrl`) değişmez.
9. Önce medya hattı kanıtlanır (Faz 1), sonra ajan yazılır.
10. Fare/klavye MVP'de uygulanmaz; sözleşmesi (§ 12) şimdiden `GoraDesk.Contracts`'tadır.
11. Merkezde CLAUDE.md kuralları geçerlidir: modül kalıbı, tek yazım yolu, `Result<T>`, ProblemDetails,
    enum sayı, TS aynası elle, rate limit politika adı tanımlı değilse 500.

Her faz sonunda rapor: değişen dosyalar · eklenen özellikler · mimari değişiklik · nasıl doğrulandı ·
nasıl çalıştırılır · bilinen kısıtlar · sonraki adım.
