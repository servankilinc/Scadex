# Scadex RemoteDesk — Production Readiness Raporu

**Tarih:** 9 Ekim 2026
**Kapsam:** Saha Windows istemcisi (`Scadex.RemoteDesk.Windows`), merkezi RemoteDesk modülü
(`Scadex.RemoteDesk`), paylaşılan sözleşmeler (`Scadex.RemoteDesk.Contracts`), planlanan servis
projesi (`Scadex.RemoteDesk.Windows.Service`); görüntü, dosya, pano ve uzaktan kontrol akışları.
**Amaç:** Mevcut çalışan MVP'yi güvenli, yönetilebilir ve arıza toleranslı üretim çözümüne taşımak.
**Yazıldığı okuyucu:** Proje sahibi ve RemoteDesk'i sahaya alacak ekip — yayın kararını verecek olanlar.
**Dayanak:** [RemoteDesk.md](../RemoteDesk.md) (mimari şartname), [CLAUDE.md](../CLAUDE.md) ve kod tabanı
(bulgular koda karşı doğrulandı — her maddede dosya referansı var).

## Yönetici özeti

> **Güncel durum (2026-10-26).** Aşağıdaki bulgular 9 Ekim tespitidir; her maddenin başındaki **Durum** notu bugünü söyler. Özet:
>
> - **Kapandı:** cihaz kimliği MAC yerine HTTP Basic Auth (P0.1'in kimlik kısmı); `PcHub` rate limit + Hello zaman aşımı (P0.2);
>   gerçek servis + SYSTEM Agent + arayüz ayrımı, kodda (P0.3); cihaza bağlı, 2 dk, tek kullanımlık transfer bileti (P0.5); atomik
>   yazma ve bağ/UNC/ADS reddi (P1.1, P0.4'ün yazma kısmı); geçici dosya süpürmesi (P1.2); izleme bitince aktarımın kesilmesi (P1.3);
>   pano için STA iş parçacığı (P1.4); operatöre girdi durumu gösterimi (P1.5); IIS gövde sınırı; modül HTTP uçlarında rate limit.
> - **Bu raporun önerisinin bilerek tersi (kullanıcı kararı):** UAC onay penceresi ve kilit ekranı uzaktan izlenir ve kontrol edilir
>   (P0.7, AnyDesk davranışı).
> - **Kabul edilen riskler (kullanıcı kararı):** cihaz kimliğinde HTTPS zorunlu değil (mTLS yerine Basic Auth); dosya gezme/okuma yerel
>   diskte serbest (izin listesi yok); PC'deki `appsettings.json` standart kullanıcıca okunabilir; tek merkez düğümü (P1.6).
> - **Açık:** güçlü parola / MFA (P0.6); kod imzalama ve temiz PC'ye kurulum testi; servis kurulumuyla saha sınamaları (oturum
>   değişimi, UAC/kilit, P1.5 gösterimi); denetim bütünlüğü, saklama, alarm ve olay müdahale kılavuzu (P2).

**9 Ekim tespiti.** Mevcut çözümün iyi temelleri vardır ve bunlar kodda doğrulanmıştır: görüntü ve komut düzlemleri
ayrılmış (video RTSP→MediaMTX→WHEP, komutlar SignalR); kontrol PC başına tek operatörle sınırlı ve
canlı izleme ön koşuluna bağlı (`RemoteControlCoordinator`); giriş olayları sunucuda filtreleniyor
(`code` tablo dışıysa atılıyor, paket başına ≤256 olay); dosya ve pano işlemleri izleme ön koşuluna
bağlı; dört ayrı izin tanımlı (`RemotePcView` / `RemotePcControl` / `RemotePcAudit` /
`RemotePcFileTransfer`); dosya baytları hub yerine biletli HTTP ile taşınıyor; denetim kayıtları
(`ScreenViewLog`, `RemoteControlSession`, `FileTransferLog`) yazılıyor.

O tarihte çözüm **kontrollü pilot** seviyesindeydi. Başlıca engeller (bugünkü durumları yukarıda ve her maddede):
anonim `PcHub` + MAC kimliği; iskelet Windows servisi; güvenli masaüstüne erişilememesi; sınırsız dosya okuma ve zayıf yazma
koruması; tekrar kullanılabilir transfer bileti; operasyonel sağlık, telemetri, HA ve parola politikası eksikleri.

---

## 1. Mevcut durumda doğru olanlar (kodla doğrulandı)

| Alan | Mevcut uygulama | Kanıt | Değerlendirme |
|---|---|---|---|
| Görüntü | SignalR kontrol düzlemi + MediaMTX/RTSP görüntü düzlemi ayrı | RemoteDesk.md § 3; `ScreenStreamCoordinator` | Doğru ayrım; video gecikmesi kontrol kanalını tıkamaz. |
| Kontrol | PC başına tek operatör, izleme ön koşulu, 5 dk boşta sonlandırma | `RemoteControlCoordinator`; § 12.7 | İyi temel; yetki sınırı anlaşılır. |
| Input | Sunucuda süzme: tablo dışı `code` atılır, paket başına ≤256 olay, koordinat/tekerlek kırpma | ViewerHub `SendInput`; § 12.7 | Sahadaki takılı tuş ve taşma riskini azaltır. |
| Dosya | Baytlar hub yerine biletli HTTP; 1 GB akış sınırı; denetim kaydı | `FileTransferController`, `FileTransferStore.MaxBytes` | Doğru yön; erişim ve bilet politikası eksik. |
| Yetki | 4 ayrı izin, policy controller/metot başına | `RemoteDeskModule`, `ViewerHub` metot policy'leri | Least-privilege için uygun; katmanlı. |
| İzleme ön koşulu | Dosya + pano işlemleri `IsViewingAsync` ile korunuyor | `FileTransferCoordinator.ResolveAsync`, `ClipboardCoordinator.ResolveAsync` | "Görmeden iş yapılmaz" ilkesi tutarlı uygulanmış. |
| Dayanıklılık | Geri çekilerek yeniden bağlanma (1/2/5/10/30 sn); FFmpeg Job Object | `CentralConnectionService`; `ChildProcessJob` | İstemci/FFmpeg yetim süreç riskini azaltır. |
| Denetim | Açılışta yetim oturum/transfer kapatma + periyodik süpürme | `ScreenStreamWorker` | Sunucu restart'ında kayıtlar tutarlı kapanır. |

---

## 2. Bulgu envanteri ve önerilen düzeltmeler

### P0 — Saha açılışından önce çözülmeli

#### P0.1 — Anonim PC hub + MAC kimliği

> **Durum (2026-10-25): kısmen kapandı.** MAC kimliği kaldırıldı; `PcHub` ve SCADA ingest/kart uçları **HTTP Basic Auth** ister
> (kullanıcı adı = cihaz Id'si, parola = sistemin ürettiği 128 bit sır, sunucuda SHA-256 — RemoteDesk.md § 5, CLAUDE.md
> "Saha cihazı kimliği"). mTLS yerine bu yol seçildi (kullanıcı kararı). Açık kalan: **HTTPS zorunlu değil** (kullanıcı kararı) —
> düz HTTP'de sır dinlenebilir; sertifika/rotasyon yok, sır panelden elle yenilenir. Aşağıdaki metin bu değişiklikten öncesini anlatır.

- **Etki:** MAC bilen saldırgan, gerçek PC bağlı değilken onun yerine bağlanıp operatöre sahte ekran
  gösterebilir; Faz 8+ ile operatörün o PC'ye yazdığı tuşları (parolalar dahil) toplayabilir.
- **Kanıt:** [PcHub.cs:22-23](../Modules/RemoteDesk/Scadex.RemoteDesk/Hubs/PcHub.cs#L22) — `[AllowAnonymous]`,
  `[DisableRateLimiting]`; MAC karşılaştırması bellekte (`Hello`, satır 59-69). Kaynakta TODO ile
  "güvenli bir yönteme geçilecek" yazılı.
- **Teknik çözüm:**
  1. **Cihaz enrollment + X.509 client sertifikası.** Kurulumda tek-kullanımlık enrollment kodu →
     merkez cihaza özel bir sertifika verir (Subject/SAN = `DeviceId`). Sertifika Windows Certificate
     Store'da (LocalMachine\My) saklanır; istemci config'inde parola/token taşınmaz.
  2. **mTLS.** `PcHub` ve RTSP publish yolları için Kestrel `ClientCertificateMode.RequireCertificate`.
     IIS/ters proxy arkasındaysa sertifika forwarding (`X-ARR-ClientCert` ya da Kestrel'e doğrudan
     TLS termination). Hub'ta `HttpContext.Connection.ClientCertificate` doğrulanır; Subject'ten çözülen
     `DeviceId` ile `Hello`'daki MAC **çapraz kontrol** edilir (MAC artık kimlik değil, envanter sinyali).
  3. **Sertifika yaşam döngüsü:** iptal listesi (CRL/OCSP ya da merkezde basit revocation tablosu),
     süre dolumu uyarısı (telemetri), rotasyon ucu. Sertifika öznesi `DeviceId`'ye bağlı olduğu için
     diyagramda cihaz silinip yeniden çizilirse yeniden enrollment gerekir — bu akış UI'da olmalı.
  4. **Geçiş:** anonim `Hello`/MAC yolu bir sürüm boyunca "uyarı modunda" (kabul edilir ama loglanır)
     tutulup, tüm sahalar sertifikaya geçince kapatılır. TODO satırları bu iş kapanınca silinir.

#### P0.2 — Anonim hub'da rate limit kapalı

> **Durum (2026-10-25): kapandı (PcHub).** IP başına dakikada 60 istek (`RemoteDeskModule.PcHubRateLimitPolicy`), Hello'suz bağlantı 10 sn'de
> kapanır; `UseRateLimiter` yetkilendirmeden önceye alındı (kimliksiz sel de sayılır). Sınandı: 70 kimliksiz negotiate → 60 × 401 + 10 × 429;
> Hello'suz bağlantı 10,0 sn'de kapandı. ViewerHub (kullanıcı JWT) ve modülün diğer HTTP uçları bilerek değişmedi.

- **Etki:** Sahte bağlantı, negotiate flood ya da büyük SignalR mesajıyla kaynak tüketimi; `PcHub` ve
  `ViewerHub` için `MaximumReceiveMessageSize` 4 MB'a çıkarılmış durumda, bu da flood yüzeyini büyütür.
- **Kanıt:** [PcHub.cs:23](../Modules/RemoteDesk/Scadex.RemoteDesk/Hubs/PcHub.cs#L23) `[DisableRateLimiting]`;
  [ServiceRegistration.cs:145-147](../Modules/RemoteDesk/Scadex.RemoteDesk/ServiceRegistration.cs#L145) 4 MB.
- **Teknik çözüm:**
  1. **Negotiate ucuna IP + cihaz bazlı limit.** ASP.NET `RateLimiter` ile `PartitionedRateLimiter`:
     IP başına sabit pencere (ör. 30 negotiate/dk), aşımda 429. Modül uçlarında rate limit "bilerek yok"
     kuralı (CLAUDE.md) **yalnızca HTTP controller'lar içindir**; hub negotiate'i için ayrı bir politika
     tanımlanabilir — politika adı `Program.cs`'te tanımlı olmalı (yoksa 500).
  2. **Bağlantı kotası:** cihaz başına tek aktif `PcHub` bağlantısı zaten var (`PcConnectionRegistry`);
     IP başına eşzamanlı negotiate kotası eklenir.
  3. **Mesaj frekans limiti:** `Hello` öncesi yalnızca `Hello` kabul edilir (mevcut), ama `Hello`
     kabul edilmeden önce bağlantı ömrü sınırlanmalı (ör. 10 sn içinde `Hello` gelmezse kapat).
  4. mTLS geldiğinde (P0.1) anonim yüzey daralır; yine de taşıma öncesi TLS el sıkışması maliyetine
     karşı bağlantı hız limiti kalır.

#### P0.3 — Windows Service uygulanmamış

> **Durum (2026-10-25): kod tamam, sahada sınanmadı.** Servis (SCM oturum olayları, `CreateProcessAsUser` ile SYSTEM Agent, Job Object,
> geri çekilmeli yeniden başlatma, SCM kurtarma) + Agent + yalnızca-arayüz WPF + ACL'li named pipe (SYSTEM + logon SID, oturum ve
> sunucu yolu doğrulaması, üç komut) + elle kurulum (`sc create`; betik 2026-10-25'te kaldırıldı) — RemoteDesk.md § 9. Fark: Agent yalnızca kullanıcı oturumunda çalışır
> (kullanıcı kararı); servis modu ve kurulum yönetici + merkez gerektirdiği için henüz denenmedi.

- **Etki:** Kullanıcı çıkışında istemci kaybolur (RemoteDesk.md § 9.7); çökme/kurtarma ve oturum
  geçişi güvenilir değil. Oturum açılmamış PC'ye erişilemez.
- **Kanıt:** [Worker.cs](../Modules/RemoteDesk/Scadex.RemoteDesk.Windows.Service/Worker.cs) saniyede log;
  Program.cs yalnızca `AddHostedService<Worker>()`. Bugün Görev Zamanlayıcı yolu kullanılıyor
  (`Install-StartupTask.ps1`).
- **Teknik çözüm (RemoteDesk.md § 9.8 Faz 11a ile uyumlu):**
  1. **LocalSystem supervisor servisi** (`Scadex.RemoteDesk.Windows.Service`): `.UseWindowsService()`,
     SCM failure actions (restart on crash), watchdog. Servis merkezle **konuşmaz**, yalnızca Agent'ı
     yönetir.
  2. **Session-bound headless Agent:** servis, WTS API (`WTSEnumerateSessions`,
     `WTSQueryUserToken`/`CreateProcessAsUser`) ile aktif konsol/RDP oturumunda penceresiz Agent'ı
     başlatır. Hub bağlantısı, ekran yakalama, `SendInput`, dosya ve pano Agent'tadır (bugünkü WPF
     servislerinin taşınması).
  3. **Oturum geçişi:** `WTSRegisterSessionNotification` ile login/logout/RDP connect/disconnect/lock
     izlenir; eski Agent kesin durdurulur, yeni oturumda yeni Agent kurulur.
  4. **WPF yalnızca gösterge + 3 zararsız komut** (Yeniden bağlan / Encoder sına / Tercih et); normal
     kullanıcı yetkisinde. SYSTEM yetkili pencere açılmaz (yerel yetki yükseltme açığı olurdu).
  5. **IPC:** `NamedPipeServerStream` + `PipeSecurity`; ACL yalnızca SYSTEM ve o oturumun logon SID'ine.
     Her istekte `SessionId` + logon SID + izin verilen message type doğrulanır; kullanıcı girdisiyle
     proses/komut satırı kurulmaz. Protokol versiyonlu sözleşmedir.

#### P0.4 — Dosyalar varsayılan olarak her yerden okunabilir

> **Durum (2026-10-25): kısmen.** Allowlist (varsayılan red) **seçilmedi** — gezme/okuma yerel diskte serbest kalır (kullanıcı kararı).
> Yapılan (`FilePathPolicy`): ağ ve aygıt yolları (UNC, `\\?\`, `\\.\`) ve ADS her yönde reddedilir; yazmada yalın dosya adı, ayrılmış ad
> reddi, yolda junction/sembolik bağ reddi, açılan dosyanın gerçek yolunun yeniden denetimi; korumalı köklere kurulum klasörü,
> `%ProgramData%\Scadex` ve oturum kullanıcısının Startup'ı eklendi.

- **Etki:** Hassas veri sızıntısı; yükseltilmiş süreç tüm disk + ağ paylaşımlarını okuyabilir.
- **Kanıt:** [FileTransferService.cs:33-41](../Modules/RemoteDesk/Scadex.RemoteDesk.Windows/Services/FileTransfer/FileTransferService.cs#L33)
  — `ProtectedRoots` yalnızca Windows/ProgramFiles/ProgramFilesX86/Startup/CommonStartup ve **yalnızca
  yazmada** (`DownloadFileAsync`); okuma/gezme/listeleme tümüyle serbest. `IsProtected` sadece prefix
  karşılaştırması; reparse point / junction / symlink / UNC / ADS denetimi yok.
- **Teknik çözüm:**
  1. **Cihaz bazlı allowlist kök klasörler, varsayılan RED.** Merkezde cihaz başına izin verilen
     kökler (ör. `D:\Paylasim`, kullanıcı profili alt klasörleri). Hem okuma/gezme hem yazma allowlist'e
     tabi. Allowlist merkezden gelir, istemcide saklanmaz.
  2. **Yol normalizasyonu + kaçış denetimi:** `Path.GetFullPath` (mevcut) + **reparse point reddi**
     (`FileAttributes.ReparsePoint` → junction/symlink engeli), **UNC reddi** (`\\server\share`),
     **ADS reddi** (`:` içeren yol), reserved device name (`CON`, `PRN`, `NUL`…), uzun yol (`\\?\`).
  3. **`IsProtected` zayıflığı:** bugünkü prefix karşılaştırması `C:\Windows` köküne `C:\Windows2`
     gibi kardeş klasörleri yanlış değerlendirebilir — `Path.DirectorySeparatorChar` ekiyle sınırlanmış
     (satır 175), bu doğru; ama allowlist modeline geçince bu kara liste tamamen yerini beyaz listeye bırakır.
  4. Denetimde her **listeleme de** opsiyonel loglanabilir (bugün yalnızca indir/yükle loglanıyor —
     `FileTransferCoordinator`); en azından allowlist dışı erişim denemeleri loglanmalı.

#### P0.5 — Transfer bileti tekrar kullanılabilir ve kriptografik bağlı değil

> **Durum (2026-10-25): kapandı.** İmzalı token yerine bilet **cihaz kimliğine bağlandı**: PC uçları (`PcFileTransferController`) PC'nin
> Basic Auth kimliğini ister; bilet komut anında üretilir, 2 dk, yalnızca o transferin cihazınca, tek kullanımlık (atomik). Sınandı:
> ikinci kullanım, kimliksiz, kart kimliği, kullanıcı JWT'si ve yanlış bilet 401; gerçek Agent'la iki yönde transfer çalıştı. SHA-256
> içerik bağı (P1.1'in kalanı) yok. Yayın bileti (`ScreenTicketStore`) bilerek değişmedi.

- **Etki:** Bilet ele geçirilirse 30 dk TTL boyunca aynı transferId için tekrar istek yapılabilir;
  bilet yön/cihaz/içerik hash'ine bağlı değil, yalnızca bellekte saklanan string ile eşleşir.
- **Kanıt:** [FileTransferStore.cs:78-79](../Modules/RemoteDesk/Scadex.RemoteDesk/FileTransfer/FileTransferStore.cs#L78)
  `ValidateTicket` salt `e.Ticket == ticket`; TTL `EntryTtl = 30 dk` (satır 20); `push`/`pull` uçları
  `[AllowAnonymous]`, yalnızca bileti doğrular
  ([FileTransferController.cs:79-106](../Modules/RemoteDesk/Scadex.RemoteDesk/Controllers/FileTransferController.cs#L79)).
  `ScreenTicketStore` yayın bileti de tek-kullanımlık değil (bu, FFmpeg reconnect için bilinçli) ama
  okuma bileti 60 sn TTL ile tek doğrulama yerine süre boyunca geçerli.
- **Teknik çözüm:**
  1. **Tek-kullanımlık, atomik tüketim.** `push`/`pull` ilk başarılı kullanımdan sonra bileti sil
     (`ConcurrentDictionary.TryRemove` ile atomik "al ve geçersiz kıl"). Dosya yönü başına tek push ve
     tek pull zaten yeterli; reconnect senaryosu dosya transferinde yok.
  2. **Kısa ömür.** TTL'i transfer ömrüne indir (ör. 2 dk + aktifken yenilenme); 30 dk yarım kalan için
     fazla uzun.
  3. **Kriptografik bağ (capability token).** Bileti düz GUID string yerine HMAC/JWT yap: içinde
     `transferId`, `direction`, `deviceId`, dosya SHA-256 (biliniyorsa), `exp`. Sunucu anahtarıyla imzalı;
     `push`/`pull` imzayı ve alanları doğrular. Böylece bilet başka yöne/cihaza/dosyaya kullanılamaz.
  4. **HA uyumu:** bilet deposu bugün `AddDistributedMemoryCache` (bellek, tek-düğüm —
     [Scadex.Core/ServiceRegistration.cs:40](../Scadex.Core/ServiceRegistration.cs#L40)); imzalı token
     stateless olduğu için çoklu düğümde de çalışır (bkz. P2 HA).

#### P0.6 — Zayıf parola politikası ve kimlik sertleştirme eksikleri

- **Etki:** `RemotePcControl`/`RemotePcFileTransfer` izinli hesap, istemci yükseltilmiş çalıştığı için
  o PC'de fiilen yönetici düzeyine sahiptir; bu hesabın parolası 4 karakter olabiliyor.
- **Kanıt:** [Program.cs:150-154](../Scadex.WebAPI/Program.cs#L150) `RequiredLength = 4`,
  `RequireDigit/NonAlphanumeric/Lowercase/Uppercase = false`. JWT 1440 dk (24 saat), izin claim'i
  token'a girişte yazılır (izin iptali yeniden girişe bağlı — `RemoteDeskModule` doc). MFA yok.
- **Teknik çözüm:**
  1. Ayrıcalıklı izinlere sahip roller için **güçlü parola** (uzunluk ≥ 12, karmaşıklık) ve tercihen
     **MFA** (TOTP). Lockout zaten var (7 deneme / 5 dk — Program.cs:144-146).
  2. **İzin iptalinin hızlı etkisi:** kontrol/dosya/pano gibi tehlikeli uçlar için token ömrünü kısalt
     (refresh ile) ya da iptal anında aktif hub bağlantılarını düşür.
  3. Bu, RemoteDesk'e özel değil Scadex geneli bir karardır; ayrıcalıklı modül açıkken asgari seviyenin
     yükseltilmesi önerilir.

#### P0.7 — UAC/secure desktop için gerçekçi olmayan beklenti

> **Durum (2026-10-25): bu raporun önerisinin TERSİNE, bilinçli kullanıcı kararıyla güvenli masaüstü desteklendi** (Faz 11b dar —
> RemoteDesk.md § 12.6). SYSTEM Agent aktif masaüstünü izler; UAC onay penceresi ve kilit ekranı görüntülenir ve uzaktan kontrol edilir
> (AnyDesk davranışı). Operatör UAC onaylayabilir — `RemotePcControl` izni o PC'de fiilen yönetici yetkisidir. Giriş ekranı ve
> Ctrl+Alt+Del hâlâ yok. Girdi reddi günlüğü artık masaüstü adını yazar; operatöre neden gösterimi (P1.5) açık. Sahada servis kurulumuyla
> sınanmadı.

- **Etki:** Operatör "kontrol çalışmıyor" zanneder veya güvenlik politikası gevşetilir.
- **Kanıt:** `SendInput` reddi UIPI'yi ayırt etmiyor
  ([RemoteInputService.cs:287-300](../Modules/RemoteDesk/Scadex.RemoteDesk.Windows/Services/Input/RemoteInputService.cs#L287));
  tek log satırı "yönetici penceresi, kilit ekranı ya da UAC olabilir" der (hangisi olduğunu bilmez).
- **Teknik çözüm:** § 4'te (ürün politikası) ve P1.5'te (desktop telemetri) ele alınıyor. Secure desktop
  **destek dışı durum** olarak modellenir; GPO ile UAC kapatmak çözüm değildir.

### P1 — İlk production sürümünde tamamlanmalı

#### P1.1 — İndirme hedefe doğrudan `FileMode.Create` ile yazılıyor

> **Durum (2026-10-25): PC tarafında kapandı** — `.scadex-part` + `Content-Length` denetimi + atomik taşıma; hata/iptalde parça silinir.
> SHA-256 (merkez desteği ister) ve merkez tarafındaki geçici dosya yazımı açık.

- **Etki:** Hata/bağlantı kaybında mevcut dosya bozulur (truncate edilir, yarım kalır).
- **Kanıt:** [FileTransferService.cs:149](../Modules/RemoteDesk/Scadex.RemoteDesk.Windows/Services/FileTransfer/FileTransferService.cs#L149)
  (PC'ye yazma) ve [FileTransferController.cs:113](../Modules/RemoteDesk/Scadex.RemoteDesk/Controllers/FileTransferController.cs#L113)
  (merkeze yazma) `FileMode.Create` doğrudan hedefe; SHA-256/boyut doğrulaması yok.
- **Teknik çözüm:** Aynı volume üzerinde `<hedef>.scadex-part` geçici dosyaya yaz → boyut + SHA-256
  doğrula (merkez beklenen hash'i capability token'dan ya da yan kanaldan bilir) → `File.Move(temp, dest,
  overwrite)` ile atomik rename. Hata/iptalde geçici dosya silinir, mevcut dosyaya dokunulmaz.

#### P1.2 — Merkez geçici deposu restart'ta temizlenmiyor

> **Durum (2026-10-25): kapandı.** `FileTransferStore.Sweep` 5 dk'da bir (ilki açılışın hemen ardından) kaydı olmayan ve son yazımı 10 dk'dan
> eski dosyaları siler. Açılışta tümünü silmek IIS'in eski/yeni süreç çakışmasında süren aktarımı bozacağı için seçilmedi. Disk kotası ve
> geçici alanın şifrelenmesi yapılmadı. Sınandı: bu makinede kalan 6 yetim dosya açılışta silindi.

- **Etki:** Sunucu çökerse `%TEMP%\scadex-remotedesk-ft` altında yetim dosyalar kalır; `Sweep` yalnızca
  bellekteki `_entries` üzerinde çalışır, restart sonrası dizin boştur → disk sızar.
- **Kanıt:** [FileTransferStore.cs:110-116](../Modules/RemoteDesk/Scadex.RemoteDesk/FileTransfer/FileTransferStore.cs#L110)
  `Sweep` `_entries.Values` üzerinde; restart'ta `_entries` boş, diskteki eski dosyalar taranmaz.
  (Not: `FileTransferLogStore.CloseOrphansAsync` DB kayıtlarını kapatır ama disk dosyalarını değil.)
- **Teknik çözüm:** Açılışta `_tempRoot`'taki tüm dosyaları yaşa göre temizle (ör. > `EntryTtl`); disk
  kotası ekle (kök boyutu sınırı, aşımda en eskiyi sil + alarm); geçici alanı mümkünse şifrele
  (EFS/BitLocker-korumalı volume). Başarısız temizlik alarm üretmeli.

#### P1.3 — Transfer/pano izleme ön koşulu başlatma anıyla sınırlı

> **Durum (2026-10-25): transferde kapandı.** `FileTransferCoordinator.SweepAsync` (`ScreenStreamWorker`, 5 sn) izlemesi düşen
> kullanıcının PC ayağını (`push`/`pull`) `FileTransferEntry.Abort` ile keser, denetimi başarısız kapatır, tarayıcıya bildirir. Sınandı:
> `pull` 40 MB'ın 22,9 MB'ında, `push` 34,5 MB'ta kesildi. Pano tek seferlik bir işlemdir (akış yok), ön koşul istek anında yeterli.

- **Etki:** İşlem başlarken izleme varsa ama ortada düşerse akış tamamlanabilir; "görmeden iş yapılmaz"
  politikası delinmiş olur.
- **Kanıt:** `ResolveAsync` yalnızca istek anında `IsViewingAsync` kontrol eder
  ([FileTransferCoordinator.cs:46-53](../Modules/RemoteDesk/Scadex.RemoteDesk/FileTransfer/FileTransferCoordinator.cs#L46)).
  Uzun bir `push`/`pull` sırasında izleme düşerse HTTP akışı iptal edilmiyor.
- **Teknik çözüm:** Transfer sahipliğini view lease'e bağla; lease düşünce ilgili `push`/`pull`
  `CancellationTokenSource`'u iptal et (akış kesilir), transferi `Failed` yap ve kullanıcıya bildir.
  `ScreenStreamCoordinator` lease düşüşünde `FileTransferCoordinator`'a sinyal verir.

#### P1.4 — Clipboard WPF dispatcher'a bağlı

> **Durum (2026-10-25): kapandı** — Agent'ın kendi STA iş parçacığı (WinForms `Clipboard`, 5 sn zaman aşımı).

- **Etki:** Faz 11a'daki penceresiz Agent'ta `Application.Current.Dispatcher` yoktur; pano çalışmaz.
- **Kanıt:** [ClipboardAccess.cs:63-68](../Modules/RemoteDesk/Scadex.RemoteDesk.Windows/Services/Clipboard/ClipboardAccess.cs#L63)
  `Application.Current?.Dispatcher`.
- **Teknik çözüm:** Ayrı, tek-thread'li **STA clipboard worker** (kendi `Thread` + `SetApartmentState(STA)`
  + mesaj pompası, ya da WinForms gizli pencere). Sınır (1.000.000 karakter, mevcut), timeout ve retry
  politikası korunur. Agent'a taşınırken WPF bağımlılığı kaldırılır.

#### P1.5 — `SendInput` dönüşü UIPI tanısı değil

> **Durum (2026-10-25): kapandı.** Agent girdi durumunu (`ControlInputState`: `SecureDesktop`, `SecureDesktopUnavailable`, `Rejected`,
> `MonitorMissing`) yalnızca değişince bildirir; merkez kontrol eden tarayıcıya iletir, araç çubuğunda eylem odaklı mesaj görünür
> (RemoteDesk.md § 12.6). Bütünlük düzeyi (yükseltilmiş pencere) tanısı yazılmadı: üretimde Agent SYSTEM olduğu için UIPI engeli oluşmaz.
> Gerçek Agent'ın kilit ekranı/UAC'de durumu üretmesi servis kurulumuyla sınanacak.

- **Etki:** Yanlış hata sınıflaması; operatöre belirsiz hata.
- **Kanıt:** P0.7 ile aynı kod.
- **Teknik çözüm:** Agent, sağlık mesajında aktif desktop/session/integrity durumunu raporlasın:
  `OpenInputDesktop`/`GetUserObjectInformation` ile aktif masaüstü adı (`Default`/`Winlogon`/`Screen-saver`),
  `GetTokenInformation(TokenIntegrityLevel)` ile hedef bütünlük. UI "yüksek pencere", "kilitli",
  "secure desktop", "oturum yok" durumlarını ayırsın ve eylem odaklı mesaj göstersin (§ 4 tablosu).

#### P1.6 — Tek merkez düğümü varsayımı

- **Etki:** Merkez restart'ı tüm canlı durumları (hub bağlantıları, lease, bilet, kontrol oturumu)
  düşürür; yatay ölçekleme yok.
- **Kanıt:** Tüm koordinatörler singleton + bellek (`PcConnectionRegistry`, `ScreenStreamCoordinator`,
  `RemoteControlCoordinator`, `FileTransferStore`); cache in-memory (P0.5 kanıtı); RemoteDesk.md § 6.1
  "tek sunucu varsayımı".
- **Teknik çözüm:** İlk aşamada kontrollü bakım + istemci yeniden bağlanma (zaten var, geri çekilmeli).
  Sonraki aşamada P2 HA (Redis backplane + dağıtık store).

### P2 — Operasyon kalitesi ve savunma derinliği

- **Kod imzalama:** EV/kurumsal code-signing sertifikası; uzaktan erişim yazılımları AV'lerce sık
  işaretlenir (RemoteDesk.md § 9.6 "sertifika gelince"). İmza doğrulayan kurulum, sürüm envanteri.
  Faz 11a'dan sonra (SYSTEM + ekran + girdi) daha da kritik.
- **Güncelleme:** "Kendiliğinden güncelleme yok" kararı korunur (§ 9.6); manuel paket imzalı ve hash
  doğrulanabilir olmalı. Kontrollü rollout/ring, geri alma, bakım penceresi.
- **AV/kötücül tarama:** Yüklenen dosya PC'ye yazılmadan önce Windows Defender (AMSI / `IAttachmentExecute`
  ya da komut satırı taraması) ile taransın; enfekte dosya reddedilsin ve loglansın.
- **Gövde sınırı (Kestrel/IIS):** *(2026-10-25: kapandı — `Scadex.WebAPI/web.config` `requestLimits maxAllowedContentLength` = 1 GB + 1 MB;
  yayındaki birleşmiş `web.config` doğrulandı, IIS'te büyük aktarım sahada denenecek.)* `filetransfer` ucu `[DisableRequestSizeLimit]` + akışta 1 GB zorlar
  ([FileTransferController.cs:31-32](../Modules/RemoteDesk/Scadex.RemoteDesk/Controllers/FileTransferController.cs#L31)),
  ama IIS arkasında `web.config maxAllowedContentLength` ve Kestrel `MaxRequestBodySize` ayrı sınırlardır;
  deploy'da uyumlu ayarlanmalı (depoda web.config yok — IIS kullanılacaksa eklenmeli).
- **Gözlemlenebilirlik:** OpenTelemetry (.NET), Windows Event Log, merkezi log toplama, metrikler ve
  alarm kuralları. Sunucu logları bugün Serilog; `Microsoft.AspNetCore` Warning seviyesinde.
- **Merkezi denetim bütünlüğü:** oturum başlangıcı/bitişi, yetki değişikliği, cihaz sertifikası olayı,
  dosya özeti/hash'i, başarısız policy denemeleri ve servis işi sonucu **değiştirilemez** biçimde
  (append-only / imzalı) saklanmalı. Bugün `ScreenViewLog`/`RemoteControlSession`/`FileTransferLog`
  standart tablolar; saklama/temizlik işi yok (Scadex geneli karar).
- **Sırlar:** sertifikalar Windows Certificate Store'da; config'de parola/kalıcı token olmamalı
  (istemci config bugün yalnızca `CentralApiUrl` taşıyor — doğru).

---

## 3. Hedef mimari

```text
                 Merkez Scadex
 ┌──────────────────────────────────────────────────────────┐
 │ API + SignalR + cihaz kayıt/sertifika otoritesi            │
 │ Yetki motoru · denetim · transfer capability servisi       │
 └───────────────▲───────────────────────────┬───────────────┘
                 │ mTLS / TLS                │ kullanıcı JWT (+ MFA)
                 │                            │
 ┌───────────────┴────────────────────────────▼──────────────┐
 │ Saha PC                                                    │
 │  LocalSystem Supervisor Service                            │
 │   - SCM recovery, WTS oturum olayları, sağlık, sertifika   │
 │   - session worker (Agent) yaşam döngüsü                   │
 │   - kataloglanmış yönetim işleri                           │
 │                                                            │
 │  Session Agent (aktif oturum, penceresiz, SYSTEM)          │
 │   - görüntü yakalama, SignalR (Hello), kontrollü SendInput │
 │   - dosya/pano, session değişiminde güvenli duruş          │
 │                                                            │
 │  Normal yetkili WPF Tray                                   │
 │   - yalnızca durum/uyarı + 3 zararsız operatör komutu      │
 │                                                            │
 │  Named pipe: SYSTEM + ilgili logon SID, kimlik doğrulamalı │
 └────────────────────────────────────────────────────────────┘
```

Bu, RemoteDesk.md § 9.8 (Faz 11a) ile birebir uyumludur; farkı, kimlik kökünün mTLS'e taşınmasıdır.

### Ayrıcalık modeli

1. WPF hiçbir zaman yönetici/SYSTEM kimliğiyle çalışmaz.
2. Görüntü ve pano gibi kullanıcı profili gerektiren işler Agent'ta (kullanıcı oturumu, SYSTEM) çalışır.
3. Yükseltilmiş pencere kontrolü gerekiyorsa yalnızca input için başsız, dar API'li yüksek-integrity
   yardımcı kullanılır; bu yardımcı dosya sistemi, shell, genel pipe komutu veya UI barındırmaz.
4. Servis masaüstüne pencere çizmez; Session 0 izolasyonu korunur (interaktif servis kullanılmaz —
   [Microsoft: Interactive Services](https://learn.microsoft.com/windows/win32/services/interactive-services)).
5. Her IPC isteğinde logon SID, SessionId ve izin verilen message type doğrulanır; kullanıcı girdisiyle
   proses/komut satırı oluşturulmaz.

---

## 4. UAC ve UIPI için ürün politikası

| Durum | Beklenen davranış | Operatöre gösterilecek durum |
|---|---|---|
| Normal uygulama | Görüntü ve kontrol desteklenir. | "Kontrol etkin" |
| Yükseltilmiş pencere | Dar yüksek-integrity input worker mevcutsa desteklenir (bugün: istemci Görev Zamanlayıcı ile yükseltilmiş). | "Yükseltilmiş pencere kontrolü" |
| Kilit ekranı / giriş ekranı | Görüntü ve input durdurulur. | "Etkileşimli masaüstü yok" |
| UAC secure desktop | Uzaktan tıklama/tuş gönderilmez. | "UAC yerel onay bekliyor" |
| Ctrl+Alt+Del | Destek dışıdır. | "Güvenli dikkat dizisi uzaktan gönderilemez" |

`SendInput` yalnızca eşit veya düşük integrity hedeflere input enjekte edebilir ve API sonucu UIPI
bloklamasını güvenilir ayırt etmez
([SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)). Bu
yüzden durum ayrımı aktif desktop/integrity telemetrisiyle yapılmalıdır (P1.5).

**Önerilen alternatif:** UAC isteyen rutin işler için uzaktan ekrana tıklamak yerine servis tarafından
yürütülen imzalı/kataloglanmış bakım işleri (RemoteDesk worker yeniden başlatma, teşhis paketi, onaylı
sürüm kurulumu). Genel PowerShell/CMD/arbitrary executable kanalı **eklenmez**.

UAC secure desktop'ı GPO ile kapatmak ya da `Never notify` kullanmak production çözümü değildir
([UAC settings](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/settings-and-configuration)).

---

## 5. Teknoloji seçimleri

| İhtiyaç | Önerilen teknoloji | Neden |
|---|---|---|
| PC kimliği | X.509 client certificate, mTLS, Windows Certificate Store | MAC yerine kriptografik cihaz kimliği; iptal/rotasyon. |
| Servis | .NET Worker Service + `UseWindowsService()` + SCM recovery | Kullanıcıdan bağımsız yaşam döngüsü. |
| Oturum yönetimi | WTS API (`WTSRegisterSessionNotification`, `WTSEnumerateSessions`, `WTSQueryUserToken`) | Konsol/RDP/login/logout/lock doğru izleme. |
| Agent başlatma | `CreateProcessAsUser`/WTS token akışı; session ve desktop doğrulaması | Agent'ın doğru interaktif oturumda çalışması. |
| IPC | `NamedPipeServerStream` + `PipeSecurity` + SID/session doğrulaması | Yerel, dar, erişim kontrollü protokol. |
| Dosya bütünlüğü | SHA-256, temp-file, atomic rename, `FileOptions.Asynchronous` | Yarım/değiştirilmiş dosyayı hedefe almama (P1.1). |
| Transfer bileti | HMAC/JWT capability (yön+cihaz+transfer+hash+exp), atomik tüketim | Replay ve yanlış-hedef kullanımını önleme (P0.5). |
| AV/kötücül kontrol | Microsoft Defender/AMSI tarama entegrasyonu | Dosya yükleme tehdidini azaltma. |
| Telemetri | OpenTelemetry .NET, Serilog, Windows Event Log, merkezi collector | Sahada teşhis ve alarm. |
| Dayanıklılık | Polly timeout/retry/circuit breaker; bounded channels | Ağ arızasında kontrollü geri deneme, bellek koruması. |
| HA (sonraki aşama) | Redis SignalR backplane + distributed cache/store | Çoklu düğümde canlı durum ve bilet sürekliliği. |

---

## 6. Adım adım uygulama planı

### Faz A — Güvenlik temeli (yayın bloklayıcı)

1. Tehdit modeli: operatör, saha kullanıcısı, ağ saldırganı, çalınmış cihaz, kötücül dosya senaryoları.
2. Cihaz kayıt akışı: tek-kullanımlık enrollment kodu, merkezden sertifika alma, cihaz onayı,
   iptal/yenileme ekranı.
3. `PcHub` için mTLS zorunlu; anonim `Hello`/MAC akışını kademeli kapat (önce uyarı modu).
4. Hub negotiate ve HTTP uçlarına rate limit, payload kotası, bağlantı kotası, correlation ID.
5. Dosya erişimini cihaz bazlı allowlist'e çevir (varsayılan red); reparse/UNC/ADS/symlink reddi.
6. Transfer biletlerini tek-kullanımlık + kısa ömürlü + imzalı capability yap.
7. Ayrıcalıklı roller için güçlü parola/MFA; izin iptalinin aktif bağlantıya hızlı etkisi.

**Çıkış kriteri:** MAC taklidi ya da eski ticket replay ile PC hub'a/transfer ucuna erişim mümkün değil;
reddedilen tüm denemeler denetim kaydında görünür.

### Faz B — Dosya aktarımı ve komut güvenliği

1. İndirme/yükleme için SHA-256, byte count, geçici dosya, atomik taşıma (P1.1).
2. Reparse point, symbolic link, UNC, ADS, reserved device name, uzun yol, `..` kaçış testleri.
3. View lease sona erince aktif transferi iptal et + kullanıcıya bildir (P1.3).
4. Dosya türü/boyutu/kota/virüs tarama politikası + retention; merkez temp restart temizliği (P1.2).
5. Genel shell komutu eklenmez; gerekli operasyonlar için komut kataloğu, parametre şeması, çift onay,
   zaman kısıtı.

**Çıkış kriteri:** Ağ kesintisi mevcut dosyayı bozmaz; izinli kök dışı erişim, bağlantı noktası kaçışı
ve tekrar kullanılan bilet reddedilir.

### Faz C — Servis, Agent ve WPF ayrımı (RemoteDesk.md Faz 11a)

1. Service projesini gerçek Windows service'e çevir; SCM failure actions, watchdog, sağlık endpointi.
2. WTS olaylarını dinle; her aktif console/RDP oturumu için doğru Agent'ı başlat, eskiyi kesin durdur.
3. Agent penceresiz; WPF normal kullanıcı yetkisinde ayrı.
4. Pipe protokolünü versiyonlu sözleşme yap; yalnızca durum + 3 zararsız komut kabul et.
5. Clipboard için bağımsız STA worker (P1.4); WPF dispatcher bağımlılığını kaldır.
6. `Install-Service.ps1` idempotent + code-signature + hash doğrulaması; Görev Zamanlayıcı yolunu kaldır.

**Çıkış kriteri:** Logout/login, RDP connect/disconnect, kilit açma, Agent çökmesi ve PC restart sonrası
doğru oturumda tek Agent çalışır; kullanıcı WPF'yi kapatsa bile uzaktan bağlantı davranışı tanımlı kalır.

### Faz D — UAC/UIPI üretim davranışı

1. Aktif masaüstü, session ve integrity durumunu Agent sağlık mesajına ekle (P1.5).
2. Secure desktop/kilit algılanınca input kuyruğunu boşalt, basılı tuşları bırak, görüntüyü
   `ScreenLocked`/`SecureDesktop` durumuna taşı.
3. Operatör ekranında teknik hata yerine eylem odaklı mesaj (§ 4 tablosu).
4. Yönetici pencere kontrolü gerekiyorsa sınırlı yüksek-integrity input helper ile test; gerekmiyorsa
   normal Agent ile devam ederek saldırı yüzeyini küçült.
5. UAC isteyen bakım senaryolarını kataloglanmış servis işlerine taşı.

**Çıkış kriteri:** UAC/kilit ekranında hiçbir tuş başka pencereye sızmaz; operatör ve denetim durumun
nedenini açıkça gösterir.

### Faz E — Operasyon, gözlemlenebilirlik ve HA

1. Metrikler: bağlı cihaz, aktif oturum, görüntü gecikmesi, yeniden bağlanma, transfer hata oranı,
   certificate expiry, disk kota kullanımı.
2. Alarmlar: sertifika bitişi, Agent crash loop, policy reddi artışı, ticket replay, disk alanı, merkez
   bağlantı kaybı.
3. Loglarda PII/clipboard/ticket/parola bulunmadığını otomatik denetle (bugün bilet loglanmıyor, pano
   loglanmıyor — bu korunmalı).
4. Yedekleme, retention, incident runbook, saha destek prosedürü.
5. Çoklu düğüm gerekirse Redis backplane + dağıtık session/ticket store ile ayrı HA fazı.

**Çıkış kriteri:** Cihaz arızası ya da kontrollü merkez restart'ı gözlenebilir ve geri alınabilir;
operasyon ekibi günlük dosyası istemeden olayın nedenini merkezden görebilir.

---

## 7. Production kabul test matrisi

| Kategori | Zorunlu senaryolar |
|---|---|
| Kimlik | Geçerli sertifika, iptal edilmiş sertifika, yanlış cihaz sertifikası, MAC taklidi, TLS downgrade, enrollment kodu tekrar kullanımı |
| Yetki | View-only, control-only, file-only, audit-only, yetki kaldırma (aktif bağlantıya etki), MFA, iki operatör yarışması |
| Masaüstü | Normal, yükseltilmiş uygulama, UAC prompt, lock/unlock, login, RDP bağlan/kes, hızlı kullanıcı değiştirme |
| Dayanıklılık | Ağ kesintisi, DNS/TLS hatası, merkez restart (temp temizliği), Agent kill, servis restart, PC restart |
| Dosya | Büyük dosya (~1 GB sınır), yarım akış, hash hatası, dolu disk, izinli/izinsiz kök, reparse point, UNC/ADS, aynı isimli dosya (atomik replace), virüslü dosya |
| Güvenlik | Ticket replay, tek-kullanım sonrası ticket, pipe'a başka kullanıcı erişimi, flood/rate limit, bozuk SignalR paketi, audit bütünlüğü |
| Geri dönüş | Yeni sürüm başarısız, sertifika yenileme başarısız, policy hatası, kurulum kaldırma/yeniden kurma |

---

## 8. Yayın kapıları

Production'a geçiş aşağıdakiler olmadan yapılmamalıdır:

- [ ] mTLS cihaz kimliği ve sertifika iptal/rotasyon akışı (P0.1).
- [ ] Allowlist tabanlı dosya politikası + reparse/UNC/ADS reddi + atomik/hash doğrulamalı transfer (P0.4, P1.1).
- [x] Tek-kullanımlık, cihaza bağlı, kısa ömürlü transfer bileti (P0.5) — 2026-10-25.
- [~] Gerçek Windows service + Agent + IPC ACL + çökme kurtarma (P0.3, Faz C) — kod 2026-10-25, sahada kurulum denemesi bekliyor.
- [~] Secure desktop/UIPI için belgelenmiş, test edilmiş, kullanıcıya görünür davranış (§ 4, P1.5) — 2026-10-25: belgelendi ve operatöre
  gösteriliyor; servis kurulumlu PC'de sınanacak.
- [x] Merkez geçici deposunun yeniden başlamada temizlenmesi (P1.2) — 2026-10-25.
- [x] İzleme bitince PC tarafı dosya aktarımının kesilmesi (P1.3) + IIS gövde sınırı (P2) — 2026-10-25.
- [x] Hub negotiate rate limit + Hello zaman aşımı (P0.2) — 2026-10-25 (`PcHub`).
- [ ] Ayrıcalıklı roller için güçlü parola/MFA (P0.6).
- [ ] İmzalı binary/kurulum + temiz Windows PC kurulum testi (P2).
- [ ] Kritik güvenlik ve arıza matrisi otomasyonla ya da kayıtlı manuel testle geçmiş.
- [ ] Alarm, denetim bütünlüğü, retention ve incident runbook hazır.

## Sonuç

Kısa vadede Görev Zamanlayıcı ile yüksek ayrıcalıklı WPF, normal ve yükseltilmiş uygulamalar için
kullanılabilir; fakat bu model kalıcı saha standardı olmamalıdır. Üretim hedefi: kriptografik cihaz
kimliği olan, servis tarafından yönetilen, kullanıcı arayüzünden ayrılmış, dar yetkili Agent'lar
kullanan ve secure desktop sınırına saygı duyan bir mimaridir.

En önemli iki tasarım kararı:

1. **UAC güvenli masaüstünü uzaktan tıklamaya çalışmayın.** Güvenli masaüstü gerektiren işlemleri yerel
   onaya veya denetlenebilir, kataloglanmış bakım işlerine dönüştürün.
2. **Cihaz güven kökünü ve dosya politikasını servise geçişten ÖNCE düzeltin.** Agent SYSTEM yetkisiyle
   çalışacağı için, anonim MAC kimliği ve sınırsız dosya okuma riskleri servisle birlikte **büyür**;
   önce P0 kapanmalı, sonra Faz 11a mimarisi kurulmalıdır.
