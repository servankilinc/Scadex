# Scadex

Geliştirme komutları ve mimari kurallar [CLAUDE.md](CLAUDE.md)'de, sistemin ne yaptığı ve kararların gerekçeleri
[PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)'dedir. Bu dosya sahada **IIS'e kurulumu** anlatır.

---

## IIS'te yayın (alan adı olmadan, IP ile)

Sistem alan adı olmadan, sunucunun yerel ağ IP'si üzerinden HTTPS ile çalışır. Sertifikayı kendi yerel kök
sertifikamız (Root CA) imzalar; bu kök sertifika sunucuya ve her istemci bilgisayara güvenilir olarak yüklenir.

HTTPS zorunludur: refresh token cookie'si `Secure`'dur, RemoteDesk dosya aktarımı ve pano güvenli bağlam ister,
HTTPS sayfadan HTTP'ye (MediaMTX) istek tarayıcıda engellenir.

### Örnek değerler

Aşağıdaki tüm komutlar bu değerlerle yazılmıştır. Sahada farklıysa **her yerde** değiştirin.

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

Tüm PowerShell komutları **yönetici olarak** çalıştırılır.

### Kim nereye bağlanır

| Bağlanan | Hedef | Ayar |
|---|---|---|
| Operatörün tarayıcısı | Arayüz `:3000` | IIS binding |
| Arayüz (tarayıcıda) | API `:3009` | `.env.production > VITE_API_URL`, API'de `Cors:Origins` |
| Operatörün tarayıcısı | MediaMTX `:8889` + `8189/udp` (kamera ve PC izleme) | `MediaGateway:WebRtcPublicBaseUrl` |
| Sahadaki PC (RemoteDesk istemcisi) | API `:3009` | istemcide `CentralApiUrl` |
| Sahadaki PC (RemoteDesk istemcisi) | MediaMTX `:8554` (ekran yayını) | `Modules:RemoteDesk:PublishRtspBaseUrl` |
| MediaMTX | API `:3009` (yetki kontrolü) | `mediamtx.yml > authHTTPAddress` |
| MediaMTX | Kameralar (RTSP) | kamera kaydı |

---

### 0. Önkoşullar (bir kez)

- **.NET 10 Hosting Bundle** — IIS'ten **sonra** kurulmalı; önce kurulduysa yeniden çalıştırıp *Repair*.
  Eksikse site `500.19 / 0x8007000d` verir.
- **URL Rewrite** modülü (arayüzün `web.config`'i için).
- IIS özellikleri:

  ```powershell
  Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebSockets, IIS-ApplicationInit
  iisreset
  ```

- **OpenSSL** (ör. Win64 OpenSSL Light) ve PATH'e eklenmesi:

  ```powershell
  Test-Path "C:\Program Files\OpenSSL-Win64\bin\openssl.exe"

  $opensslPath = "C:\Program Files\OpenSSL-Win64\bin"
  [Environment]::SetEnvironmentVariable("Path", [Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + $opensslPath, "Machine")
  ```

  Yeni PATH yalnızca **yeni açılan** PowerShell pencerelerinde geçerlidir.

### 1. Sertifikalar

#### 1.1 Çalışma klasörü

```powershell
mkdir C:\certs
cd C:\certs
```

#### 1.2 Yerel Root CA

```powershell
openssl genrsa -out local-root-ca.key 4096

openssl req -x509 -new -nodes `
  -key local-root-ca.key `
  -sha256 `
  -days 3650 `
  -out local-root-ca.crt `
  -subj "/C=TR/O=Local Development/CN=Local Development Root CA"
```

- `local-root-ca.key` Root CA'nın özel anahtarıdır. Bunu ele geçiren biri, kök sertifikayı yüklemiş tüm
  bilgisayarların güveneceği sahte sertifikalar üretebilir. **Sunucuda bırakmayın, güvenli bir yerde saklayın.**
- `local-root-ca.crt` yaklaşık 10 yıl geçerlidir.

#### 1.3 Sunucu anahtarı ve CSR

```powershell
openssl genrsa -out scadex-server.key 2048

openssl req -new `
  -key scadex-server.key `
  -out scadex-server.csr `
  -subj "/C=TR/O=Local Development/CN=172.16.42.106"
```

#### 1.4 Uzantı dosyası (SAN)

Tarayıcılar CN'e değil SAN alanına bakar. `notepad C:\certs\scadex-server.ext` ile oluşturun:

```ini
authorityKeyIdentifier=keyid,issuer
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=@alt_names

[alt_names]
IP.1=172.16.42.106
```

#### 1.5 Sunucu sertifikasını imzala ve kontrol et

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

#### 1.6 IIS için PFX

```powershell
openssl pkcs12 -export `
  -out scadex-server.pfx `
  -inkey scadex-server.key `
  -in scadex-server.crt `
  -certfile local-root-ca.crt `
  -name "Scadex Local Network API HTTPS"
```

Sorulan PFX parolası bir sonraki adımda gerekir.

#### 1.7 Windows sertifika deposuna yükle

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

`LocalMachine\My` altında `FriendlyName = Scadex Local Network API HTTPS`, `HasPrivateKey = True` görünmeli.
IIS'in *Server Certificates* ekranında yalnızca bu sertifika görünür; Root CA orada görünmez, bu normaldir.

### 2. API'yi yayınla

```powershell
dotnet publish Scadex.WebAPI -c Release -o C:\inetpub\scadex\api
```

Aşağıdakiler **git'te yoktur**. Yayın klasöründe olduklarını kontrol edin, yoksa elle kopyalayın:

- `C:\inetpub\scadex\api\MediaTools\ffmpeg\ffmpeg.exe` (+ `LICENSE.txt`; LGPL build olmalı)
- `C:\inetpub\scadex\api\MediaTools\mediamtx\mediamtx.exe` + `mediamtx.yml`
- `C:\inetpub\scadex\api\MapTiles\` (`Scadex.WebAPI\MapTiles` klasörünün tamamı)

### 3. Uygulama havuzu ve site (API)

```powershell
Import-Module WebAdministration
New-WebAppPool ScadexApi
Set-ItemProperty IIS:\AppPools\ScadexApi -Name managedRuntimeVersion -Value ""
Set-ItemProperty IIS:\AppPools\ScadexApi -Name startMode -Value AlwaysRunning
Set-ItemProperty IIS:\AppPools\ScadexApi -Name processModel.idleTimeout -Value "00:00:00"

New-Website ScadexApi -PhysicalPath C:\inetpub\scadex\api -ApplicationPool ScadexApi -Port 3009 -Ssl
Set-ItemProperty IIS:\Sites\ScadexApi -Name applicationDefaults.preloadEnabled -Value $true
```

`AlwaysRunning`, `idleTimeout = 0` ve `preloadEnabled` zorunludur: MediaMTX'i API başlatır. Bunlar olmazsa ilk
HTTP isteğine kadar MediaMTX açılmaz, boşta 20 dakika sonra da IIS uygulamayı kapatır.

3009 portunu başka bir site tutuyorsa `New-Website` hata verir; o siteyi kaldırın ya da ayarları o siteye uygulayın.

IIS Yöneticisi'nde:

- **Site → Bindings → https (3009) → Edit**:
  IP address *All Unassigned*, Host name **boş**, *Require Server Name Indication* **işaretsiz**,
  SSL certificate *Scadex Local Network API HTTPS*.
- **Site → SSL Settings**: *Require SSL* ✓, *Client certificates* = **Ignore**.

Ardından `iisreset`.

### 4. Klasör izinleri

```powershell
icacls C:\inetpub\scadex\api /grant "IIS AppPool\ScadexApi:(OI)(CI)M"
```

Havuz kimliğinin exe'leri çalıştırabilmesi, log ve kamera kayıtlarını yazabilmesi için gereklidir.

### 5. SQL Server girişi

Bağlantı dizesinde `Trusted_Connection=True` varsa uygulama SQL'e havuz kimliğiyle bağlanır. SSMS'te:

```sql
CREATE LOGIN [IIS AppPool\ScadexApi] FROM WINDOWS;
USE Scadex;
CREATE USER [IIS AppPool\ScadexApi] FOR LOGIN [IIS AppPool\ScadexApi];
ALTER ROLE db_owner ADD MEMBER [IIS AppPool\ScadexApi];
```

Yoksa uygulama açılışta `500.30` ile düşer. Alternatif: bağlantı dizesinde SQL kullanıcı adı/parola.

### 6. `appsettings.json`

`C:\inetpub\scadex\api\appsettings.json`. IIS'te ortam **Production**'dır; `appsettings.Development.json` okunmaz.

```jsonc
"Cors": {
  // React uygulamasının açıldığı adres: https://{arayüzIP}:{arayüzPort} — sonda "/" yok
  "Origins": [ "https://172.16.42.106:3000" ]
},
"MediaGateway": {
  // Operatör tarayıcısı canlı görüntüyü (kamera + PC) buradan çeker: https://{sunucuIP}:{mediamtx webrtc portu}
  // API portu DEĞİL
  "WebRtcPublicBaseUrl": "https://172.16.42.106:8889"
},
"Modules": {
  "RemoteDesk": {
    // Sahadaki PC'ler ekran görüntüsünü buradan MediaMTX'e gönderir: rtsp://{sunucuIP}:{mediamtx rtsp portu}
    "PublishRtspBaseUrl": "rtsp://172.16.42.106:8554"
  }
},
"TokenSettings": {
  // Git'teki değeri canlıda kullanmayın
  "SecurityKey": "<yeni anahtar>"
}
```

Yeni anahtar:

```powershell
$b = New-Object byte[] 64
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
[Convert]::ToBase64String($b)
```

Ayrıca kontrol edin: `ConnectionStrings:Database`, `Modules:Signalization:Enabled`, `Modules:RemoteDesk:Enabled`.

### 7. MediaMTX sertifikası ve `mediamtx.yml`

PEM dosyaları 1. adımda üretildi, dönüştürmeye gerek yok:

```powershell
copy C:\certs\scadex-server.key C:\inetpub\scadex\api\MediaTools\mediamtx\server.key
copy C:\certs\scadex-server.crt C:\inetpub\scadex\api\MediaTools\mediamtx\server.crt
```

`C:\inetpub\scadex\api\MediaTools\mediamtx\mediamtx.yml` içinde **var olan** satırları değiştirin (yeni satır eklemeyin):

```yaml
authHTTPAddress: https://172.16.42.106:3009/api/MediaGateway/auth
webrtcEncryption: true
webrtcServerKey: server.key
webrtcServerCert: server.crt
```

- `yes` değil `true` yazın.
- Sunucu Root CA'ya güvendiği için MediaMTX'in API'ye HTTPS çağrısı geçer.
- Yerel ağda çalışıldığı için `webrtcAdditionalHosts` ve `hosts` dosyası gerekmez.
- Değişiklikten sonra Görev Yöneticisi'nden `mediamtx.exe`'yi sonlandırın; API birkaç saniye içinde yml'nin yeni
  haliyle yeniden açar.

### 8. Güvenlik duvarı

```powershell
New-NetFirewallRule -DisplayName "Scadex API 3009"    -Direction Inbound -Protocol TCP -LocalPort 3009 -Action Allow
New-NetFirewallRule -DisplayName "Scadex Web 3000"    -Direction Inbound -Protocol TCP -LocalPort 3000 -Action Allow
New-NetFirewallRule -DisplayName "MediaMTX WebRTC"    -Direction Inbound -Protocol TCP -LocalPort 8889 -Action Allow
New-NetFirewallRule -DisplayName "MediaMTX ICE"       -Direction Inbound -Protocol UDP -LocalPort 8189 -Action Allow
New-NetFirewallRule -DisplayName "MediaMTX RTSP (PC)" -Direction Inbound -Protocol TCP -LocalPort 8554 -Action Allow
```

**9997 açılmaz.** Kuralları `-RemoteAddress` ile ilgili alt ağlara daraltmak önerilir
(8554 → PC ağları, 3000/3009/8889/8189 → operatör ve PC ağları).

### 9. Arayüz (React)

`Scadex.WebUI\.env.production`:

```ini
VITE_API_URL=https://172.16.42.106:3009
VITE_CLIENT_TYPE=a9537ece-1957-4bd0-8692-5da2c16e715b
# Backend'deki Modules:*:Enabled ile AYNI olmalı
VITE_MODULES=signalization,remotedesk
```

Derleyin ve `dist\` içeriğini `C:\inetpub\scadex\web\` altına kopyalayın:

```powershell
cd Scadex.WebUI
npm run build
```

`C:\inetpub\scadex\web\web.config` (sayfa yenilemede 404 almamak için):

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

Site:

```powershell
New-WebAppPool ScadexWeb
Set-ItemProperty IIS:\AppPools\ScadexWeb -Name managedRuntimeVersion -Value ""
New-Website ScadexWeb -PhysicalPath C:\inetpub\scadex\web -ApplicationPool ScadexWeb -Port 3000 -Ssl
```

Binding'de API ile **aynı** sertifikayı seçin (*Scadex Local Network API HTTPS*); sertifika IP'ye bağlıdır, port fark etmez.

### 10. İstemci bilgisayarlar

- `local-root-ca.crt`'yi **her operatör bilgisayarına** ve RemoteDesk istemcisi kurulu **her sahadaki PC'ye** yükleyin
  (istemci merkeze HTTPS ile bağlanır):

  ```powershell
  Import-Certificate -FilePath "C:\certs\local-root-ca.crt" -CertStoreLocation "Cert:\LocalMachine\Root"
  ```

- Chrome ve Edge Windows deposunu kullanır. Firefox kendi deposunu kullanır:
  `about:config → security.enterprise_roots.enabled = true`.
- RemoteDesk istemcisinin `appsettings.json`'unda: `"CentralApiUrl": "https://172.16.42.106:3009"`.

### 11. Kontrol

1. `https://172.16.42.106:3009/scalar` sertifika uyarısı olmadan açılıyor.
2. API logunda `MediaMTX baslatildi (PID …)` satırı var; Görev Yöneticisi'nde `mediamtx.exe` çalışıyor.
3. `https://172.16.42.106:3000` üzerinden giriş yapılıyor; bir alt sayfada F5'te 404 yok.
4. Bir kamerada canlı izleme açılıyor (MediaMTX + sertifika).
5. Aynı kamerada anlık görüntü alınıyor (FFmpeg).
6. Sahadaki PC listede çevrimiçi görünüyor ve ekranı izlenebiliyor.

### Sık hatalar

| Belirti | Neden |
|---|---|
| `500.19` / `0x8007000d` | Hosting Bundle ya da URL Rewrite eksik |
| `500.30` | Uygulama açılışta düşüyor. API `web.config`'inde `stdoutLogEnabled="true"`, `stdoutLogFile=".\logs\stdout"` yapın, `api\logs` klasörünü oluşturun, `stdout_*.log`'u okuyun (çoğunlukla SQL girişi ya da `appsettings`) |
| Canlı görüntü açılmıyor | `WebRtcPublicBaseUrl` portu 8889 ve `https` mi, `webrtcEncryption: true` mu, 8889/tcp ve 8189/udp açık mı |
| Çekimde "Medya geçidi çekim biletini reddetti" | `authHTTPAddress` yanlış ya da MediaMTX API'ye ulaşamıyor |
| Çekimde "FFmpeg bulunamadı" | `MediaTools\ffmpeg\ffmpeg.exe` yok ya da havuzun okuma/çalıştırma izni yok |
| Tarayıcıda sertifika uyarısı | O bilgisayara Root CA yüklenmemiş (Firefox'ta ayrıca enterprise roots) |
| Giriş sonrası oturum düşüyor / CORS hatası | `Cors:Origins` arayüzün adresiyle birebir eşleşmiyor (şema, port, sonda `/`) |
