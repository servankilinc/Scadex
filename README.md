# Scadex

Sahadaki SCADA'nın **üstünde** çalışan süpervizyon platformu: kabinleri, pano şemalarını (diyagram), giriş/çıkış kanallarını,
kameraları ve sahadaki PC'leri tek ekrandan izler ve yönetir.

Geliştirme komutları ve mimari kurallar [CLAUDE.md](CLAUDE.md)'de, sistemin ne yaptığı ve kararların gerekçeleri
[PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)'dedir. RemoteDesk modülünün şartnamesi [RemoteDesk.md](RemoteDesk.md), SCADA protokolü
[Docs/scada_communication_guide.md](Docs/scada_communication_guide.md). **Bu dosya sahaya kurulumu anlatır.**

## İçindekiler

- [Genel bakış](#genel-bakış)
  - [Bileşenler](#bileşenler) · [Örnek değerler](#örnek-değerler) · [Kim nereye bağlanır](#kim-nereye-bağlanır) · [Yapılandırma dosyaları](#yapılandırma-dosyaları)
- [1. Ortak hazırlık](#1-ortak-hazırlık)
  - [1.1 Önkoşullar](#11-önkoşullar)
  - [1.2 Sertifikalar](#12-sertifikalar)
  - [1.3 Güvenlik duvarı](#13-güvenlik-duvarı)
  - [1.4 HTTP ile kurulum (sertifikasız)](#14-http-ile-kurulum-sertifikasız)
- [2. Kurulum — proje proje](#2-kurulum--proje-proje)
  - [2.1 Veritabanı (SQL Server)](#21-veritabanı-sql-server)
  - [2.2 API — Scadex.WebAPI (IIS)](#22-api--scadexwebapi-iis)
  - [2.3 Arayüz — Scadex.WebUI (IIS)](#23-arayüz--scadexwebui-iis)
  - [2.4 RemoteDesk PC istemcisi — Windows servisi](#24-remotedesk-pc-istemcisi--windows-servisi)
  - [2.5 Saha cihazları ve istemciler](#25-saha-cihazları-ve-istemciler)
- [3. Bakım ve işletme](#3-bakım-ve-işletme)
  - [3.1 Windows'ta ne nasıl çalışır](#31-windowsta-ne-nasıl-çalışır)
  - [3.2 Güncelleme sırası](#32-güncelleme-sırası)
  - [3.3 Kontrol listesi ve sık hatalar](#33-kontrol-listesi-ve-sık-hatalar)
- [Geliştirme ortamı](#geliştirme-ortamı)

---

## Genel bakış

### Bileşenler

```text
                         ┌──────────────── Sunucu (Windows + IIS) ────────────────┐
 Operatör tarayıcısı ──► │ Arayüz (IIS, :3000)                                     │
         │               │ API (IIS, :3009) ──┬─► SQL Server                       │
         └─ canlı görüntü│                    └─► MediaMTX (:8889 / :8554)          │
            ───────────► │                         (API başlatır, servis DEĞİL)    │
                         │                         └─► Kameralar (RTSP)            │
                         └─────────────────────────────────────────────────────────┘
 SCADA kontrol kartı ──► API :3009  (DeviceId header'ı: kartın MAC adresi)
 RemoteDesk PC ────────► API :3009 (Basic Auth) + MediaMTX :8554 (ekran yayını)
                         PC'de: ScadexRemoteDesk Windows servisi + Agent + System Tray arayüzü
```

| Bileşen | Ne | Nasıl çalışır | Kurulum |
|---|---|---|---|
| SQL Server | Veritabanı (`dbo`, `signalization`, `remotedesk` şemaları) | Ayrı kurulur | [2.1](#21-veritabanı-sql-server) |
| API (`Scadex.WebAPI`) | REST + SignalR, iş mantığı | IIS uygulama havuzu | [2.2](#22-api--scadexwebapi-iis) |
| MediaMTX | Kamera ve PC canlı görüntüsü (RTSP → WebRTC) | API'nin alt süreci; API başlatır, kapanırsa yeniden başlatır | [2.2.6](#226-mediamtx-ve-ffmpeg) |
| FFmpeg | Kameradan anlık görüntü / klip | API gerektiğinde çağırır | [2.2.6](#226-mediamtx-ve-ffmpeg) |
| Arayüz (`Scadex.WebUI`) | React tek sayfalık uygulama | IIS'te statik site | [2.3](#23-arayüz--scadexwebui-iis) |
| RemoteDesk PC istemcisi | Sahadaki PC'nin ekranını paylaşır | Windows servisi `ScadexRemoteDesk` + oturumda Agent + System Tray arayüzü | [2.4](#24-remotedesk-pc-istemcisi--windows-servisi) |
| SCADA kontrol kartı | Kabindeki giriş/çıkış kartı | API'ye `DeviceId` (MAC) header'ıyla veri gönderir | [2.5.1](#251-scada-kontrol-kartı) |
| Operatör bilgisayarı | Tarayıcı | Kurulum yok, yalnızca Root CA | [2.5.2](#252-operatör-bilgisayarları) |

Sistem alan adı olmadan, sunucunun yerel ağ IP'si üzerinden çalışır. Bu rehber **HTTPS** kurulumunu anlatır (önerilen): sertifikayı kendi
yerel kök sertifikamız (Root CA) imzalar, kök sertifika sunucuya ve her istemci bilgisayara güvenilir olarak yüklenir. **Kapalı ağda HTTP de
olur** — sertifikasız kurulumun farkları ve riskleri [§ 1.4](#14-http-ile-kurulum-sertifikasız)'tedir. İkisini karıştırmayın: HTTPS sayfadan
HTTP'ye (ör. MediaMTX'e) istek tarayıcıda engellenir; ya her şey HTTPS ya her şey HTTP olmalı.

### Örnek değerler

Aşağıdaki tüm komutlar bu değerlerle yazılmıştır. Sahada farklıysa **her yerde** değiştirin. Tüm PowerShell komutları **yönetici
olarak** çalıştırılır.

| Ne | Değer |
|---|---|
| Sunucu IP | `172.16.42.106` |
| API | `https://172.16.42.106:3009` |
| Arayüz (React) | `https://172.16.42.106:3000` |
| MediaMTX WebRTC (canlı izleme) | `https://172.16.42.106:8889` |
| MediaMTX RTSP (PC ekran yayını) | `rtsp://172.16.42.106:8554` |
| MediaMTX ICE (görüntü verisi) | `8189/udp` |
| MediaMTX Control API | `127.0.0.1:9997` — yalnızca sunucu içi, **dışarı açılmaz** |
| API klasörü | `C:\inetpub\scadex\api` |
| Arayüz klasörü | `C:\inetpub\scadex\web` |
| Sertifika klasörü | `C:\certs` |
| Derleme çıktısı (paket) | `C:\paket` |
| RemoteDesk PC kurulum klasörü | `C:\Program Files\Scadex\RemoteDesk` |

### Kim nereye bağlanır

| Bağlanan | Hedef | Ayar |
|---|---|---|
| Operatörün tarayıcısı | Arayüz `:3000` | IIS binding |
| Arayüz (tarayıcıda) | API `:3009` | `.env.production > VITE_API_URL`, API'de `Cors:Origins` |
| Operatörün tarayıcısı | MediaMTX `:8889` + `8189/udp` (kamera ve PC izleme) | `MediaGateway:WebRtcPublicBaseUrl` |
| SCADA kontrol kartı | API `:3009` (`/api/Scada/ingest`, `/api/Scada/card`) | kartın kendi ayarı + diyagramda üretilen sır |
| API | SCADA kontrol kartı (çıkış komutu) | kabin ekranında SCADA adresi |
| Sahadaki PC (RemoteDesk) | API `:3009` | PC'de `Service\appsettings.json > CentralApiUrl` |
| Sahadaki PC (RemoteDesk) | MediaMTX `:8554` (ekran yayını) | API'de `Modules:RemoteDesk:PublishRtspBaseUrl` |
| MediaMTX | API `:3009` (yetki kontrolü) | `mediamtx.yml > authHTTPAddress` |
| MediaMTX | Kameralar (RTSP) | kamera kaydı |

### Yapılandırma dosyaları

| Dosya | Nerede | Ne zaman okunur | Anahtarlar |
|---|---|---|---|
| `appsettings.json` | `C:\inetpub\scadex\api\` | API açılışında; değişince havuz yeniden başlatılır | [2.2.4](#224-appsettingsjson--tüm-anahtarlar) |
| `mediamtx.yml` | `C:\inetpub\scadex\api\MediaTools\mediamtx\` | MediaMTX açılışında; değişince MediaMTX sonlandırılır | [2.2.6](#226-mediamtx-ve-ffmpeg) |
| `.env.production` | Derleme makinesinde `Scadex.WebUI\` | **Derleme anında** — değişince arayüz yeniden derlenir | [2.3.1](#231-envproduction--tüm-anahtarlar) |
| `web.config` (arayüz) | `C:\inetpub\scadex\web\` | IIS | [2.3.3](#233-webconfig-ve-site) |
| `appsettings.json` (RemoteDesk) | PC'de `…\RemoteDesk\Service\` | Agent açılışında; değişince servis yeniden başlatılır | [2.4.4](#244-kopyalama-ve-appsettingsjson--tüm-anahtarlar) |

Depodaki ayar dosyaları **geliştirme** değerleri taşır (yerel veritabanı parolası, `SecurityKey`, `localhost` adresleri). Sahada bu
değerlerin hiçbirini olduğu gibi bırakmayın.

---

## 1. Ortak hazırlık

### 1.1 Önkoşullar

İki makine düşünülür: **derleme makinesi** (kaynak koddan yayın alınan bilgisayar) ve **sunucu**. Aynı makine de olabilir.

**Derleme makinesi:** .NET 10 SDK, `dotnet-ef` aracı (`dotnet tool install --global dotnet-ef`), Node.js + npm.

**Git'te olmayan, elle tutulan dosyalar** (büyük ya da lisanslı; derleme makinesinde yerindeyse yayına kendiliğinden kopyalanır):

| Dosya | Yer | Not |
|---|---|---|
| `ffmpeg.exe` + `LICENSE.txt` | `Scadex.WebAPI\MediaTools\ffmpeg\` | **LGPL build** (BtbN 8.1 LGPL). GPL build koymayın — müşteriye dağıtımda kaynak sunma yükümlülüğü doğar |
| `mediamtx.exe` + `mediamtx.yml` | `Scadex.WebAPI\MediaTools\mediamtx\` | yml olmadan MediaMTX başlatılmaz |
| Harita zemini | `Scadex.WebAPI\MapTiles\` | `basemap.pmtiles` (Protomaps şeması, Türkiye z0–13) + `fonts\Noto Sans {Regular,Medium,Italic}` + `sprites\v4\{light,dark}` |
| `ffmpeg.exe` + `LICENSE.txt` (RemoteDesk) | `Modules\RemoteDesk\Scadex.RemoteDesk.Windows.Service\tools\ffmpeg\` | Sunucudakiyle aynı LGPL exe; yoksa RemoteDesk yayını bilerek hata verir |

**Sunucu:**

- **IIS** ve şu özellikler (WebSockets SignalR için, ApplicationInit API'nin isteksiz de ayakta kalması için):

  ```powershell
  Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets, IIS-ApplicationInit
  iisreset
  ```

- **.NET 10 Hosting Bundle** — IIS'ten **sonra** kurulmalı; önce kurulduysa yeniden çalıştırıp *Repair*. Eksikse site
  `500.19 / 0x8007000d` verir.
- **URL Rewrite** modülü (arayüzün `web.config`'i için).
- **SQL Server** (aynı ya da ayrı makinede; Express de olur).
- **OpenSSL** (ör. Win64 OpenSSL Light) — yalnızca sertifika üretimi için, PATH'e eklenir:

  ```powershell
  Test-Path "C:\Program Files\OpenSSL-Win64\bin\openssl.exe"

  $opensslPath = "C:\Program Files\OpenSSL-Win64\bin"
  [Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + $opensslPath, "Machine")
  ```

  Yeni PATH yalnızca **yeni açılan** PowerShell pencerelerinde geçerlidir.

**Sahadaki cihazlar:** RemoteDesk PC'leri Windows 10/11 64 bit — önkoşul yok (istemci .NET'i içinde taşır). SCADA kontrol kartları
API'ye HTTPS + `DeviceId` (MAC adresi) başlığıyla istek atabilmeli ([2.5.1](#251-scada-kontrol-kartı)).

### 1.2 Sertifikalar

#### Çalışma klasörü

```powershell
mkdir C:\certs
cd C:\certs
```

#### Yerel Root CA

```powershell
openssl genrsa -out local-root-ca.key 4096

openssl req -x509 -new -nodes `
  -key local-root-ca.key `
  -sha256 `
  -days 3650 `
  -out local-root-ca.crt `
  -subj "/C=TR/O=Local Development/CN=Local Development Root CA"
```

- `local-root-ca.key` Root CA'nın özel anahtarıdır. Bunu ele geçiren biri, kök sertifikayı yüklemiş tüm bilgisayarların güveneceği
  sahte sertifikalar üretebilir. **Sunucuda bırakmayın, güvenli bir yerde saklayın.**
- `local-root-ca.crt` yaklaşık 10 yıl geçerlidir.

#### Sunucu sertifikası

Anahtar ve CSR:

```powershell
openssl genrsa -out scadex-server.key 2048

openssl req -new `
  -key scadex-server.key `
  -out scadex-server.csr `
  -subj "/C=TR/O=Local Development/CN=172.16.42.106"
```

Tarayıcılar CN'e değil SAN alanına bakar. `notepad C:\certs\scadex-server.ext` ile uzantı dosyasını oluşturun:

```ini
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=@alt_names

[alt_names]
IP.1=172.16.42.106
```

İmzala ve kontrol et:

```powershell
openssl x509 -req `
  -in scadex-server.csr `
  -CA local-root-ca.crt `
  -CAkey local-root-ca.key `
  -CAcreateserial `
  -out scadex-server.crt `
  -days 825 `
  -sha256 `
  -extfile scadex-server.ext

openssl x509 -in scadex-server.crt -text -noout
```

Çıktıda şunlar bulunmalı:

```
X509v3 Key Usage:               Digital Signature, Key Encipherment
X509v3 Extended Key Usage:      TLS Web Server Authentication
X509v3 Subject Alternative Name: IP Address:172.16.42.106
```

Sunucu sertifikası 825 gün geçerlidir; süresi dolmadan bu adım ve sonraki iki adım tekrarlanır (Root CA aynı kalır, istemcilere yeniden
yüklenmez).

#### PFX ve Windows sertifika deposu

```powershell
openssl pkcs12 -export `
  -out scadex-server.pfx `
  -inkey scadex-server.key `
  -in scadex-server.crt `
  -certfile local-root-ca.crt `
  -name "Scadex Local Network API HTTPS"
```

Sorulan PFX parolası yüklemede gerekir:

```powershell
# Root CA → Güvenilen Kök
Import-Certificate -FilePath "C:\certs\local-root-ca.crt" -CertStoreLocation "Cert:\LocalMachine\Root"

# Sunucu sertifikası → Kişisel
$pwd = Read-Host "PFX Password" -AsSecureString
Import-PfxCertificate -FilePath "C:\certs\scadex-server.pfx" -CertStoreLocation "Cert:\LocalMachine\My" -Password $pwd

# Kontrol
Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -like "*Local Development Root CA*" } | Select-Object Subject, Thumbprint, NotAfter
Get-ChildItem Cert:\LocalMachine\My | Select-Object Subject, FriendlyName, Thumbprint, HasPrivateKey
```

`LocalMachine\My` altında `FriendlyName = Scadex Local Network API HTTPS`, `HasPrivateKey = True` görünmeli. IIS'in *Server Certificates*
ekranında yalnızca bu sertifika görünür; Root CA orada görünmez, bu normaldir.

#### Root CA'nın dağıtımı

`local-root-ca.crt` sunucunun dışında şu makinelere de aynı komutla yüklenir:

```powershell
Import-Certificate -FilePath "C:\certs\local-root-ca.crt" -CertStoreLocation "Cert:\LocalMachine\Root"
```

| Nereye | Neden |
|---|---|
| Her operatör bilgisayarı | Tarayıcı arayüze, API'ye ve MediaMTX'e HTTPS ile bağlanır ([2.5.2](#252-operatör-bilgisayarları)) |
| Her RemoteDesk PC'si | Agent merkeze HTTPS ile bağlanır ([2.4](#24-remotedesk-pc-istemcisi--windows-servisi)) |
| SCADA kontrol kartları | Kart API'ye HTTPS ile bağlanır; yükleme yöntemi kartın kendi arayüzündedir ([2.5.1](#251-scada-kontrol-kartı)) |

Etki alanı varsa Windows makinelere Grup İlkesi ile dağıtılabilir (*Ortak Anahtar İlkeleri → Güvenilen Kök Sertifika Yetkilileri*).

### 1.3 Güvenlik duvarı

Sunucuda:

```powershell
New-NetFirewallRule -DisplayName "Scadex API 3009"    -Direction Inbound -Protocol TCP -LocalPort 3009 -Action Allow
New-NetFirewallRule -DisplayName "Scadex Web 3000"    -Direction Inbound -Protocol TCP -LocalPort 3000 -Action Allow
New-NetFirewallRule -DisplayName "MediaMTX WebRTC"    -Direction Inbound -Protocol TCP -LocalPort 8889 -Action Allow
New-NetFirewallRule -DisplayName "MediaMTX ICE"       -Direction Inbound -Protocol UDP -LocalPort 8189 -Action Allow
New-NetFirewallRule -DisplayName "MediaMTX RTSP (PC)" -Direction Inbound -Protocol TCP -LocalPort 8554 -Action Allow
```

| Port | Kim bağlanır |
|---|---|
| 3009/tcp | Operatör tarayıcıları, SCADA kontrol kartları, RemoteDesk PC'leri |
| 3000/tcp | Operatör tarayıcıları |
| 8889/tcp, 8189/udp | Operatör tarayıcıları (canlı görüntü) |
| 8554/tcp | RemoteDesk PC'leri (ekran yayını) — RemoteDesk kapalıysa açmayın |
| 9997/tcp | **Açılmaz** (MediaMTX yönetim API'si, yalnızca sunucu içi) |

Kuralları `-RemoteAddress` ile ilgili alt ağlara daraltmak önerilir (ör.
`Set-NetFirewallRule -DisplayName "MediaMTX RTSP (PC)" -RemoteAddress 10.20.0.0/16`).

Giden yönde: RemoteDesk PC'leri **hiçbir dinleyen port açmaz** (yalnızca sunucuya 3009 ve 8554); sunucunun kameraların RTSP portuna
(genellikle 554) ve kontrol kartlarının HTTP adresine ([2.5.1](#251-scada-kontrol-kartı)) erişmesi gerekir.

### 1.4 HTTP ile kurulum (sertifikasız)

Kapalı ağda sertifika kurmadan çalıştırmak için rehberi izleyin ama aşağıdaki farkları uygulayın. Her yerde `https://` yerine `http://`
yazılır; portlar aynıdır.

| Bölüm | HTTPS kurulumu | HTTP kurulumu |
|---|---|---|
| [1.2 Sertifikalar](#12-sertifikalar) | Root CA + sunucu sertifikası, dağıtım | **Atlanır.** Hiçbir makineye kök sertifika yüklenmez |
| [2.2.2 API sitesi](#222-uygulama-havuzu-ve-site) | `New-Website … -Port 3009 -Ssl`, bağlama + *Require SSL* | `New-Website ScadexApi -PhysicalPath C:\inetpub\scadex\api -ApplicationPool ScadexApi -Port 3009` (**`-Ssl` yok**); bağlama ve SSL Settings adımları yok |
| [2.2.4 `appsettings.json`](#224-appsettingsjson--tüm-anahtarlar) | `https://…` | `Cors:Origins` = `[ "http://172.16.42.106:3000" ]`, `MediaGateway:WebRtcPublicBaseUrl` = `http://172.16.42.106:8889` |
| [2.2.6 `mediamtx.yml`](#226-mediamtx-ve-ffmpeg) | `authHTTPAddress: https://…`, `webrtcEncryption: true`, sertifika kopyası | `authHTTPAddress: http://127.0.0.1:3009/api/MediaGateway/auth`, `webrtcEncryption: false`; sertifika kopyalanmaz |
| [2.3 Arayüz](#23-arayüz--scadexwebui-iis) | `VITE_API_URL=https://…`, site `-Ssl` | `VITE_API_URL=http://172.16.42.106:3009`; `New-Website ScadexWeb … -Port 3000` (`-Ssl` yok) |
| [2.4 RemoteDesk PC](#24-remotedesk-pc-istemcisi--windows-servisi) | `CentralApiUrl: https://…`, Root CA | `CentralApiUrl: http://172.16.42.106:3009`; Root CA yok |
| [2.5.1 Kontrol kartı](#251-scada-kontrol-kartı) | `https://…:3009` + Root CA | `http://172.16.42.106:3009` — kartın HTTPS desteği gerekmez |

API sitesinde **yalnızca http bağlaması** olmalı: iki bağlama birden varsa API (`UseHttpsRedirection`) http isteklerini https'e yönlendirir;
MediaMTX'in yetki çağrısı ve cihazlar bu yönlendirmeyle bozulur.

**Operatörün göreceği farklar** (tarayıcılar bazı özellikleri yalnızca HTTPS'te açar):

| Özellik | HTTPS | HTTP |
|---|---|---|
| Giriş, diyagram, kameralar, canlı görüntü, uzaktan kontrol | Tam | Tam |
| RemoteDesk dosya aktarımı — sol (yerel) panel | Klasör gezgini; indirme seçilen klasöre | **Klasik mod:** "Dosya seç" / sürükle-bırak; indirme tarayıcının indirme klasörüne |
| Panolar arası aktarım | Tek tık | **İki adım:** açılan pencereye Ctrl+V / pencereden Ctrl+C (ya da "Kopyala") |
| Tam ekranda Win / Alt+Tab'ın uzağa gitmesi (Keyboard Lock) | Var (Chromium) | Yok — araç çubuğundaki Win / Alt+Tab düğmeleri kullanılır |

**HTTP'de tam deneyim için tarayıcı ayarı (isteğe bağlı, Chrome/Edge).** Tarayıcıya arayüzün adresini güvenli saymasını söylerseniz klasör
gezgini, tek tık pano ve Keyboard Lock HTTP'de de açılır. Firefox'ta karşılığı yoktur; orada klasik mod kalır.

- Tek bilgisayarda: `chrome://flags/#unsafely-treat-insecure-origin-as-secure` (Edge: `edge://flags/#unsafely-treat-insecure-origin-as-secure`)
  → kutuya `http://172.16.42.106:3000` yazın → *Enabled* → tarayıcıyı yeniden başlatın.
- Tüm operatör bilgisayarlarına (yönetici PowerShell ya da Grup İlkesi ile aynı kayıt): `OverrideSecurityRestrictionsOnInsecureOrigin` ilkesi

  ```powershell
  # Chrome
  reg.exe add "HKLM\SOFTWARE\Policies\Google\Chrome\OverrideSecurityRestrictionsOnInsecureOrigin" /v 1 /t REG_SZ /d "http://172.16.42.106:3000" /f
  # Edge
  reg.exe add "HKLM\SOFTWARE\Policies\Microsoft\Edge\OverrideSecurityRestrictionsOnInsecureOrigin" /v 1 /t REG_SZ /d "http://172.16.42.106:3000" /f
  ```

  Tarayıcı yeniden başlatılınca geçerli olur; `chrome://policy` / `edge://policy` sayfasında görünmelidir.

**Kabul edilen riskler.** HTTP'de ağdaki trafik şifresizdir: kullanıcı parolası (giriş anında), oturum token'ı, kontrol kartı ve PC sırları,
canlı görüntü biletleri ve aktarılan dosyalar ağı dinleyen biri tarafından okunabilir. Bu kurulumu yalnızca dışarıya kapalı, ayrı bir ağda
(VLAN) kullanın; güvenlik duvarı kurallarını [1.3](#13-güvenlik-duvarı)'teki gibi alt ağlara daraltın.

---

## 2. Kurulum — proje proje

### 2.1 Veritabanı (SQL Server)

Tek veritabanı, üç şema; her şemanın **kendi migration geçmişi** vardır:

| Şema | DbContext | Proje |
|---|---|---|
| `dbo` (çekirdek) | `AppDbContext` | `Scadex.DataAccess` |
| `signalization` | `SignalizationDbContext` | `Scadex.Signalization` |
| `remotedesk` | `RemoteDeskDbContext` | `Modules\RemoteDesk\Scadex.RemoteDesk` |

**API açılışta migration UYGULAMAZ.** Tablolar ilk kurulumda ve her güncellemede aşağıdaki komutlarla elle oluşturulur/güncellenir.
Kapalı bir modülün şeması da kurulabilir; zararı yoktur.

#### 2.1.1 SQL girişi

Önce SSMS'te boş veritabanını açın (`CREATE DATABASE Scadex;`), sonra iki yoldan birini seçin.

**a) Windows kimliği** — API SQL'e IIS havuzunun kimliğiyle bağlanır. Yalnızca SQL Server **API ile aynı makinedeyse** çalışır:

```sql
CREATE LOGIN [IIS AppPool\ScadexApi] FROM WINDOWS;
USE Scadex;
CREATE USER [IIS AppPool\ScadexApi] FOR LOGIN [IIS AppPool\ScadexApi];
ALTER ROLE db_owner ADD MEMBER [IIS AppPool\ScadexApi];
```

**b) SQL kullanıcı adı/parola** — `sa` kullanmayın, uygulamaya ayrı giriş açın:

```sql
CREATE LOGIN scadex_app WITH PASSWORD = '<güçlü parola>';
USE Scadex;
CREATE USER scadex_app FOR LOGIN scadex_app;
ALTER ROLE db_owner ADD MEMBER scadex_app;
```

Karşılık gelen bağlantı dizeleri ([2.2.4](#224-appsettingsjson--tüm-anahtarlar) > `ConnectionStrings:Database`):

```text
a) Server=localhost;Database=Scadex;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
b) Server=10.0.0.10,1433;Database=Scadex;User Id=scadex_app;Password=<parola>;TrustServerCertificate=True;MultipleActiveResultSets=True
```

#### 2.1.2 Migration'lar

Derleme makinesinde, **depo kökünden**. `dotnet ef` tasarım zamanında Development ayarlarını okur; sahadaki veritabanının bağlantı
dizesini ortam değişkeniyle verin — ayar dosyalarını değiştirmeyin:

```powershell
$env:ConnectionStrings__Database = "Server=10.0.0.10,1433;Database=Scadex;User Id=scadex_app;Password=<parola>;TrustServerCertificate=True;MultipleActiveResultSets=True"

dotnet ef database update --project Scadex.DataAccess --startup-project Scadex.WebAPI --context AppDbContext
dotnet ef database update --project Scadex.Signalization --startup-project Scadex.WebAPI --context SignalizationDbContext
dotnet ef database update --project Modules/RemoteDesk/Scadex.RemoteDesk --startup-project Scadex.WebAPI --context RemoteDeskDbContext

Remove-Item Env:ConnectionStrings__Database
```

- **`--context` zorunludur** (yoksa `More than one DbContext was found`). Sıra: çekirdek önce.
- Uygulanmış migration'lar atlanır; her güncellemede üçünü birden çalıştırmak güvenlidir.

Veritabanına derleme makinesinden ulaşılamıyorsa migration'ları **idempotent SQL betiğine** çevirip SSMS'te numara sırasıyla çalıştırın
(yalnızca eksik migration'ları uygular):

```powershell
dotnet ef migrations script --idempotent --project Scadex.DataAccess --startup-project Scadex.WebAPI --context AppDbContext -o C:\paket\db\01-core.sql
dotnet ef migrations script --idempotent --project Scadex.Signalization --startup-project Scadex.WebAPI --context SignalizationDbContext -o C:\paket\db\02-signalization.sql
dotnet ef migrations script --idempotent --project Modules/RemoteDesk/Scadex.RemoteDesk --startup-project Scadex.WebAPI --context RemoteDeskDbContext -o C:\paket\db\03-remotedesk.sql
```

#### 2.1.3 İlk kullanıcı

Anonim kayıt yoktur. Migration'lar rolleri, izinleri, şablonları ve bir yönetici hesabı getirir: **`admin` / `Admin!2345`**. Bu parola
kaynak kodda yazılıdır — **ilk girişten hemen sonra değiştirin**. Diğer kullanıcıları Kullanıcılar ekranından açın.

#### 2.1.4 Güncelleme

Önce yedek, sonra [2.1.2](#212-migrationlar)'deki üç komut (yeni migration yoksa bir şey yapmazlar):

```sql
BACKUP DATABASE Scadex TO DISK = 'D:\Yedek\Scadex.bak' WITH COPY_ONLY, INIT;
```

### 2.2 API — Scadex.WebAPI (IIS)

#### 2.2.1 Yayın

Derleme makinesinde, depo kökünden:

```powershell
dotnet publish Scadex.WebAPI -c Release -o C:\paket\api
```

Paket klasöründe şunların olduğunu kontrol edin, yoksa elle kopyalayın; sonra paketi sunucuda `C:\inetpub\scadex\api` altına koyun:

- `api\MediaTools\ffmpeg\ffmpeg.exe` (+ `LICENSE.txt`)
- `api\MediaTools\mediamtx\mediamtx.exe` + `mediamtx.yml`
- `api\MapTiles\` — yoksa API açılır, uyarı loglar, harita zemini boş görünür

#### 2.2.2 Uygulama havuzu ve site

```powershell
Import-Module WebAdministration
New-WebAppPool ScadexApi
Set-ItemProperty IIS:\AppPools\ScadexApi -Name managedRuntimeVersion -Value ""
Set-ItemProperty IIS:\AppPools\ScadexApi -Name startMode -Value AlwaysRunning
Set-ItemProperty IIS:\AppPools\ScadexApi -Name processModel.idleTimeout -Value "00:00:00"

New-Website ScadexApi -PhysicalPath C:\inetpub\scadex\api -ApplicationPool ScadexApi -Port 3009 -Ssl
Set-ItemProperty IIS:\Sites\ScadexApi -Name applicationDefaults.preloadEnabled -Value $true
```

`AlwaysRunning`, `idleTimeout = 0` ve `preloadEnabled` zorunludur: MediaMTX'i ve arka plan işlerini (yoklama, durum taraması,
sinyalizasyon motoru) API çalıştırır. Bunlar olmazsa ilk HTTP isteğine kadar hiçbiri çalışmaz, boşta 20 dakika sonra da IIS uygulamayı
kapatır. 3009 portunu başka bir site tutuyorsa `New-Website` hata verir.

IIS Yöneticisi'nde:

- **Site → Bindings → https (3009) → Edit**: IP address *All Unassigned*, Host name **boş**, *Require Server Name Indication*
  **işaretsiz**, SSL certificate *Scadex Local Network API HTTPS*.
- **Site → SSL Settings**: *Require SSL* ✓, *Client certificates* = **Ignore**.

Ardından `iisreset`.

#### 2.2.3 Klasör izinleri

```powershell
icacls C:\inetpub\scadex\api /grant "IIS AppPool\ScadexApi:(OI)(CI)M"
```

Havuz kimliğinin exe'leri (MediaMTX, FFmpeg) çalıştırabilmesi, log ve kamera kayıtlarını yazabilmesi için gereklidir. Ayar dosyası
bağlantı parolasını ve `SecurityKey`'i taşır; klasör izni yöneticiler + havuz kimliğiyle sınırlı kalmalı.

#### 2.2.4 `appsettings.json` — tüm anahtarlar

`C:\inetpub\scadex\api\appsettings.json`. IIS'te ortam **Production**'dır; `appsettings.Development.json` okunmaz. Bir anahtar ayar
dosyası yerine ortam değişkeniyle de verilebilir (`:` yerine `__`, ör. `Modules__RemoteDesk__Enabled=true`); ortam değişkeni dosyayı
ezer. Değişiklikten sonra `Restart-WebAppPool ScadexApi`.

**Sahada mutlaka girilenler:**

| Anahtar | Ne | Örnek |
|---|---|---|
| `ConnectionStrings:Database` | SQL bağlantı dizesi ([2.1.1](#211-sql-girişi)) | `Server=localhost;Database=Scadex;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True` |
| `Cors:Origins` | Arayüzün açıldığı adres(ler); şema + IP + port, sonda `/` yok | `[ "https://172.16.42.106:3000" ]` |
| `TokenSettings:SecurityKey` | JWT imza anahtarı; **depodaki değeri kullanmayın** (üretimi aşağıda). Değişirse herkes yeniden giriş yapar | 88 karakterlik base64 |
| `MediaGateway:WebRtcPublicBaseUrl` | Tarayıcının canlı görüntüyü çektiği MediaMTX adresi — API portu **değil** | `https://172.16.42.106:8889` |
| `Modules:Signalization:Enabled` | Sinyalizasyon modülü (firmaya özel; yalnızca o müşteride `true`) | `false` |
| `Modules:RemoteDesk:Enabled` | RemoteDesk modülü (PC ekran izleme) | `true` |
| `Modules:RemoteDesk:PublishRtspBaseUrl` | PC'lerin ekran yayınını gönderdiği MediaMTX adresi; **PC'lerin ulaşabildiği** IP olmalı (`127.0.0.1` değil). Yanlışsa izleme "yayın hazır değil" ile düşer | `rtsp://172.16.42.106:8554` |

```powershell
# Yeni SecurityKey
$b = New-Object byte[] 64
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
[Convert]::ToBase64String($b)
```

**Gerekirse değişenler** (varsayılanlar sahada çalışır):

| Anahtar | Ne | Varsayılan |
|---|---|---|
| `Modules:RemoteDesk:AllowedNetworks` | PC bağlantısı ve yayını kabul edilen ağlar (CIDR); boş = her yer | `[]` (ör. `[ "10.20.0.0/16" ]`) |
| `MediaGateway:ApiBaseUrl` | MediaMTX yönetim API'si; = `mediamtx.yml > apiAddress` | `http://127.0.0.1:9997` |
| `MediaGateway:RtspPort` | MediaMTX RTSP portu (FFmpeg `127.0.0.1:<port>`'tan okur); = `rtspAddress` | `8554` |
| `MediaGateway:FfmpegPath` | FFmpeg yolu; göreliyse API klasörüne göre | `MediaTools/ffmpeg/ffmpeg.exe` |
| `MediaGateway:MediaMtxPath` | MediaMTX yolu; yml aynı klasörde aranır | `MediaTools/mediamtx/mediamtx.exe` |
| `MediaGateway:ApiTimeoutMs` | MediaMTX API çağrısı zaman aşımı | `30000` |
| `MediaGateway:TokenTtlSeconds` | İzleme/çekim biletinin ömrü | `60` |
| `MediaGateway:SourceOnDemandCloseAfterSec` | Son izleyici ayrıldıktan sonra kameraya giden oturumun kapanma süresi | `10` |
| `MediaGateway:RtspTransport` | Kamera → MediaMTX taşıması: `tcp` \| `udp` \| `multicast` \| `automatic` | `tcp` |
| `TokenSettings:AccessTokenExpiration` | Erişim token'ı ömrü (dakika) | `1440` |
| `TokenSettings:RefreshTokenExpiration` | Refresh token ömrü (dakika) | `10080` |
| `Scada:SweepIntervalSeconds` | Kabin durum taraması turu | `300` |
| `Monitoring:SweepIntervalSeconds` | Cihaz yoklama turu; cihazların en kısa yoklama aralığından küçük olmalı | `30` |
| `Monitoring:ProbeTimeoutMs` | Tek yoklamanın zaman aşımı | `3000` |
| `Jobs:MediaPathCleanupHour` | Canlı izleme yollarının günlük temizlik saati (0–23, yerel) | `3` |
| `Jobs:CaptureRetentionSweepHour` | Süresi dolmuş çekim dosyalarının silinme saati (0–23, yerel) | `4` |
| `Basemap:Directory` | Harita zemini klasörü (dosyada yok; göreliyse API klasörüne göre) | `MapTiles` |

**Dokunulmayanlar:** `TokenSettings:Audience` / `Issuer` / `RefreshTokenTTL`, `CacheSettings`, `LocalizationSettings`, `Logging`,
`AllowedHosts`. Kamera çekim ayarları (saklama süresi vb.) dosyada **değil** veritabanındadır (arayüzde **Yönetim → Ayarlar**);
`Cameras` bölümü eklemeyin, okunmaz.

**Modüller iki tarafta ayrı ayarlanır ve aynı olmalıdır:** API'de `Modules:<Ad>:Enabled`, arayüzde `VITE_MODULES`
([2.3.1](#231-envproduction--tüm-anahtarlar)). Tanımsız = kapalı; kapalı modülün uçları 404 döner, menüsü görünmez.

#### 2.2.5 Kontrol

`https://172.16.42.106:3009/scalar` sertifika uyarısı olmadan açılmalı; API günlüğünde (`api\Logs\Information*.log`)
`MediaMTX baslatildi (PID …)` satırı görünmeli.

#### 2.2.6 MediaMTX ve FFmpeg

- **MediaMTX'i API başlatır** ve kapanırsa birkaç saniyede yeniden başlatır. API kapanırken MediaMTX'i bilerek öldürmez; yeniden açılınca
  çalışanı sahiplenir.
- **MediaMTX'i Windows servisi olarak KURMAYIN, elle de çalıştırmayın.** `mediamtx.exe` Windows servis protokolünü uygulamaz
  (`sc create` ile kurulursa `1053` hatası verir) ve ikinci kopya aynı portları tutar.
- **FFmpeg'i API çağırır** (anlık görüntü, klip); kameraya doğrudan değil MediaMTX üzerinden bağlanır. Kameraya yalnızca MediaMTX bağlanır.

Sertifika ([1.2](#12-sertifikalar)'de üretilen PEM dosyaları):

```powershell
copy C:\certs\scadex-server.key C:\inetpub\scadex\api\MediaTools\mediamtx\server.key
copy C:\certs\scadex-server.crt C:\inetpub\scadex\api\MediaTools\mediamtx\server.crt
```

`mediamtx.yml` — **var olan** satırlar değiştirilir, yeni satır eklenmez (`yes` değil `true` yazın):

| Anahtar | Sahada | Not |
|---|---|---|
| `authHTTPAddress` | `https://172.16.42.106:3009/api/MediaGateway/auth` | **Mutlaka değişir** (depoda geliştirme adresi `http://127.0.0.1:5208/…`). Sunucu Root CA'ya güvendiği için HTTPS çağrısı geçer |
| `webrtcEncryption` | `true` | Tarayıcı HTTPS sayfadan yalnızca HTTPS'e bağlanır |
| `webrtcServerKey` / `webrtcServerCert` | `server.key` / `server.crt` | Depodaki değer zaten budur; dosyalar yukarıda kopyalandı |
| `apiAddress` | `127.0.0.1:9997` | = `MediaGateway:ApiBaseUrl`. Dışarı açılmaz |
| `rtspAddress` | `:8554` | = `MediaGateway:RtspPort` ve `PublishRtspBaseUrl` portu |
| `webrtcAddress` | `:8889` | = `MediaGateway:WebRtcPublicBaseUrl` portu |
| `webrtcLocalUDPAddress` | `:8189` | Yalnızca güvenlik duvarında açılır |
| `authMethod` | `http` | Değiştirmeyin — her izleme/yayın API'ye sorulur |
| `logDestinations` | `[stdout]` | Değiştirmeyin — çıktı API günlüğüne `[MediaMTX]` önekiyle yazılır |
| `rtmp`, `hls`, `srt`, `moq`, `rtspTransports` | `false` / `[tcp]` | Kullanılmayan protokoller port açmasın diye kapalı — açmayın |

Yerel ağda çalışıldığı için `webrtcAdditionalHosts` ve `hosts` dosyası gerekmez. yml değişince MediaMTX'i sonlandırın; API birkaç saniyede
yeni haliyle açar:

```powershell
Stop-Process -Name mediamtx -Force
```

#### 2.2.7 Güncelleme

Korunan dosyalar: `appsettings.json`, `mediamtx.yml` (yayındaki geliştirme kopyası sahaya özel ayarları ezer), `server.key`, `server.crt`.

```powershell
Import-Module WebAdministration
Stop-WebAppPool ScadexApi
robocopy C:\paket\api C:\inetpub\scadex\api /E /XF appsettings.json mediamtx.yml server.key server.crt
Start-WebAppPool ScadexApi
```

- `/E` yayında olmayan dosyaları (`MapTiles`, günlükler, kamera kayıtları) silmez.
- `mediamtx.exe` güncellendiyse kopyadan önce `Stop-Process -Name mediamtx -Force` (exe kilitli kalır).
- `web.config` depodan gelir ve yayınla güncellenir: IIS'in gövde sınırını 1 GB'a çıkaran `requestLimits` onun içindedir (silinirse
  ~28,6 MB üstü dosya aktarımı 404.13 alır). Elle bir şey eklediyseniz (ortam değişkeni, stdout log) kopyadan sonra yeniden ekleyin.
- Yeni sürüm yeni bir ayar getirdiyse `appsettings.json`'a elle ekleyin.

### 2.3 Arayüz — Scadex.WebUI (IIS)

#### 2.3.1 `.env.production` — tüm anahtarlar

`Scadex.WebUI\.env.production` (derleme makinesinde). Değerler **derlemeye gömülür**; değişirse yeniden derlenip kopyalanır.

```ini
VITE_API_URL=https://172.16.42.106:3009
VITE_CLIENT_TYPE=a9537ece-1957-4bd0-8692-5da2c16e715b
VITE_MODULES=signalization,remotedesk
```

| Anahtar | Ne | Not |
|---|---|---|
| `VITE_API_URL` | API'nin dış adresi | Sonda `/` yok; API'nin binding'iyle aynı şema/IP/port |
| `VITE_CLIENT_TYPE` | Web istemcisinin sabit kimliği | **Değiştirmeyin** — sunucudaki `ClientType.Web` ile aynı olmalı, yoksa oturum yenilenmez |
| `VITE_MODULES` | Açık modüller, virgülle: `signalization`, `remotedesk` | API'deki `Modules:*:Enabled` ile **aynı**; modül yoksa boş (`VITE_MODULES=`) |

#### 2.3.2 Derleme

```powershell
cd Scadex.WebUI
npm ci
npm run build
```

`npm run build` tip kontrolünü de yapar; hata verirse yayına çıkılmaz. Çıktı `Scadex.WebUI\dist\`; **içeriğini** `C:\inetpub\scadex\web\`
altına kopyalayın.

#### 2.3.3 `web.config` ve site

`C:\inetpub\scadex\web\web.config` (sayfa yenilemede 404 almamak için; derleme çıktısında yoktur):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="SPA" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
```

```powershell
Import-Module WebAdministration
New-WebAppPool ScadexWeb
Set-ItemProperty IIS:\AppPools\ScadexWeb -Name managedRuntimeVersion -Value ""
New-Website ScadexWeb -PhysicalPath C:\inetpub\scadex\web -ApplicationPool ScadexWeb -Port 3000 -Ssl
```

Binding'de API ile **aynı** sertifikayı seçin (*Scadex Local Network API HTTPS*); sertifika IP'ye bağlıdır, port fark etmez. Host name
boş, SNI işaretsiz. Arayüzün adresi API'de `Cors:Origins` içinde **birebir** yazılı olmalı.

#### 2.3.4 Güncelleme

```powershell
robocopy C:\paket\web C:\inetpub\scadex\web /MIR /XF web.config
```

Havuzu durdurmak gerekmez; operatörler sayfayı yenileyince yeni sürüm gelir.

### 2.4 RemoteDesk PC istemcisi — Windows servisi

İzlenecek her PC'ye kurulur. Tasarım ve kararlar: [RemoteDesk.md](RemoteDesk.md) § 9. **Kurulum paketi ve kurulum betiği bilerek
yoktur:** iki proje yayınlanır, PC'ye kopyalanır, servis bir kez `sc.exe create` ile tanıtılır.

#### 2.4.1 Süreçler

| Süreç | Exe | Nerede, kim | Ne yapar |
|---|---|---|---|
| **Servis** `ScadexRemoteDesk` | `Service\Scadex.RemoteDesk.Windows.Service.exe` | Session 0, LocalSystem; açılışta otomatik | Gözetmen: aktif kullanıcı oturumunda tek Agent çalıştırır, çökerse yeniden başlatır |
| **Agent** | aynı exe, `--session` (servis başlatır) | Kullanıcının oturumu, SYSTEM | Merkeze bağlanır, ekranı yayınlar, fare/klavye, dosya, pano |
| **Arayüz** (isteğe bağlı) | `Tray\Scadex.RemoteDesk.Windows.exe --tray` | Kullanıcının oturumu, normal yetki | System Tray ikonu + "izleniyor" uyarısı |

- Agent yalnızca **oturum açılmış** bir kullanıcı varken çalışır (önce konsol, yoksa ilk aktif RDP oturumu); giriş ekranında PC merkezde
  "bağlı değil" görünür.
- Kilit ekranı ve UAC onay penceresi izlenir ve kontrol edilir; Ctrl+Alt+Del gönderilemez.
- Kullanıcı Agent'ı kapatamaz; "Arayüzü kapat" yalnızca arayüzü kapatır, izleme sürer.

#### 2.4.2 Merkezde PC tanımı ve kimlik

1. Kabin diyagramına **Bilgisayar** şablonundan cihaz ekleyin → özellik paneli → **Kimlik bilgisi → Üret** → **Kaydet**.
2. **Cihaz Id'sini** ve **sırrı** not alın (sır yalnızca üretildiği anda görünür; kaybolursa **Yenile**).
3. API'de `Modules:RemoteDesk:Enabled = true` ve doğru `PublishRtspBaseUrl` ([2.2.4](#224-appsettingsjson--tüm-anahtarlar)), arayüzde
   `VITE_MODULES` içinde `remotedesk`.

#### 2.4.3 Yayın (Service + Tray)

Derleme makinesinde. İkisi de **self-contained win-x64** (.NET gömülü; PC'de kurulum gerekmez, toplam ≈ 420 MB):

```powershell
dotnet publish Modules\RemoteDesk\Scadex.RemoteDesk.Windows.Service -c Release -r win-x64 --self-contained true -o C:\paket\RemoteDesk\Service
dotnet publish Modules\RemoteDesk\Scadex.RemoteDesk.Windows         -c Release -r win-x64 --self-contained true -o C:\paket\RemoteDesk\Tray
```

- **İki ayrı klasör bilerek:** aynı klasöre konursa ortak DLL'ler birbirinin üstüne yazılır.
- `tools\ffmpeg\ffmpeg.exe` + LGPL `LICENSE.txt` yoksa servis yayını **bilerek hata verir** ([1.1](#11-önkoşullar)).
- Binary'ler henüz kod imzalı değildir; antivirüs işaretlerse kurulum klasörü istisnaya eklenir.

#### 2.4.4 Kopyalama ve `appsettings.json` — tüm anahtarlar

`Service\` ve `Tray\` **yan yana** kalmalı (arayüz Agent'ı `..\Service\` yolundan doğrular; değilse "Agent doğrulanamadı"):

```text
C:\Program Files\Scadex\RemoteDesk\
├── Service\
│   ├── Scadex.RemoteDesk.Windows.Service.exe
│   ├── appsettings.json
│   └── tools\ffmpeg\ffmpeg.exe (+ LICENSE.txt)
└── Tray\
    └── Scadex.RemoteDesk.Windows.exe
```

Root CA'yı yükleyin ([1.2](#root-canın-dağıtımı)), sonra `Service\appsettings.json`:

```json
{
  "RemoteDesk": {
    "CentralApiUrl": "https://172.16.42.106:3009",
    "DeviceId": "<2.4.2'deki cihaz Id'si>",
    "DeviceSecret": "<2.4.2'deki sır>"
  }
}
```

| Anahtar | Ne | Not |
|---|---|---|
| `RemoteDesk:CentralApiUrl` | API'nin dış adresi | Depoda `http://localhost:5208` (geliştirme) — sahada **mutlaka** değişir; sertifikadaki IP ile aynı yazılır |
| `RemoteDesk:DeviceId` | Basic Auth kullanıcı adı = diyagramdaki PC cihazının Id'si | Boşsa "Yapılandırma eksik" |
| `RemoteDesk:DeviceSecret` | Basic Auth parolası = panelde üretilen sır | Boşsa "Yapılandırma eksik"; günlüğe ve arayüze yazılmaz |

Başka anahtar yoktur: video profili ve yayın adresi merkezden gelir, FFmpeg yolu servis klasörüne göre sabittir (`tools\ffmpeg\`). Arayüzün
(`Tray\`) ayar dosyası yoktur. Değişiklikten sonra `sc.exe stop ScadexRemoteDesk` + `sc.exe start ScadexRemoteDesk`.

Dosya cihaz sırrını taşır ve varsayılan izinlerle standart kullanıcılarca **okunabilir** (kabul edilen eksik). Kısmak için (yalnızca SYSTEM
ve Administrators):

```powershell
icacls "C:\Program Files\Scadex\RemoteDesk\Service\appsettings.json" /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F"
```

#### 2.4.5 Servis kaydı (sc.exe)

Yönetici PowerShell. **PowerShell'de `sc` başka bir komuttur (`Set-Content`); her zaman `sc.exe` yazın.** `binPath=`, `start=` gibi
anahtarlarda eşittirden sonra **bir boşluk** zorunludur.

```powershell
sc.exe create ScadexRemoteDesk binPath= "C:\Program Files\Scadex\RemoteDesk\Service\Scadex.RemoteDesk.Windows.Service.exe" start= auto DisplayName= "Scadex RemoteDesk"
sc.exe description ScadexRemoteDesk "Scadex merkezine ekran izleme ve uzaktan kontrol sağlar."
sc.exe failure ScadexRemoteDesk reset= 86400 actions= restart/5000/restart/10000/restart/30000
sc.exe start ScadexRemoteDesk

sc.exe query ScadexRemoteDesk                  # STATE: 4 RUNNING
Get-Process Scadex.RemoteDesk.Windows.Service  # oturum açıkken iki süreç: servis + Agent
```

- Servis hesabı LocalSystem'dir (varsayılan) — **değiştirmeyin**: Agent'ı kullanıcı oturumunda SYSTEM olarak başlatmak için gerekir.
- `sc.exe failure` servis sürecinin kendisi çökerse Windows yeniden başlatsın diyedir (Agent'ın çökmesini servis zaten toparlar).
- Servis exe'sini elle çalıştırmayın; elle (SYSTEM olmadan) başlatılan Agent kendiliğinden çıkar.

Birkaç saniye sonra merkezde PC çevrimiçi görünür. Diğer `sc.exe` komutları: [3.1](#31-windowsta-ne-nasıl-çalışır).

#### 2.4.6 Arayüzü oturum açılışına ekleme (isteğe bağlı)

```powershell
reg.exe add "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "ScadexRemoteDesk" /t REG_SZ /d "\"C:\Program Files\Scadex\RemoteDesk\Tray\Scadex.RemoteDesk.Windows.exe\" --tray" /f
```

Alternatif: aynı exe'nin `--tray` kısayolunu ortak Başlangıç klasörüne koyun (`Win+R → shell:common startup`). Arayüzde **Bağlı —
{kabin} / {cihaz}** görülmeli.

#### 2.4.7 Eski sürümden geçiş

Eski sürüm (Görev Zamanlayıcı ile çalışan tek WPF uygulaması) kurulu PC'de önce onu kaldırın; yoksa eski istemci bağlantıyı tutar ve yeni
Agent "Bu cihaz başka bir bağlantıda" kalır:

```powershell
schtasks.exe /Delete /TN "Scadex RemoteDesk Client" /F
Stop-Process -Name Scadex.RemoteDesk.Windows -Force -ErrorAction SilentlyContinue
```

Eski Başlangıç kısayolunu ve klasörü silin; kimlik [2.4.2](#242-merkezde-pc-tanımı-ve-kimlik)'deki gibi yeniden üretilir (MAC ayarları
artık kullanılmaz).

#### 2.4.8 Güncelleme ve kaldırma

Güncelleme (`appsettings.json` korunur; `Service` ve `Tray` **birlikte** güncellenir, yoksa arayüz "sürüm uyuşmuyor" der; otomatik
güncelleme bilerek yoktur):

```powershell
sc.exe stop ScadexRemoteDesk
Stop-Process -Name Scadex.RemoteDesk.Windows -Force -ErrorAction SilentlyContinue   # açık arayüz dosyaları kilitler

robocopy "D:\yeni\RemoteDesk\Service" "C:\Program Files\Scadex\RemoteDesk\Service" /MIR /XF appsettings.json
robocopy "D:\yeni\RemoteDesk\Tray"    "C:\Program Files\Scadex\RemoteDesk\Tray"    /MIR

sc.exe start ScadexRemoteDesk
```

Kaldırma:

```powershell
sc.exe stop ScadexRemoteDesk
sc.exe delete ScadexRemoteDesk
reg.exe delete "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "ScadexRemoteDesk" /f
Stop-Process -Name Scadex.RemoteDesk.Windows -Force -ErrorAction SilentlyContinue
Remove-Item "C:\Program Files\Scadex\RemoteDesk" -Recurse -Force
Remove-Item "$env:ProgramData\Scadex\RemoteDesk" -Recurse -Force     # günlükler (isteğe bağlı)
```

Merkezde PC'nin kimliğini de iptal edin (diyagram → **İptal et** → Kaydet).

#### 2.4.9 Günlükler ve sorun giderme

| Süreç | Günlük |
|---|---|
| Servis | `%ProgramData%\Scadex\RemoteDesk\logs\service-YYYYMMDD.log` |
| Agent | `%ProgramData%\Scadex\RemoteDesk\logs\agent-YYYYMMDD.log` |
| Arayüz | `%LocalAppData%\Scadex\RemoteDesk\logs\ui-*.log` (oturumdaki kullanıcının) |

Cihaz sırrı, yayın bileti ve pano içeriği **asla** yazılmaz.

| Belirti | Neden |
|---|---|
| Servis çalışıyor, merkezde PC yok | Kimse oturum açmamış (tek süreç görünür) |
| "Yapılandırma eksik" | `DeviceId` ya da `DeviceSecret` boş |
| "Kimlik bilgisi reddedildi" | Id/sır yanlış, yenilenmiş/iptal edilmiş ya da cihaz silinmiş; yeniden üretip girin, servisi yeniden başlatın |
| "Bu cihaz başka bir bağlantıda" | Aynı kimlik başka PC'de ya da eski istemci çalışıyor ([2.4.7](#247-eski-sürümden-geçiş)) |
| "Bu ağdan bağlantı kabul edilmiyor" | PC'nin IP'si `Modules:RemoteDesk:AllowedNetworks` dışında |
| "Servis bağlantısı yok" | Servis kapalı ya da Agent yeniden başlıyor — `sc.exe query`, `service-*.log` |
| "Agent doğrulanamadı" | `Service\` ve `Tray\` yan yana değil |
| Agent günlüğünde SSL/sertifika hatası | PC'ye Root CA yüklenmemiş ya da `CentralApiUrl` sertifikadaki IP'den farklı |
| İzleme "yayın hazır değil" ile düşüyor | `PublishRtspBaseUrl` PC'nin ulaşamadığı adres ya da 8554/tcp kapalı |
| Yönetici pencerelerine tıklama çalışmıyor | Agent servis yerine elle başlatılmış (SYSTEM değil) |

### 2.5 Saha cihazları ve istemciler

#### 2.5.1 SCADA kontrol kartı

Her kabinde bir kontrol kartı vardır. Kart ekibine verilen sözleşme: [Docs/scada-http-api.md](Docs/scada-http-api.md).

| Yön | İstek | Kimlik |
|---|---|---|
| Kart → Scadex | `POST /api/Scada/ingest` (giriş/analog değer), `POST /api/Scada/card` (kart okuma) | **`DeviceId: <MAC>` header'ı** |
| Scadex → Kart | `POST {kabinin SCADA adresi}/command` + `{ "channelNumber": <n>, "value": "<0\|1>" }` (çıkış komutu) | Yok |

**Diyagramda kimlik bilgisi:**

1. Kabinin **diyagram**ına kontrol modülünü ekleyin, kanallarını tanımlayın (`EditDiagram` izni).
2. Cihazı seçin → özellik paneli → **Kontrol kartı → MAC adresi** alanına kartın MAC adresini yazın → **Kaydet**. Yazım farkı
   (`50-8d-5c-…`, `508D5C…`) önemsizdir, `AA:BB:CC:DD:EE:FF` biçimine çevrilir.
3. Kart her istekte bu adresi `DeviceId` header'ında gönderir.

| Durum | Ne yapılır |
|---|---|
| Kart değişti | Yeni kartın MAC'ini girin → Kaydet (eski kart 401 alır) |
| Kartı devre dışı bırakmak | MAC alanını boşaltın → Kaydet |

Kabin bu MAC'ten çözülür; gövdede kabin yoktur. Eşleşmeyen / eksik MAC **401** döner (nedeni API günlüğünde). MAC tekilliği
denetlenmez: aynı adresi iki karta girmeyin (ilk eşleşen kazanır).

> **Güncelleme notu (2026-10-28):** `ControlCardMacIdentity` migration'ı kontrol kartlarının eski Basic Auth sırlarını siler; her
> kontrol kartına diyagramdan MAC adresi girilmeden kart 401 alır.

**Kartın ayarları:**

| Kartta | Değer |
|---|---|
| Sunucu adresi | `https://172.16.42.106:3009` |
| `DeviceId` header'ı | Kartın MAC adresi (diyagrama girilenle aynı) |
| Güvenilen sertifika | `local-root-ca.crt` |

Sahadaki API sitesi yalnızca HTTPS kabul eder ([2.2.2](#222-uygulama-havuzu-ve-site), *Require SSL*). **Kart HTTPS konuşamıyorsa** API'ye
ayrı bir HTTP bağlaması ve *Require SSL* ayarının gözden geçirilmesi gerekir; düz HTTP'de MAC her istekte açık gider (kabul edilmiş risk).
Bu düzen sahada karar verilmeden kurulmaz.

Deneme (kanalın değerini gerçekten yazar; kullanılmayan bir kanalla deneyin): `200` = kimlik doğru, `401` = MAC eşleşmedi.

```powershell
curl.exe -H "DeviceId: <kartın MAC adresi>" -H "Content-Type: application/json" `
  -d "{ \"type\": \"I\", \"channelNumber\": 1, \"value\": \"1\" }" `
  https://172.16.42.106:3009/api/Scada/ingest
```

**Giden komutlar:** kabin düzenleme ekranında **SCADA açık** işaretli ve **SCADA adresi** kartın HTTP adresi olmalı (ör.
`http://10.20.1.15:8080`). Komutta yeniden deneme **bilerek yoktur**: başarısız komut tekrarlanmaz, operatör yeniden gönderir.

#### 2.5.2 Operatör bilgisayarları

Kurulacak program yoktur; yalnızca Root CA yüklenir ([1.2](#root-canın-dağıtımı)). HTTP kurulumunda Root CA da yoktur; isteğe bağlı tarayıcı
ayarı [1.4](#14-http-ile-kurulum-sertifikasız)'tedir.

- Chrome ve Edge Windows deposunu kullanır. Firefox kendi deposunu kullanır: `about:config → security.enterprise_roots.enabled = true`.
- Sertifika uyarısı **çıkmamalı**. Uyarıyı "devam et" ile geçmek yetmez: canlı görüntü ayrı porttan geldiği için yine açılmaz.

---

## 3. Bakım ve işletme

### 3.1 Windows'ta ne nasıl çalışır

| Parça | Makine | Windows'taki karşılığı |
|---|---|---|
| API | Sunucu | IIS havuzu `ScadexApi` (`w3wp.exe`); IIS servisleri `W3SVC`, `WAS` |
| Arayüz | Sunucu | IIS sitesi `ScadexWeb` |
| MediaMTX | Sunucu | API'nin alt süreci — **servis DEĞİL** |
| FFmpeg | Sunucu | API'nin kısa ömürlü alt süreci |
| SQL Server | Sunucu ya da ayrı | Servis `MSSQLSERVER` / `MSSQL$SQLEXPRESS` |
| RemoteDesk servisi | Sahadaki PC | Servis `ScadexRemoteDesk` (LocalSystem, otomatik) — [2.4.5](#245-servis-kaydı-scexe) |
| RemoteDesk Agent | Sahadaki PC | Servisin alt süreci (kayıt yok) |
| RemoteDesk arayüzü | Sahadaki PC | `HKLM\…\Run` ya da Başlangıç klasörü |

API IIS'te barınacak şekilde yazılmıştır, Windows servisi olarak barındırma kodu yoktur.

#### sc.exe komutları

| Ne | Komut |
|---|---|
| Kaydet | `sc.exe create <ad> binPath= "<exe>" start= auto DisplayName= "<görünen ad>"` |
| Başlat / durdur | `sc.exe start <ad>` · `sc.exe stop <ad>` |
| Durum (PID ile) | `sc.exe query <ad>` · `sc.exe queryex <ad>` |
| Kayıtlı ayarlar / kurtarma ayarları | `sc.exe qc <ad>` · `sc.exe qfailure <ad>` |
| Yol / başlangıç türü değiştir | `sc.exe config <ad> binPath= "<yeni exe>"` · `sc.exe config <ad> start= delayed-auto` (`auto`, `demand`, `disabled`) |
| Sil | `sc.exe stop <ad>` sonra `sc.exe delete <ad>` |

`sc.exe delete` sonrası Hizmetler penceresi (`services.msc`) açıksa kayıt o pencere kapanana kadar kalır. PowerShell karşılıkları:

```powershell
Get-Service ScadexRemoteDesk | Format-List Name, DisplayName, Status, StartType
Restart-Service ScadexRemoteDesk
Get-CimInstance Win32_Service -Filter "Name='ScadexRemoteDesk'" | Select-Object Name, State, StartName, PathName, ProcessId
```

#### IIS / MediaMTX / SQL komutları

```powershell
Import-Module WebAdministration
Get-Service W3SVC, WAS
Get-WebAppPoolState ScadexApi
Restart-WebAppPool ScadexApi
Get-Website | Select-Object Name, State, PhysicalPath, Bindings
& "$env:windir\System32\inetsrv\appcmd.exe" list wp     # hangi w3wp hangi havuzun

Get-Process mediamtx | Select-Object Id, StartTime, Path  # API'nin başlattığı tek kopya
Get-Service MSSQL*
```

#### Olay Görüntüleyicisi

Servis başlatma hataları (`1053`, `7000`, `7031`): *Sistem* günlüğü, kaynak **Service Control Manager**. API açılış hataları (`500.30`):
*Uygulama* günlüğü, kaynak **IIS AspNetCore Module V2**.

```powershell
Get-WinEvent -FilterHashtable @{ LogName = 'System'; ProviderName = 'Service Control Manager' } -MaxEvents 20 |
  Where-Object Message -like '*Scadex*' | Format-List TimeCreated, Id, Message
```

### 3.2 Güncelleme sırası

1. **Yayınlar** (derleme makinesi): [2.2.1](#221-yayın), [2.3.2](#232-derleme), gerekiyorsa [2.4.3](#243-yayın-service--tray).
2. **Veritabanı:** yedek + migration — [2.1.4](#214-güncelleme).
3. **API:** [2.2.7](#227-güncelleme).
4. **Arayüz:** [2.3.4](#234-güncelleme).
5. **RemoteDesk PC'leri:** [2.4.8](#248-güncelleme-ve-kaldırma).
6. **Kontrol:** [3.3](#33-kontrol-listesi-ve-sık-hatalar).

### 3.3 Kontrol listesi ve sık hatalar

1. `https://172.16.42.106:3009/scalar` sertifika uyarısı olmadan açılıyor.
2. API günlüğünde `MediaMTX baslatildi (PID …)` satırı var; Görev Yöneticisi'nde `mediamtx.exe` çalışıyor.
3. `https://172.16.42.106:3000` üzerinden giriş yapılıyor; bir alt sayfada F5'te 404 yok; harita zemini görünüyor.
4. Bir kamerada canlı izleme açılıyor (MediaMTX + sertifika) ve anlık görüntü alınıyor (FFmpeg).
5. Kontrol kartı veri gönderiyor: diyagramda canlı gösterge yeşil, kabin rengi doğru.
6. Sahadaki PC listede çevrimiçi görünüyor ve ekranı izlenebiliyor.

| Belirti | Neden |
|---|---|
| `500.19` / `0x8007000d` | Hosting Bundle ya da URL Rewrite eksik |
| `500.30` | Uygulama açılışta düşüyor. API `web.config`'inde `stdoutLogEnabled="true"`, `stdoutLogFile=".\logs\stdout"` yapın, `api\logs` klasörünü oluşturun, `stdout_*.log`'u okuyun (çoğunlukla SQL girişi ya da `appsettings`); sonra geri kapatın |
| İstekler `Invalid object name …` ile 500 | Migration'lar uygulanmamış ([2.1.2](#212-migrationlar)) |
| Canlı görüntü açılmıyor | `WebRtcPublicBaseUrl` portu 8889 ve `https` mi, `webrtcEncryption: true` mu, 8889/tcp ve 8189/udp açık mı |
| Çekimde "Medya geçidi çekim biletini reddetti" | `authHTTPAddress` yanlış ya da MediaMTX API'ye ulaşamıyor |
| Çekimde "Medya geçidine ulaşılamıyor" | `MediaGateway:RtspPort` ile `rtspAddress` eşleşmiyor ya da MediaMTX çalışmıyor |
| Çekimde "FFmpeg bulunamadı" | `MediaTools\ffmpeg\ffmpeg.exe` yok ya da havuzun okuma/çalıştırma izni yok |
| `mediamtx.exe` başlamıyor | `mediamtx.yml` yok ya da port başka süreçte (`Get-NetTCPConnection -LocalPort 8889`) |
| Harita zemini boş | `api\MapTiles\` eksik |
| Tarayıcıda sertifika uyarısı | O bilgisayara Root CA yüklenmemiş (Firefox'ta ayrıca enterprise roots) |
| Giriş sonrası oturum düşüyor / CORS hatası | `Cors:Origins` arayüzün adresiyle birebir eşleşmiyor (şema, port, sonda `/`) ya da `VITE_CLIENT_TYPE` değiştirilmiş |
| Modül menüsü yok ya da ekranı 404'lerle dolu | `Modules:*:Enabled` ile `VITE_MODULES` farklı |
| Kontrol kartı 401 | Cihaz Id/sır yanlış, yenilenmiş/iptal edilmiş ya da cihaz silinmiş |
| Komut "SCADA'ya ulaşılamadı" | Kabinin SCADA adresi yanlış ya da sunucu karta erişemiyor |
| Büyük dosya aktarımı 404.13 / yarıda kalıyor | API klasöründeki `web.config`'te `requestLimits` yok (yayından gelir; elle düzenlenmiş eski bir `web.config` kalmış olabilir) |
| Dosya aktarımı "İzleme bittiği için aktarım durduruldu" | Beklenen: operatör PC'yi izlemeyi bırakınca PC'ye/PC'den süren aktarım kesilir |
| Dosya aktarımında klasör gezgini yok, "Dosya seç" çıkıyor / pano bir pencere açıyor | Beklenen: arayüz HTTP'den açılmış ya da tarayıcı Firefox ([1.4](#14-http-ile-kurulum-sertifikasız) — tarayıcı ayarıyla tam deneyim) |
| HTTP kurulumunda MediaMTX çekim/izleme yetkisi reddediliyor, cihazlar bağlanamıyor | API sitesinde https bağlaması da var; `UseHttpsRedirection` http'yi yönlendiriyor — yalnızca http bağlaması bırakın |
| RemoteDesk sorunları | [2.4.9](#249-günlükler-ve-sorun-giderme) |

**Günlükler:** API `C:\inetpub\scadex\api\Logs\` (`Information*.log`, `Warning*.log`, `Error*.log`; MediaMTX çıktısı `[MediaMTX]`
önekiyle). RemoteDesk: [2.4.9](#249-günlükler-ve-sorun-giderme).

---

## Geliştirme ortamı

```bash
# Backend (depo kökünden) — http://localhost:5208, Scalar: /scalar
dotnet build Scadex.slnx
dotnet run --project Scadex.WebAPI

# Frontend (Scadex.WebUI/) — http://localhost:5173
npm install
npm run dev
```

Migration komutları, modül ayarları ve çalışma kuralları: [CLAUDE.md](CLAUDE.md) → *Komutlar*.
