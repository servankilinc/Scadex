# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Bu dosya ne değildir

Bu dosya bir özet değildir. Sistemin **ne yaptığı, hangi kararın neden verildiği ve neyin
bilinçli olarak eksik bırakıldığı** [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) içinde yazar —
600 satırlık o doküman tek doğruluk kaynağıdır. Buradaki içerik yalnızca **koda dokunmadan
önce bilinmesi gerekenlerdir**: çalışma dili, komutlar, katman yönü ve tek bir dosyaya bakarak
asla fark edilemeyecek kurallar.

Ek kaynaklar: [Docs/scada_communication_guide.md](Docs/scada_communication_guide.md) (SCADA
protokol detayı), [Docs/data-structor.md](Docs/data-structor.md) (veri modeli notları),
[README.md](README.md) (sahaya kurulum rehberi — IIS, migration'lar, `sc.exe` servis kaydı; kurulum adımı
değiştiren bir iş yaparsanız ilgili bölümü de güncelleyin).

Scadex kısaca: sahadaki bir SCADA'nın **üstünde** çalışan süpervizyon platformu. Modbus/seri
port konuşmaz; SCADA ile HTTP üzerinden yalnızca `{ kabin, pin adresi (IN7/OUT5), değer }`
üçlüsünü değiş tokuş eder. Anlam koda gömülmez, operatörün çizdiği pano şemasından okunur.

## Çalışma dili

**Türkçe yazın.** Yorumlar, XML doc'lar, hata mesajları, commit mesajları ve dokümanlar
Türkçe; sınıf / metot / DTO / değişken isimleri İngilizce kalır. Mevcut bir dosyayı
düzenlerken bu ayrımı bozmayın.

## Komutlar

```bash
# Backend (depo kökünden)
dotnet build Scadex.slnx
dotnet run --project Scadex.WebAPI          # http://localhost:5208 + https://localhost:7167
                                            # OpenAPI: /openapi/v1.json — Scalar UI: /scalar

# Üç DbContext var: --context ZORUNLU (vermezseniz "More than one DbContext was found")
dotnet ef migrations add <Ad> --project Scadex.DataAccess --startup-project Scadex.WebAPI --context AppDbContext
dotnet ef database update     --project Scadex.DataAccess --startup-project Scadex.WebAPI --context AppDbContext

# Sinyalizasyon modülü (signalization şeması, kendi migration geçmişi)
dotnet ef migrations add <Ad> --project Scadex.Signalization --startup-project Scadex.WebAPI --context SignalizationDbContext --output-dir Data/Migrations
dotnet ef database update     --project Scadex.Signalization --startup-project Scadex.WebAPI --context SignalizationDbContext

# RemoteDesk modülü (remotedesk şeması, kendi migration geçmişi — RemoteDesk.md)
dotnet ef migrations add <Ad> --project Modules/RemoteDesk/Scadex.RemoteDesk --startup-project Scadex.WebAPI --context RemoteDeskDbContext --output-dir Data/Migrations
dotnet ef database update     --project Modules/RemoteDesk/Scadex.RemoteDesk --startup-project Scadex.WebAPI --context RemoteDeskDbContext

dotnet user-secrets set "TokenSettings:SecurityKey" "<64+ karakter>" --project Scadex.WebAPI
```

```bash
# Frontend (Scadex.WebUI/ içinden)
npm install
npm run dev      # :5173 — appsettings.json > Cors:Origins ile eşleşmek zorunda
npm run build    # tsc -b && vite build  → tip kontrolü BUNUN içindedir
npm run lint
```

Bilinmesi gerekenler:

- **`Scadex.WebUI` çözüme dahil değildir.** `Scadex.slnx` 10 .NET projesini taşır (5 çekirdek katman +
  `Scadex.Signalization` + `Modules/RemoteDesk/` altındaki dört proje); frontend ayrı çalıştırılır. Çözümde PC tarafı
  (`Scadex.RemoteDesk.Windows.Service` + `Scadex.RemoteDesk.Windows`, `net10.0-windows`) olduğu için `dotnet build Scadex.slnx` yalnızca Windows'ta derlenir.
- **Test paketi yoktur.** Otomatik kontrol yalnızca `dotnet build` ve frontend tarafında
  `npm run lint` + `npm run build`. Davranış, uygulamayı çalıştırarak doğrulanır — bir
  değişikliğin çalıştığını iddia etmeden önce gerçekten çalıştırın. (`npm run lint` ve
  `npm run build` bugün yeşil; yeşil kalmalı.)
- **`npm run typecheck` diye bir script yoktur** (`build` zaten `tsc -b` çalıştırır).
- **MediaMTX'i WebAPI başlatır ve kapanırsa yeniden başlatır** (`MediaMtxSupervisorWorker`,
  2026-09-27). Exe yolu `MediaGateway:MediaMtxPath` (varsayılan
  `MediaTools/mediamtx/mediamtx.exe`); yapılandırma exe ile aynı klasördeki `mediamtx.yml`'dir
  — yml yoksa **başlatılmaz** (varsayılanlarla auth kapalı açılırdı). `mediamtx.exe` Windows servis
  protokolünü uygulamadığı için `sc create` ile servis yapılamaz (1053); ayrıca servis/elle çalıştırmayın,
  aynı portları tutar. Scadex kapanırken MediaMTX **bilerek öldürülmez**; sonraki açılış aynı yoldan
  çalışanı sahiplenir. Çıktısı Serilog'a `[MediaMTX]` önekiyle yazılır (yml'de `logDestinations: [stdout]`
  kalmalı). MediaMTX **kameraya bağlanan tek bileşendir**: canlı izleme de, FFmpeg'in anlık görüntü/klip
  okuması da onun `cam_{id}_main` yolundan geçer. Portlar yml'dedir ve `appsettings.json > MediaGateway`
  ile **elle senkron** tutulur; eşleşme listesi yml'nin en başındadır. Scadex'in kullanmadığı protokoller
  (rtmp/hls/srt/moq, RTSP udp/multicast) yml'de port açmasın diye kapalıdır — açmayın.
- **FFmpeg'i uygulama kendisi çağırır** (anlık görüntü + klip, `CameraCaptureGateway`). Yol
  `MediaGateway:FfmpegPath` ayarıdır (varsayılan `MediaTools/ffmpeg/ffmpeg.exe`, göreliyse
  `ContentRootPath` altından). **`MediaTools/` git'te yoktur** — FFmpeg, MediaMTX ve `mediamtx.yml` dahil
  (`.gitignore`; exe GitHub'ın 100 MiB sınırını aşıyor) — yeni klonda ve her sunucuda elle konur,
  yoksa çekimler "FFmpeg bulunamadı" ile düşer, MediaMTX başlamaz. Yayına `ffmpeg.exe`, `mediamtx.exe`
  ve `mediamtx.yml` kopyalanır (csproj). **Build LGPL olmalı (2026-09-29):** sunucu ve RemoteDesk istemcisi aynı
  exe'yi kullanır (BtbN 8.1 LGPL, yanında `LICENSE.txt`); çekirdek yalnızca RTSP okuma, `mjpeg` ve `-c copy`
  kullanır, GPL (x264/x265) gerektirmez. GPL build koymayın — müşteriye dağıtımda kaynak sunma yükümlülüğü doğar.
- **Harita zemini tamamen yereldir (2026-09-28): `Scadex.WebAPI/MapTiles/` git'te yoktur** — `basemap.pmtiles`
  (Protomaps şeması, Türkiye, z0–13) + `fonts/Noto Sans {Regular,Medium,Italic}` + `sprites/v4/{light,dark}`
  (`protomaps/basemaps-assets`) her sunucuya elle konur; yoksa uyarı loglanır ve harita zemini boş görünür.
  WebAPI bunları `/basemap` altında `UseCors`'tan **sonra** servis eder (tarayıcı başka origin'den Range ile
  okur; ilk `UseStaticFiles` CORS'tan önce olduğu için oraya taşımayın). Stil `src/lib/map-basemap.ts`'tedir;
  şema Protomaps olduğu için VersaTiles/Carto/OpenMapTiles stilleri bu dosyayla çalışmaz. Konum seçicideki
  adres araması (Nominatim) hâlâ internet ister.

## Mimari

```
Core → Model → DataAccess → Business → WebAPI
```

Bu ok **tek yönlüdür ve kırılmaz.** Her katmanın kendi `ServiceRegistration.AddXServices()`
uzantısı vardır; `Program.cs` bunları sırayla çağırır.

- **Core** — `Result` / `Result<T>`, `IQueryable` üzerinde dinamik filtre/sıralama/sayfalama/
  datatable motoru, `ProjectJsonOptions`, cache, localization, FluentValidation altyapısı, Serilog.
- **Model** — entity'ler, lifecycle arayüzleri (`Core/Model/IEntity.cs`),
  `Dtos/<Aggregate>/{Commands,Queries}/`, `Enums/EntityEnums.cs`.
- **DataAccess** — `AppDbContext` (Identity tabanlı), generic `RepositoryBase`, entity başına
  repository, hepsini + transaction'ı taşıyan `IUnitOfWork`, üç `SaveChanges` interceptor'ı.
- **Business** — aggregate başına bir servis (`Abstract/I*Service.cs` + `Concrete/*Service.cs`),
  `Result<T>` döner, `IValidationService` ile doğrular, AutoMapper `ProjectTo` ile projeksiyon
  yapar. Dış dünyaya açılan gateway'ler `Utils/` altında.
- **WebAPI** — `BaseController`'dan türeyen ince controller'lar, `DiagramHub`, hosted
  service'ler, exception middleware.

### Müşteri modülleri — `Scadex.Signalization`

```
Core → Model → DataAccess → Business → WebAPI
                                ↑          │
                    Scadex.Signalization ←─┘   (yalnızca WebAPI referans alır, Program.cs kaydeder)
```

Firmaya özel iş (sinyalizasyon operatör işlemi takibi) çekirdeğe **girmez**, bu class library'de
durur. Ayrıntı: PROJECT_OVERVIEW.md § 10.

- **Modül kurulum başına açılır: `Modules:Signalization:Enabled`** (tanımsız = kapalı).
  `appsettings.json`'da `false`, `appsettings.Development.json`'da `true`; müşteri kurulumu kendi
  ortam dosyasında ya da `Modules__Signalization__Enabled=true` ile açar. Firma kimliği kodda
  sorulmaz. Kayıt `Program.cs`'te `AddControllers()` zincirindedir
  (`.AddSignalizationModule(configuration)`), çünkü kapalıyken **iki şey birden** yapılmalı:
  servisler/arka plan işleri/observer kaydedilmez **ve** modülün controller'ları ApplicationPart
  listesinden çıkarılır (uçlar 404, OpenAPI'de yok). Yalnızca servisleri koşullamak yetmez —
  assembly referans olduğu için controller'lar otomatik keşfedilir ve DI hatasıyla her istekte 500
  döner. **Yeni modül = aynı kalıp**: `Modules:<Ad>:Enabled` + `.Add<Ad>Module(...)` tek satırı;
  reflection ile modül keşfi yapmayın. Frontend'deki `VITE_MODULES` bunun aynasıdır ve **ayrıca**
  ayarlanır.
- **Modülün SignalR hub'ı varsa kaydı iki satırdır:** `.Add<Ad>Module(...)` servisleri, `app.Map<Ad>Module(...)`
  hub'ı eşler (`Program.cs`, `MapHub<DiagramHub>`'ın yanında); ikisi de `Enabled`'a bakar. Hub'lar
  controller'lar gibi otomatik keşfedilmez, kapalıyken eşlenmez → negotiate 404. Modül yayını **kendi tipli
  hub'ıyla** yapar (`SignalizationHub` + `ISignalizationNotifier`, `DiagramHub` / `IDiagramNotifier` kalıbı);
  çekirdeğe genel, string konulu bir yayın altyapısı eklemeyin (2026-09-17 kararı).
- `dotnet ef` tasarım zamanında `Development` ortamını kullanır; modül orada açık olduğu için
  `--context SignalizationDbContext` komutları çalışır. Modülü Development'ta kapatırsanız EF
  context'i bulamaz.
- **Modül açık ama controller'ları 404 / OpenAPI'de yoksa** WebAPI'nin
  `obj/.../Scadex.WebAPI.MvcApplicationParts*` dosyaları eskidir: projeye ASP.NET Core referansı sonradan eklenince
  artımlı derleme bu listeyi yenilemeyebilir (2026-09-29, RemoteDesk'te yaşandı). O iki dosyayı silip yeniden derleyin.
- **İkinci modül: `Scadex.RemoteDesk` (PC ekran izleme, `Modules:RemoteDesk:Enabled`).** Şartnamesi, fazları ve
  kararları depo kökündeki [RemoteDesk.md](RemoteDesk.md)'dedir. Çekirdekteki izleri: `DeviceType.Pc` (sistem şablonu
  "Bilgisayar", Id'si eski Peripheral kimliğinde kalır) ve MediaMTX auth kancasındaki `IMediaPathAuthorizer` —
  `cam_` yolları ona **hiç sorulmaz**, modül `pc_` yollarını üstlenir, kimsenin üstlenmediği yol 401'dir.
  `IMediaGateway.GetRuntimePathAsync` / `KickPublisherAsync` yalnızca yapılandırmada OLMAYAN, yayıncıyla yaşayan `pc_` yolları
  içindir (yol hazır mı, uymayan yayıncıyı at). `pc_` yolları `MediaPathCleanupWorker`'a görünmez ve görünmemeli: temizlenecek
  yapılandırma yoktur, yayıncıyı/oturumu modülün kiralama mekanizması durdurur.
  **PC istemcisinin bağlı olup olmadığı kabin durumuna bilerek yansımaz (2026-09-30):** yalnızca bellekte (`PcConnectionRegistry`);
  `ICabinetStatusService`'e kanıt olarak vermeyin — uygulama kapanınca kabin `Warning`'e düşerdi. İzlendiğine dair onay da
  bilerek yoktur (gösterge + `ScreenViewLog`). **Modülün HTTP uçları `RemotePcView` iznini ister:** policy adı
  `RemoteDeskModule.ViewPolicy` = izin kodu `Permission.RemotePcView` — enum'u yeniden adlandırmayın. Policy'ler 2026-10-14'ten beri
  modülde değil çekirdekte tek yerde kaydedilir (aşağıdaki "İzin zorlaması"). `PcHub` kullanıcı JWT'si istemez; PC kendi Basic Auth
  kimliğiyle bağlanır (`DeviceAuthPolicies.Pc`, aşağıdaki "Saha cihazı kimliği"). **`PcHub` negotiate'i kendi IP politikasındadır
  (2026-10-25, readiness P0.2):** IP başına dakikada 60 istek (`RemoteDeskModule.PcHubRateLimitPolicy`, politika modülün kendi kaydında,
  `MapHub(...).RequireRateLimiting`); bağlanıp 10 sn içinde `Hello`'su kabul edilmeyen bağlantı kapatılır. Modülün HTTP uçları çekirdeğin
  `Default` politikasındadır (aşağıdaki "Modül controller'ları"); `ViewerHub` bilerek dışarıdadır.
  **Denetim raporu (`/remote-desk/reports`, 2026-10-05) ayrı izindir: `RemotePcAudit`** (`RemoteDeskModule.AuditPolicy`) — izleyen
  operatör başkalarının geçmişini görmesin diye. Policy bu yüzden **controller başınadır**, `RemoteDeskControllerBase` yalnızca kimlik ister:
  tabana policy koymayın (`[Authorize]`'lar VE ile birleşir, rapor izleme iznini de isterdi). Raporda girdi sayısı bilerek yok.
  **Uzaktan kontrol (fare Faz 8, klavye Faz 9) ayrı izindir: `RemotePcControl`** — `/hubs/remote-desk/viewer` bu policy ile korunur
  (`RemoteDeskModule.ControlPolicy`). Kontrol PC başına tek kullanıcıdadır ve yalnızca o PC'yi **izleyene** verilir (canlı kiralama;
  izleme biterse kontrol süpürmede düşer) — bu bağı gevşetmeyin, görmeden tıklamak demektir. Girdi sunucuda saklanmaz/sıraya alınmaz,
  yalnızca `RemoteControlSession` denetim satırı yazılır (RemoteDesk.md § 12.7). Klavye `KeyboardEvent.code` ile taşınır ve PC'de scan
  code ile basılır — `key`'e (karaktere) çevirmeyin, Türkçe Q bozulur. **Sunucu ve sözleşme OS'tan bağımsızdır (2026-10-25; ileride
  Linux/Android istemcileri sunucu değişmeden bağlanabilmeli, RemoteDesk.md § 6.2):** sözleşmede yalnızca tel üzerindeki kod kümesi durur
  (`Contracts/Hub/KeyboardCodes.cs`, sunucu `IsKnown` ile süzer), Windows set-1 tablosu istemcidedir (`Services/Input/WindowsScanCodes.cs`) —
  Windows'a özgü tabloyu sözleşmeye geri taşımayın. Sunucu PC'nin dosya yolunu `Path.*` ile işlemez (sunucunun OS kuralını uygular), ad
  çıkarma/birleştirme `FileTransfer/PcPathText`'tedir. `MonitorInfo`'nun DXGI alanları, `EncoderKind`/`GpuVendor` ve `Contracts/Local`
  Windows istemcisine özgüdür; sunucu bunları yorumlamaz, yorumlayan kod eklemeyin.
  **PC tarafı servis + Agent + WPF'tir (Faz 11a, 2026-10-25, RemoteDesk.md § 9.1/§ 9.8):** `Scadex.RemoteDesk.Windows.Service` tek exe, iki
  mod — argümansız **Windows servisi** (`ScadexRemoteDesk`, LocalSystem, Session 0; yalnızca gözetmen, merkezle konuşmaz) ve `--session`
  **Agent** (servisin `CreateProcessAsUser` ile kullanıcı oturumunda başlattığı **SYSTEM** süreci; hub, yakalama, girdi, dosya, pano — eski
  WPF `Services/` klasörü buraya taşındı). `Scadex.RemoteDesk.Windows` artık **yalnızca arayüzdür** (normal yetki, `HKLM\…\Run` `--tray`):
  Agent'la named pipe (`Contracts/Local/AgentPipeContract.cs`) üzerinden durum alır ve YALNIZCA üç komut gönderir (yeniden bağlan / encoder sına /
  tercih et) — girdi, yayın, dosya komutunu pipe'a eklemeyin (yerel kullanıcı SYSTEM Agent'a iş yaptırırdı). Pipe ACL'i SYSTEM + oturumun logon
  SID'idir; arayüz sunucunun kurulumdaki servis exe'si olduğunu doğrular. **Agent yalnızca aktif, kullanıcılı oturumda çalışır** (önce konsol,
  yoksa en küçük RDP; kullanıcı kararı) — giriş ekranında PC "bağlı değil"dir. Oturum açılıp kapanınca / hedef oturum değişince servis Agent'ı
  yeniden başlatır, çökünce geri çekilerek (1/2/5/10/30 sn); servis Job Object'le Agent'ı ve FFmpeg'lerini birlikte öldürür. Agent SYSTEM
  olduğu için yükseltilmiş pencerelere girdi görev/UAC olmadan ulaşır; Görev Zamanlayıcı yolu ve `Install-StartupTask.ps1` **kaldırıldı** —
  geri eklemeyin. Elle (servis dışı) başlatılan Agent çıkar; geliştirmede `DOTNET_ENVIRONMENT=Development` ile normal kullanıcıyla çalışır.
  Agent'ta Hello'daki kullanıcı adı ve dosya transferinin koruduğu Startup klasörü **oturumdaki kullanıcınındır** (`Agent/SessionContext`),
  `Environment.UserName` / `SpecialFolder.Startup` değil. Pano kendi STA iş parçacığındadır (WPF dispatcher yok).
  **UAC onay penceresi ve kilit ekranı desteklenir (Faz 11b dar, 2026-10-25, kullanıcı kararı — readiness önerisinin bilerek tersi):** ikisi de
  güvenli masaüstündedir (`winsta0\Winlogon`). `InputDesktopWatcher` aktif masaüstünü yalnızca yayın/kontrol sürerken yoklar; FFmpeg
  **`DesktopProcess`** ile aktif masaüstüne bağlı başlatılır ve masaüstü değişince aynı hedefle yeniden başlar — Agent'ta FFmpeg'i
  `Process.Start` ile başlatmayın (masaüstü seçemez; ayrıca tüm başlatmalar tek kilitte, kalıtılan pipe uçları sızmasın). Girdi işçisi
  (`RemoteInputService`) havuzda değil **kendi iş parçacığında**dır ve `SetThreadDesktop` ile aktif masaüstüne bağlanır — havuza geri
  taşımayın. Giriş ekranı ve Ctrl+Alt+Del yok (kullanıcı kararları). **Girdi durumu operatöre gösterilir (readiness P1.5, 2026-10-25):**
  `RemoteInputService` her turun sonunda `ControlInputState`'i (kilit ekranı/UAC, erişilemeyen güvenli masaüstü, red, monitör yok) çıkarır ve
  YALNIZCA değişince `ReportControlInput` ile bildirir; merkez kontrol eden tarayıcıya iletir. Paket başına bildirim eklemeyin (60 Hz).
  **Kurulum paketi ve kurulum betiği bilerek yoktur (2026-10-25, kullanıcı kararı — betikler karmaşık bulundu, silindi):** iki
  `dotnet publish ... -c Release -r win-x64 --self-contained true` (`Service\` + `Tray\`, client'ta **yan yana** — arayüz Agent'ı `..\Service\`
  yolundan doğrular), `Service\appsettings.json`'a merkez adresi + `DeviceId` + `DeviceSecret`, yönetici komut isteminde
  `sc create ScadexRemoteDesk binPath= "…\Service\Scadex.RemoteDesk.Windows.Service.exe" start= auto` + `sc start` (adımlar RemoteDesk.md
  § 9.6). Servis exe'si elle çalıştırılmaz: Agent'ı yalnızca LocalSystem servis SYSTEM olarak başlatabilir. Kabul edilen eksik:
  `appsettings.json` varsayılan izinlerle kalır (cihaz sırrı standart kullanıcıca okunabilir). Kurulum betiği geri eklemeyin.
  `Service\tools\ffmpeg\` (LGPL exe + `LICENSE.txt`) git'te yoktur; eksikse publish bilerek hata verir. Günlükler: servis/Agent
  `%ProgramData%\Scadex\RemoteDesk\logs\{service,agent}-*.log`, arayüz `%LocalAppData%\…\ui-*.log`. **Monitör listesi Agent
  tarafında kendiliğinden güncellenir (2026-10-01):** `SessionSwitch` (RDP/konsol geçişi) + 12 sn uzlaştırma, merkeze yalnızca değişince
  (`CentralConnectionService.ReportMonitorsIfChangedAsync`) — bu akışı tek-seferlik elle bildirime indirgemeyin.
  **Dosya transferi (2026-10-05) dördüncü ayrı izindir: `RemotePcFileTransfer`** (`RemoteDeskModule.FileTransferPolicy`) — viewer hub
  metotları ve `FileTransferController` bunu ister. **Transfer KONTROLE değil İZLEMEYE bağlıdır (2026-10-05 kullanıcı kararı):** her işlem,
  kullanıcı o PC'yi **o an izliyorsa** kabul edilir (`FileTransferCoordinator` → `ScreenStreamCoordinator.IsViewingAsync`); kontrol (fare/klavye)
  ALMAK gerekmez — salt dosya indirmek için PC'yi tek kullanıcıya kilitlemek aşırıydı. **Viewer hub'ın sınıf politikası `RemotePcView`**
  (bağlanmak için), kontrol metotları (`RequestControl`/`SendInput`/`ReleaseControl`) ayrıca `RemotePcControl`, dosya metotları
  `RemotePcFileTransfer` ister — bu üç-katmanı gevşetmeyin; `GetControlForViewerAsync`/`ControlLease` kaldırıldı, geri eklemeyin. **Dosyanın
  baytları hub'dan GEÇMEZ** — biletli HTTP ile merkez `%TEMP%` üzerinden akar (`FileTransferStore`, 1 GB sınır akışta zorlanır); hub yalnızca
  komut ve küçük dizin listesini (client-result `ListDirectory`) taşır. **PC uçları (`push`/`pull`, `PcFileTransferController`) iki şart ister
  (2026-10-25, readiness P0.5):** PC'nin Basic Auth kimliği (`DeviceAuthPolicies.Pc`) VE transferin bileti — bilet PC'ye komut gönderilirken
  üretilir (`IssueTicket`), 2 dk geçerli, yalnızca o transferin cihazınca ve **bir kez** kullanılır (`TryConsumeTicket`); PC aktarımı yeniden
  denemediği için tek kullanım güvenlidir. Tarayıcı uçları (`FileTransferController`, kullanıcı JWT'si) ayrı controller'dadır — `[Authorize]`'lar
  VE ile birleştiği için ikisi aynı sınıfta duramaz. **İzleme biterse PC ayağı kesilir (readiness P1.3, 2026-10-25):**
  `FileTransferCoordinator.SweepAsync` (`ScreenStreamWorker` turu) izlemesi düşen kullanıcının açık `push`/`pull`'unu `FileTransferEntry.Abort`
  ile keser; `pull` bu yüzden `File(stream)` değil elle akıtılır — geri çevirmeyin. 1 GB sınırının IIS karşılığı `Scadex.WebAPI/web.config`
  `requestLimits`'tir (IIS varsayılanı 30 MB, `[DisableRequestSizeLimit]` onu aşmaz); ikisi birlikte değişir. Geçici klasörde kaydı olmayan
  ve 10 dk'dan eski dosyalar `FileTransferStore.Sweep`'te silinir (readiness P1.2) — "açılışta klasörü boşalt"a çevirmeyin: IIS havuz geri
  dönüşümünde eski süreç birkaç saniye daha aktarım sürdürür. **Yükleme (PC'ye yazma) sistem
  klasörlerine (Windows / Program Files / Startup / kurulum klasörü / `%ProgramData%\Scadex`) PC tarafında reddedilir**, gezme/okuma/indirme
  serbest (kullanıcı kararı) — yalnızca ağ ve aygıt yolları (`\\…`, `\\?\`, `\\.\`) her yönde reddedilir (Agent SYSTEM: makine hesabıyla
  paylaşım açar, ham disk okurdu). Yazmada yalın dosya adı, ADS/ayrılmış ad yok, yolda junction/sembolik bağ yok, açılan dosyanın gerçek yolu
  yeniden denetlenir ve yazma atomiktir (`.scadex-part` → bayt sayısı → yerine taşı; yarım aktarım mevcut dosyayı bozmaz) —
  `FilePathPolicy`. Agent SYSTEM çalıştığı için bu denetimler şarttır, gevşetmeyin (RemoteDesk.md § 12.8). Frontend'de dosya transferi kontrol oturumundan ayrı kendi
  viewer hub bağlantısını taşır (`lib/file-transfer-session.ts` + `hooks/use-file-transfer.ts`). Arayüz iki panelli bir modaldir
  (`file-transfer-dialog.tsx`: sol yerel / sağ uzak, ortada Yükle/İndir), izlerken "Dosya aktarımı" düğmesiyle açılır. **Sol (yerel) panelin
  iki modu vardır (2026-10-26, kullanıcı kararı: kapalı ağda HTTPS zorunlu değil — 2026-10-06'daki "yedek kod yok" kararının yerine):**
  güvenli bağlamda (HTTPS/localhost ya da tarayıcı ayarıyla güvenli sayılan adres) + Chromium'da File System Access API ile klasör gezgini;
  değilse (HTTP, Firefox) klasik mod — `<input type="file">`/sürükle-bırak ve tarayıcı indirmesi (`lib/local-fs.ts > fsaAvailable`, `saveBlob`).
  İki modu da koruyun; birini kaldırmak ya HTTP'yi ya klasör gezginini bozar. Her indir/yükle `FileTransferLog` denetimi olur; dizin listeleme
  kaydedilmez.
  **Panolar arası metin aktarımı (2026-10-06, RemoteDesk.md § 12.9):** üst-orta araç çubuğunda Dışa/İçe aktar; aynı yetki/kapı (`RemotePcFileTransfer`
  + izleme, `ClipboardCoordinator`). Metin hub'dan geçer (küçük; dosya baytlarından farklı), 1.000.000 karakter sınırı — bu yüzden **ViewerHub +
  PcHub `MaximumReceiveMessageSize` 4 MB** (diğer hub'lar varsayılan 32 KB). İçerik **loglanmaz/denetlenmez** (parola olabilir). Güvenli
  bağlamda tek-tık `navigator.clipboard`; yoksa (HTTP) iki adımlı pencere (`components/clipboard-dialog.tsx`: Ctrl+V ile gönder / seçili
  metni `execCommand('copy')` ya da Ctrl+C ile al — 2026-10-26). PC panosu Agent'ın kendi STA iş parçacığında (`Services/Clipboard/ClipboardAccess`).

- **Çekirdek modülü bilmez.** Tek temas noktası `IScadaEventObserver` (Business/Utils/ScadaEvents):
  ingest (değer gerçekten değişince), **başarılı çıkış komutu** (kanal değeri değişince, `Direction =
  Output` + mantıksal `TurnOn`; 2026-09-21), **her çıkış komutunun sonucu** (`OnOutputCommandCompletedAsync`, başarılı/başarısız,
  gönderenle — motorun komutunda `RequestedByUserId == null`; 2026-10-13, elle komutun işleme yazılması için) ve `POST /api/Scada/card`
  gözlemcileri yazımdan SONRA çağırır. Gözlemci **sıcak yoldadır — yalnızca kuyruğa bırakır**; içinde SCADA'ya komut göndermek,
  kart isteğini bekleyen SCADA kartıyla kilitlenme demektir (çıkış bildirimi, modül motorunun
  beklediği `SendAsync`'in içinden de gelir).
- **Modül durum TUTMAZ (2026-09-21).** Kapı/siren/aydınlatma/kilit durumu yalnızca çekirdekteki kanalın
  son değerinden `ISignalChannelStateService` ile okunur; `Signal*State` tabloları kaldırıldı, geri
  eklemeyin. Çıkışta `IoChannel.CurrentValue` son BAŞARILI komutun **fiziksel** değeridir (NC'de
  ters); mantıksal okuma çekirdekte `IIoChannelService.GetOutputStatesAsync` / `IOutputPolarityResolver`
  ile yapılır, NO/NC mantığını modülde kopyalamayın. `null` = bilinmiyor.
- **Modül çekirdek tablolarına doğrudan YAZMAZ.** Komut `IDeviceCommandService.SendAsync`, kare
  `ICameraService.CreateCaptureAsync`, rol ataması `IUserRoleService.SyncAsync` üzerinden gider;
  çekirdeği `IUnitOfWork` ile yalnızca **projeksiyonla okur**.
- **Kendi context'i, kendi şeması:** `SignalizationDbContext`, `signalization` şeması,
  `signalization.__EFMigrationsHistory`. Çekirdeğe referanslar (CabinetId, IoChannelId, UserId, CameraId…)
  **FK değildir**; bütünlük yapılandırma kaydında servis doğrulamasıyla sağlanır.
  `RepositoryBase` Identity context'ine bağlı olduğu için modülde kullanılmaz.
- **"Bu kabin modülün mü" kodda sorulmaz:** `signalization.Cabinet` satırı yoksa olay yok sayılır.
- **Kapı sanaldır; ilişki `IoChannel`'a kurulur, `Device`'a değil** (Device kartın tamamıdır).
- Modül controller'ları `BaseController`'ı miras alamaz (WebAPI'de); `SignalizationControllerBase`
  aynı ProblemDetails sözleşmesini Core'un `GetProblemDetail`'i ile kurar. **Modül HTTP uçları çekirdekle aynı rate limit'tedir
  (2026-10-26, kullanıcı kararı — 2026-09-11'deki "modüle rate limit yok" kararının yerine):** taban sınıf
  (`SignalizationControllerBase`, `RemoteDeskControllerBase`) `[EnableRateLimiting(RateLimiterKey.Default)]` taşır; politika adları bu yüzden
  Business'tadır (`Scadex.Business/Utils/RateLimiting/RateLimiterKey.cs` — modül WebAPI'ye referans veremez), politikaların kaydı yine
  `Program.cs`'tedir. Yeni modül tabanı da aynı özniteliği taşır. **Hub'lar bilerek dışarıdadır** (`DiagramHub`, `SignalizationHub`,
  `ViewerHub` `[DisableRateLimiting]`): WebSocket yoksa SignalR uzun yoklamaya düşer, her mesaj bir HTTP isteği olur ve 60 Hz uzaktan
  girdi sınıra takılırdı. İstisna `PcHub` negotiate'idir (her ağdan bağlantı alan saha cihazı ucu; IP politikası modülün kaydında).

**Büyük servisler `#region` ile değil, partial sınıflara bölünür** — gruplama birimi dosyadır:
`CameraService.cs` + `.Streaming` + `.Snapshot` + `.Capture` + `.Monitoring`,
`DeviceCommandService.cs` + `.Comunication`,
`DiagramService.cs` (okuma + ortak yardımcılar) + `.Devices` + `.Connections` + `.Annotations` + `.Layout` + `.Delete`.

Diyagram servisinde her dosya bir uç ailesidir ve dosyalar arasında paylaşılan bağlam yoktur. **Toplu kaydetme deltası
2026-10-27'de kaldırıldı (kullanıcı kararı):** karmaşıklığı (istemci pin Id'leri, aynı gönderide kaydedilmemiş cihaza kablo,
`SaveContext`, iki fazlı transaction) referansların kaydedilmemiş kayıtlara gitmesi üretiyordu. Geri getirmeyin; editöre
"Kaydet / vazgeç" eklemek isteniyorsa önce bu bedeli tartın.

## Kırılmaması gereken kurallar

Bunlar tek bir dosyaya bakarak görülemez; gerekçeleri PROJECT_OVERVIEW.md §5-§6'dadır.

- **Her tablonun tek bir yazım yolu vardır.** `Device`, `Connection`, `DiagramAnnotation`,
  `Pin`, `IoChannel` yalnızca `DiagramController`'ın yazma uçlarından (`/api/Diagram/cabinet/{id}/` altında `devices`,
  `connections`, `annotations`, `layout`, `delete`) yazılır; `ComponentTemplatePin` yalnızca `POST /api/ComponentTemplate`; `CanvasSettings` yalnızca
  `PUT /api/CanvasSettings/cabinet/{cabinetId}` üzerinden. Bu tabloların generic
  controller'ları **2026-10-14'te kaldırıldı** (okuma diyagram ucundan gelir) — geri eklemeyin.
  İhtiyaç varsa sahibinin controller'ına ekran bazlı bir uç ekleyin. **Cihazın yaşam döngüsü (oluşturma + kaldırma/pasife
  alma) YALNIZCA diyagram uçlarıdır (2026-10-25, kullanıcı kararı)** — diyagramda olmayan tanımlı cihaz yoktur. Tipe özel
  ekranlar yalnızca **mevcut, aktif** cihazın bilgisini günceller ve kendi özel okumalarını yapar (kamera: `PUT /api/Camera`,
  `ConfigureSystem` + izleme/kare/klip). PC / POS / alarm gibi tiplere diyagram dışı ekran istenirse aynı kalıp izlenir
  (sahibinin controller'ında ekran bazlı güncelleme ucu, diyagramın cihaz formuyla aynı doğrulamalar); **oluşturma ya da
  `IsActive` yazan uç açılmaz**. Gerekçe: ekrandan pasife alınıp diyagramdan silinmiş bir cihazı geri getirmek onu pinsiz
  diriltiyordu (diyagram silmesi pinleri ve kabloları soft-delete eder, ekran yalnızca `IsActive` yazardı).
- **Generic CRUD yüzeyi yoktur (2026-10-14).** İstemcinin çağırmadığı uçlar (`{id}/base`, `list/base`, `selectlist`,
  `pagination`, `datatable/*`, `DeviceCommand`/`Pin`/`DeviceStatus`… controller'ları) ve yalnızca onlara hizmet eden servis
  metotları/DTO'lar silindi. "İleride lazım olur" diye uç eklemeyin; bir ekran istediğinde, o ekranın uçu eklenir.
  Core'daki dinamik filtre/sayfalama/datatable motoru ve repository'ler genel altyapı olarak durur.
- **İzin zorlaması (2026-10-14).** Policy'ler `AddPermissionPolicies()` (`Scadex.Business/Utils/Auth/PermissionAuthorization.cs`,
  `Program.cs`) ile `Permission` enum'unun HER değeri için **tek yerde** kaydedilir; policy adı = izin kodu = enum adı. Ucu
  `[RequirePermission(Permission.X)]` ile işaretleyin — modül kaydına `AddPolicy` eklemeyin. Kural: **sahaya etki eden ve veri
  yazan her uç bir izin ister** (`EditDiagram` diyagram/tuval yazımı, `ConfigureSystem` kabin/firma/kamera/şablon/ayar/sinyalizasyon
  yapılandırması, `ManageUsers` kullanıcı/rol/izin + parola, `ManageAccessCards` kart ataması + operatör kurumu),
  hassas okumalar da (`ViewCamera` kamera listesi/detayı/bilet/kare; `ViewDiagram` diyagram okuma + komut geçmişi;
  `ManageUsers` kullanıcı listesi ve rolleri — kart tanımlama kendi dar listesini `GET User/card-holders` ile `ManageAccessCards`'la okur);
  diğer okuma uçları yalnızca giriş ister. **Komut izinleri ayrıdır (2026-10-14):** çekirdek diyagram komutu `ViewDiagram` + `ControlOutput`
  (ikisi birden), sinyalizasyonun sanal kabini (okuma + komut + kart okumaları) TEK izin `SignalVirtualCabinet` — `ControlOutput`
  sanal kabinde, `SignalVirtualCabinet` diyagramda geçmez; birini diğerine bağlamayın. **Anonim kayıt yoktur** (SignUp kaldırıldı):
  kullanıcıyı `ManageUsers` açar; e-posta isteğe bağlıdır (`RequireUniqueEmail = false`, benzersizliği `UserService` denetler). `[Authorize]`'lar VE ile birleşir — izni sınıfa değil aksiyona koyun (RemoteDesk'teki gibi, sınıfa
  konan izin okumaları da kilitler). Denetim servis içinde değil **controller'da**: motor (Sinyalizasyon) komutları HTTP kullanıcısı
  olmadan `IDeviceCommandService.SendAsync`'ten gönderir. Ön yüz aynası: `PermissionCode` (`models/enums/entityEnums.ts`),
  rotada `requirePermission(...)` loader'ı (`lib/route-guards.ts`), menüde `permission`, düğmede `usePermission()` — bunlar yalnızca
  UX'tir. Diyagram editörü `EditDiagram` yoksa **salt okunurdur** (palet/form Kaydet'i/tuval ayarı yok, düğümler `draggable/connectable:
  false` — düğüm düzeyindeki `draggable` tuvalin `nodesDraggable`'ını ezdiği için orada da kapatılır).
  **Yetki yönetimi (2026-10-16):** rol→izin (`RolePermission/role/{id}/sync`) ve
  kullanıcı↔rol (`UserRole/user/{id}/sync`, Roller → Üyeler `UserRole/role/{roleId}/users/{userId}`) yazımları `ManageUsers` ister.
  **Yetki Matrisi ekranı ve toplu yazma ucu (`PUT RolePermission/matrix`) 2026-10-28'de kaldırıldı** (kullanılmıyordu) — rol izinleri
  yalnızca Roller → İzinler'den yazılır. `GET RolePermission/matrix` kullanıcı Roller penceresindeki "etkin izinler" önizlemesi için durur.
  **Bilinçli olarak yoktur** (kullanıcı kararı): son yöneticiyi kilitleme koruması (son `ManageUsers` sahibi yetkisiz kalırsa kurtarma
  veritabanından yapılır), yetki yükseltme kısıtı (ManageUsers sahibi kendinde olmayan izni verebilir), yetki denetim tablosu ve
  anında etki — izinler token'a girişte yazılır, değişiklik **bir sonraki girişte** geçerli olur; "zorla oturum kapat" düğmesi web
  istemcisi refresh kullanmadığı için etkisiz olacağından eklenmedi.
- **Diyagram editörü her eylemi anında kendi ucuna gönderir (2026-10-27).** Oluşturma (`POST`, Id'yi istemci üretir,
  varsa 400) ile güncelleme (`PUT …/{id}`, bulunamazsa 404) ayrıdır. **Pin ve kanalları sunucu üretir** ve cihaz oluşturma
  yanıtında döndürür; istemci pin kimliği göndermez, cihaz tuvale yanıttan sonra eklenir, kablo yalnızca kalıcı pinlere
  çizilir. Konum/boyut/dönüş/z/kilit/görünürlük `PUT …/layout`'ta (tek toplu uç, referans taşımaz), silme `POST …/delete`'te
  (cihazın pinleri ve kabloları sunucuda birlikte kalkar). Her işlem tek `SaveChangesAsync`'tir; transaction yoktur.
  Ön yüzde istekler **tek kuyrukta sırayla** gider, tuval iyimser güncellenir, hata → toast + graf yeniden çekilir
  (`hooks/use-diagram-editor.ts`). Kaydet/Vazgeç'i olan tek yer cihaz formudur (`device-form.tsx`: IP + izleme + ek nesne birbirine
  bağlı); kablo ve not alanları blur'da anında yazılır. Kamera zorunlu alanlarla (IP + marka) pencereden oluşturulur
  (`create-device-dialog.tsx`).
- **Kanal adresleri kabin genelinde tekildir**, cihaz genelinde değil. Bir cihazı silmek
  kanallarını serbest bırakmaz (`IN1` işgal edilmeye devam eder).
- **Tipe özgü alanlar `Device`'ta DEĞİL, 1-1 ek tablolardadır (2026-10-22):** `Camera` (marka, model, açıklama, kullanıcı,
  parola), `ControlCard` (`MacAddress`), `PcDevice` (`CredentialHash`). Birincil anahtar ortaktır (`DeviceId` hem PK hem FK),
  tip yine `ComponentTemplate.DeviceTypeId`'dir. **Kalıtım bilerek yoktur** — `Camera : Device` / `HasBaseType` yazmayın:
  `HasBaseType` tek başına TPH (tek tablo) kurar, TPT ise her `Devices` sorgusuna tüm alt tabloları JOIN'ler ve parolaları her
  cihaz okumasında belleğe getirir. Ek satır yalnızca `Include` edilince okunur. Yazım yolu diyagramın cihaz uçlarıdır:
  `DeviceCreateRequest` / `DeviceUpdateRequest` `.Camera` / `.ControlCard` / `.Pc` — şablon tipiyle eşleşen nesne **zorunludur**,
  yanlış tipe giden 400'dür (`DiagramService.Devices.cs > ValidateDeviceAsync`). Okuma DTO'ları parola döndürmez, `hasPassword` döner;
  istekte `password: null` = dokunma, `""` = temizle. **Yeni tip (Alarm, NAS…) = `DeviceType` değeri + sistem şablonu + gerekiyorsa ek
  tablo + istekte nesne (`DeviceExtensionDrafts.cs`) + panel bölümü (zorunlu alanı varsa oluşturma penceresi);** alanı `Device`'a eklemeyin, ayrı kök tablo da açmayın, diyagram dışında oluşturma ucu açmayın.
- **Saha cihazı kimliği: kontrol kartı MAC header'ı, PC Basic Auth (2026-10-28, kullanıcı kararı — 2026-10-25'teki "MAC geri
  eklenmesin" kararının kontrol kartı için yerine; sözleşme [Docs/scada-http-api.md](Docs/scada-http-api.md)).**
  **Kontrol kartı** (`/api/Scada/*`) her istekte `DeviceId: AA:BB:CC:DD:EE:FF` header'ı gönderir = kartın MAC adresi; Merkez onu
  `ControlCard.MacAddress` ile eşleştirir (`ControlCardMacAuthenticationHandler`, şema `ControlCardMac`). İki taraf da tek tipe çevrilir
  (`Scadex.Core/Utils/MacAddressFormat.cs`, ön yüz aynası `lib/diagram/device-credential.ts > normalizeMacAddress`); MAC cihaz formunda
  girilir (`ControlCardDraft.MacAddress`, her kayıtta eksiksiz gider, `null` = kart bağlanamaz). **MAC tekilliği bilerek denetlenmez**
  (index yok, doğrulama yok, silmede null'a çekme yok — kod sade kalsın, kullanıcı kararı): aynı MAC iki aktif kartta varsa ilk eşleşen
  kazanır. MAC gizli değildir — bilen kart adına yazabilir; kabul edilen risk, sır eklemeyin.
  **RemoteDesk PC'si** (`PcHub`) `Authorization: Basic` gönderir: **kullanıcı adı = cihazın Id'si**, parola = **sistemin ürettiği sır**
  (32 küçük harf onaltılık; `Scadex.Core/Utils/DeviceCredential.cs`). Sırrı tarayıcı üretir (`lib/diagram/device-credential.ts`, özellik
  paneli "Üret/Yenile/İptal et") ve cihaz formunun Kaydet'iyle gönderir (`PcDeviceDraft.Secret`: `null` dokunma, `""` iptal, değer yeni sır);
  sunucu biçimi zorlar ve yalnızca **SHA-256'sını** saklar (`PcDevice.CredentialHash`, sabit zamanlı karşılaştırma) — sır geri okunamaz,
  okuma DTO'su `hasCredential` döner. Sır yüksek entropili olduğu için PBKDF2 yoktur. Kullanıcı adı PK olduğu için benzersizlik denetimi yoktur.
  Ortak: `IDeviceCredentialService` (handler'lar çağırır, HTTP yüzeyi yok), policy'ler `DeviceAuthPolicies.ControlCard` / `.Pc`
  (`Business/Utils/Auth/DeviceAuthentication.cs`, `Program.cs`'te `.AddDeviceAuthentication()`) — modül kendi şemasını kaydetmez. Policy
  `device_type` claim'i ister: `BaseController`'ın `[Authorize]`'ıyla birleşse de kullanıcı JWT'si cihaz ucuna girmez. Cihaz kimliği
  **yalnızca gelen** isteklerdedir; Scadex'in karta gönderdiği komut (`ScadaBaseUrl`) kimliksizdir. Reddin nedeni istemciye söylenmez (401),
  günlüğe MAC / cihaz Id'si + IP ile yazılır; PC sırrı asla loglanmaz. **HTTPS zorunlu değildir** (kullanıcı kararı): düz HTTP'de kimlik ağdan
  okunabilir — kabul edilen risk, HTTPS denetimi/yönlendirmesi eklemeyin. Açık PcHub bağlantısı sır yenilense de kopana kadar sürer.
- **Kamera bir `Device`'tır (`DeviceType.Camera = 14`, sistem şablonu "IP Kamera", 2026-10-22).** Diyagramdan eklenir ve
  diyagramdan kaldırılır (şablonla sürükleme + özellik paneli, `EditDiagram`). Kamera ekranı (`/cameras/cabinet/:id`,
  `PUT /api/Camera`, `ConfigureSystem`) yalnızca **aktif** kameranın bilgisini günceller — oluşturamaz, pasife alamaz
  (`CameraUpdateDto`'da `IsActive` yoktur), pasif kamerayı düzenleyemez (404); iki yol aynı kuralları uygular, son yazan
  kazanır (2026-10-25). Pasif (diyagramdan kaldırılmış) kamera ekranda çekim geçmişi için listelenir. 2026-10-25'ten önce
  kamera ekranından eklenmiş kameralar diyagramda gizli durabilir (`IsVisible = false`): "Seçilemeyen öğeler → Göster" ile
  tuvale alınır. Kamera IP'si zorunludur ve
  **kabin içinde** tekildir (iki yolda da doğrulanır, DB index'i yok; ad tekilliği yalnızca kamera ekranının kuralı); boş izleme
  portu markanın RTSP portuyla dolar. Kamera Id'si cihaz Id'sidir:
  `CameraCapture.CameraId`, `signalization.OuterDoor.CameraId` ve `cam_{id}` yolları onu görür. Kamera bilgisi değişince MediaMTX
  yolunu düşürmek **commit sonrası** yapılır: diyagramda `DiagramController` (`UpdateDevice` + `Delete`), ekranda
  `CameraService.UpdateAsync` (`ICameraService.RefreshStreamPathsAsync`; yapılandırması güncel kaynakla uyuşmayan ya da pasif
  kameranın yolu silinir) — `DiagramService`'in içine koymayın. Kamera okuma DTO'su parolayı döndürmez (`hasPassword`). RTSP adresi kuran kod entity değil
  `CameraEndpoint` alır.
- **Lifecycle interceptor'ları sessizce geçmez, istisna atar.** `IImmutableEntity` güncelleme
  ve silmede, `IActivatableEntity` silmede patlar (`IsActive = false` kullanın);
  `ISoftDeletableEntity` fiziksel silmeyi `IsDeleted = true`'ya çevirir. `IsActive` üzerinde
  global query filter **yoktur**, `IsDeleted` üzerinde **vardır**.
- **Yanıt zarfı yoktur.** `Result<T>.Success(data)` → `200` + çıplak DTO. Hatalar RFC 7807
  `ProblemDetails`; gövdedeki `code` uzantısı HTTP kodu **değildir**.
- **Enum'lar sayı olarak serileştirilir** (`JsonStringEnumConverter` bilerek kayıtlı değil) ve
  numaraları boşlukludur. **`DictionaryKeyPolicy` bilerek `null`** — `ProblemDetails.errors`
  anahtarları PascalCase kalır (`"Camera.Brand"`, `"Devices[0].Id"`); iki taraftan birini "düzeltmeyin".
  `null` alanlar gövdeden düşmez (`DefaultIgnoreCondition = Never`).
- **Medya geçidi ayarları `appsettings.json > MediaGateway`'dedir, kamera çekim ayarları
  veritabanındadır (2026-09-27).** `MediaGatewaySettings` `IOptions` ile okunur (açılış doğrulaması
  yoktur), değişiklik yeniden başlatma ister; tablosu, servisi, ucu ve ekranı **bilerek yoktur** — port/adres
  alanları `mediamtx.yml` ile elle eşleştiği için ekrandan değiştirilmemeli. Geri DB'ye taşımayın.
  Kamera çekimi ise tek satırlık tablo + `ICameraCaptureSettingService`, `ICacheService` ile
  önbelleklenir, her yazma kendi anahtarını düşürür; `appsettings.json`'a `Cameras` bölümü eklemeyin
  — okunmuyor. Adlandırılmış `HttpClient` `Program.cs`'te kurulur ama `BaseAddress`/`Timeout` orada
  **verilmez**; `MediaMtxGateway.CreateConfiguredClient` ayardan uygular.
  Ekranı `/admin/settings`; zod şeması (`models/cameraCaptureSetting`) sunucudaki `FluentValidation`
  kuralının **elle tutulan kopyasıdır** — sunucudaki kuralı değiştirirseniz şemayı da değiştirin.
- **Yoklama (ayakta mı) akışının HTTP yüzeyi yoktur ve tipe özel değildir.**
  `MonitoredAssetProbeWorker` kayıtlı her `IMonitoredAssetProbeSource` üzerinden döner; yeni
  bir izlenen tip eklemek = yeni kaynak + tek satır DI kaydı, worker'a dokunulmaz. Sonda
  `MonitoringPort` doluysa **TCP connect**, boşsa **ICMP ping** yapar (2026-10-12; eskiden ICMP dalı yoktu ve
  portsuz varlık atlanırdı). Ping yalnızca "makine ağda" der — hizmetin ayakta olduğunu TCP kanıtlar; ICMP'si
  kapalı hedef sağlamken Çevrimdışı görünür, ona port girilir. Kamerada boş port kayıtta markanın RTSP portuyla
  dolar (ping'e düşmez). Buraya "şimdi dene" tarzı bir uç eklemeyin. Tek kaynak `DeviceProbeSource`'tur
  (2026-09-24 — `Device : IMonitoredAsset`; yalnızca izlemesi açık **ve** şablonu `ComponentTemplate.IsMonitorable` olan cihaz).
  `CameraProbeSource` 2026-10-22'de kalktı: kamera bir cihazdır ve aynı kaynakla yoklanır. Worker son sonda anı olarak sondanın
  bitişini yazar (bilinçli); `PingIntervalSec` tur aralığının tam katıysa hedef bir tur geç
  yoklanır (60 sn → fiilen 90 sn).
- **Kabin durumu ve cihaz CANLILIK durumu yalnızca `ICabinetStatusService` üzerinden yazılır
  (2026-09-24).** `Cabinet.DeviceStatusId`, `Device.DeviceStatusId` / `LastSeen` /
  `LastConnectionError`'a başka yerden dokunmayın; kanıtı (yoklama, SCADA teması, komut
  `NoResponse`) servise verin. Kabin durumu = cihaz katkılarının en kötüsü ve **tabanı kontrol
  modülüdür**: kabini Online'a da Offline'a da yalnızca kontrol modülü çeker; diğer cihazların (kameralar
  dahil — 2026-10-22'den beri ayrı kamera kuralı yok) `Offline`'ı kabine `Warning` olarak yansır, `Online`'ları hiç katılmaz
  (`CabinetContribution`). Durum tabloları: PROJECT_OVERVIEW.md § 5.3 "Canlılık". SCADA canlılığı **kontrol modülünde** tutulur
  (kanıt kart başınadır): her ingest (tanımsız kanal dahil), kart okuma ve başarılı komut temas
  sayılır; giriş modülüne canlılık yazmayın. Başarısız sonda **ilk başarısızlıkta** Offline yapar;
  `NoResponse` anında. **`LastSeen`'e bakıp Offline yargısına varılmaz** — yalnızca
  izlemesi kapalı cihazların `LastSeen`'i dolu ve 23 saatten eski Online/Offline'ı taramada `null`'a
  ("Bilinmiyor") çekilir; boş `LastSeen` eski sayılmaz (hiç görülmemiş kartın `NoResponse` Offline'ı silinmesin)
  (izlenenleri katmayın: yoklamayla her turda Bilinmiyor ↔ Offline salınırlar),
  23 saat bilerek sabittir. Canlılık yalnızca Online/Offline/`null`'a karar verir,
  Warning/Maintenance korunur. **`Critical` yoktur (2026-10-12'de kaldırıldı, numara 3 boş — yeniden kullanmayın);
  en kötü durum kırmızı Çevrimdışı'dır.** Sinyalizasyon modülü kabin durumuna **bilerek**
  yansımaz (alarm haritada ayrı ikon; `AppModule.alertCabinetsQuery`). **Kabinde `Maintenance` yapışkan bir
  operatör kararıdır (2026-09-28):** ayrı kolon yok, `Cabinet.DeviceStatusId` = Maintenance; kabin düzenleme ekranından
  `ICabinetStatusService.SetMaintenanceAsync` ile yazılır. Hesaplama ve tarama bakımdaki kabine dokunmaz, hesaplama da
  hiçbir zaman Maintenance üretmez (cihazın Maintenance'ı kabine taşınmaz) — yoksa kabin kendiliğinden bakıma düşüp kalırdı.
  **"Kabin neden bu renkte" dökümü (`GetBreakdownAsync`, 2026-10-12) katkıyı aynı `CabinetContribution`
  koduyla üretir;** ön yüz (harita panelindeki "Durum nedeni") kuralı kopyalamaz, `contribution`'ı okur.
  Kuralı değiştirirseniz tek yer orasıdır. `CabinetStatusContributorKind` 2026-10-25'te tümden kalktı (tek değeri kalmıştı); döküm kaydı her zaman bir cihazdır.
- **MediaMTX yol temizliği üç şeyi asla silmez:** bizim üretmediğimiz adlar (yalnızca `cam_`
  önekliler adaydır — `mediamtx.yml`'deki **`all_others`** silinseydi geçit
  yapılandırmasız kalırdı), `record: true` olan yollar ve `readers > 0` olan yollar. Bu kuralları
  gevşetmeyin; `MediaPathCleanupWorker.ShouldDelete`. (`clip_` yolu 2026-09-25'te kalktı: klip
  artık MediaMTX'te değil FFmpeg ile kaydediliyor.)
- **Kameraya yalnızca MediaMTX bağlanır, yalnızca RTSP ile; marka API'si (Hikvision ISAPI vb.)
  kullanılmaz (2026-09-25).** Markaya özgü tek bilgi `ICameraProtocolProfile.BuildRtspUrl`'dir ve
  yalnızca MediaMTX yolunun kaynağıdır. Çekimde FFmpeg kameraya **doğrudan bağlanmaz**:
  `CameraService.PrepareCaptureSourceAsync` yolu kurar ve tarayıcıyla aynı bilet mekanizmasından
  (`IssueStreamTokenAsync`) bir bilet alır; FFmpeg `IMediaGateway.LiveRtspUrl` adresinden okur
  (izleme açıkken kameraya ikinci oturum açılmasın diye — kullanıcı kararı; geri çevirmeyin).
  FFmpeg hata satırlarında kaynak adresini (içinde bilet) tekrarlar: `CameraCapture.FailureReason`'a
  yalnızca `CameraCaptureGateway.DescribeFailure`'ın sabit mesajları yazılır, ham stderr yalnızca
  `rtsp://***@` maskesiyle loglanır — bu ayrımı bozmayın. Klip bilinçli olarak iki adımdır
  (ham kayıt + tam süreye kırpma); tek `-t` klibi anahtar kare beklemesi kadar kısa keser.
- **Rate limit politika adı `Program.cs`'te tanımlı değilse o uç HER istekte 500 döner.**
  Bir `[EnableRateLimiting]` adını silmeden/değiştirmeden önce `RateLimiterKey`'e bakın (`Scadex.Business/Utils/RateLimiting`).
  Politikalar: `Default`, `Scada`, `MediaGateway` (+ modülün kendi kaydında `RemoteDesk.PcHub`). Modül controller'ları tabanlarından
  `Default`'u alır (2026-10-26). **`UseRateLimiter` `UseAuthentication` ile `UseAuthorization` ARASINDADIR (2026-10-25):**
  yetkilendirmeden sonra olsaydı kimliksiz istek seli 401 alıp sınıra hiç takılmazdı ve cihazların Basic Auth doğrulaması (yetkilendirmede
  çalışır, veritabanına sorar) her istekte çalışırdı. Kullanıcı bazlı bölümleme bozulmaz — `HttpContext.User`'ı JWT ile `UseAuthentication` kurar.
  Sırayı değiştirmeyin.
- **Hiçbir servis metodu `companyId` almaz, hiçbir yerde `IgnoreQueryFilters` çağrılmaz** —
  çok kiracılılık sonradan imza değiştirmeden tek bir global query filter olarak gelsin diye.
- **409 / optimistic concurrency / `rowVersion` yoktur** — son yazan kazanır.
- JWT'yi query string'den okuyan `OnMessageReceived` **yalnızca `/hubs` yolları içindir**;
  normal uçlara genişletmeyin.

## Frontend yerleşimi

`@/` alias'ı `src/`'e bakar (`vite.config.ts`).

| Klasör | İçerik |
|---|---|
| `src/api/` | Aggregate başına axios çağrıları + `query-keys.ts` |
| `src/models/<agg>/{commands,queries}/` | C# DTO'larının **elle yazılmış** TS aynası |
| `src/hooks/` | TanStack Query sarmalayıcıları (`use-diagram-editor`, `use-diagram-live`, …) |
| `src/lib/diagram/` | Tuval matematiği: waypoint, hizalama, pin tarafı, RF node/edge dönüşümü |
| `src/lib/camera/` | WHEP oynatıcı, stream bütçesi, oturum ve kare yakalama |
| `src/lib/signalr/` | `diagram-hub.ts` — `/hubs/diagram` istemcisi |
| `src/views/{auth,app,admin}/` | Ekranlar; `src/layouts/` bunları sarar |
| `src/modules/` | Müşteri modülleri. `index.ts > REGISTRY` + `VITE_MODULES` tek birleştirme noktası; her modülün manifestosu (`AppModule`) rota/menü/layout eklentisi verir, ekranları `lazy`. Modül kendi `api/ models/ hooks/ views/` klasörlerini taşır, sorgu anahtarları `['signalization', …]` altında |

Yığın: React 19 + Vite 8 + TS 6, React Flow (`@xyflow/react`), TanStack Query, Redux Toolkit,
shadcn + Tailwind 4, react-hook-form + zod, MapLibre, Recharts.

**Bir C# DTO'sunu değiştirdiğinizde TS aynasını elle güncelleyin** — codegen de test de yok,
kimse bunu sizin için yakalamaz.

**Arayüz HTTP'de de çalışmalıdır (2026-10-26, kapalı ağ kurulumu — README § 1.4).** Yalnızca güvenli bağlamda (HTTPS/localhost) var olan
tarayıcı API'lerini (`crypto.randomUUID`, `navigator.clipboard`, File System Access, Keyboard Lock…) korumasız kullanmayın: geliştirme
`localhost`'ta çalıştığı için hata görünmez, sahada HTTP'de ekran kırılır (`crypto.randomUUID` yüzünden HTTP'de giriş hiç yapılamıyordu).
Guid için `newId()` (`lib/sequential-id.ts`), diğerleri için `window.isSecureContext` kontrolü + HTTP yolu.

**Sanal kabin çizimi `src/assets/signalization/` altındaki Figma dışa aktarımlarıdır ve ekranın tek
kaynağıdır (2026-09-28).** `cabinet-inside.svg` kasa + konumlardır: her cihaz kutusu (`<g id="…-Box">`)
yalnızca dolgusuz bir `<rect>` çerçeve taşır. Cihazın görünümü ayrı dosyadadır, her durum ayrı dosya
(`Led-Opened/Closed`, `Siren-Opened/Closed`, `Indoor-Opened/Closed`, `Card-Reader`). Dosyalar `<symbol>`
olarak eklenir ve çerçeveye `<use>` ile çizilir (liste: `components/virtual-cabinet/artwork.ts`).
**SVG'leri TSX'e çevirmeyin** — çevrilen kopya asset'ten ayrışır ve Figma'daki değişiklik ekrana
yansımaz. Yeni cihaz = kasada çerçeveli kutu + görünüm dosyası + `artwork.ts`'e birer satır +
`use-svg-device.ts` ile bağlayan küçük bir bileşen. Eksik kutu/çerçeve çökertmez, geliştirmede
konsola uyarı düşer.

## Bugünün bilinen tutarsızlıkları

Bir şeyin çalıştığını varsaymadan önce doğrulayın:

- **Kanonik API adresi `http://localhost:5208`.** `mediamtx.yml > authHTTPAddress`,
  `Scadex.WebUI/.env.development`, `.env.production` ve
  [axios-helper.ts:7](Scadex.WebUI/src/lib/axios-helper.ts#L7) bu adreste birleştirildi.
  Portu değiştirirseniz dördünü birden güncelleyin. IIS'te HTTPS bağlaması
  varsa `UseHttpsRedirection` MediaMTX'in HTTP auth isteğini yönlendirir: yml'de doğrudan https
  adresi verin (kendinden imzalıysa `authHTTPFingerprint`).
- **FFmpeg'in okuduğu RTSP adresi `rtsp://127.0.0.1:{MediaGateway:RtspPort}`'tur**
  (`IMediaGateway.LiveRtspUrl`). Host bilerek sabittir (MediaMTX ile API aynı sunucuda — 2026-09-25
  kararı); port `mediamtx.yml > rtspAddress` ile eşleşmezse tüm çekimler "Medya geçidine
  ulaşılamıyor" ile düşer. MediaMTX başka makineye taşınırsa ya da RTSPS açılırsa `LiveRtspUrl` değişmeli.
- **AutoMapper 14.0.0 `NU1903` uyarısı kabul edilmiş risktir, yapılacak iş değildir** —
  AutoMapper 15 ticari lisans istiyor, bu yüzden yükseltilmeyecek. `dotnet build` çıktısındaki
  proje başına birer `NU1903` (modül AutoMapper'ı Business'tan geçişli aldığı için 6 proje)
  beklenen gürültüdür; "düzeltmeye" çalışmayın.
- **`npm run lint` yeşil (0 hata, 0 uyarı).** Vendored shadcn/mapcn kodu (`src/components/ui/**`)
  için `react-refresh/only-export-components`, `react-hooks/refs` ve
  `react-hooks/set-state-in-effect` `eslint.config.js`'te bilerek kapalıdır — o dosyaları lint
  için yeniden yazmayın, upstream'den ayrışır. Uygulama kodunda kurallar açıktır; lint'i
  `queueMicrotask`/`requestAnimationFrame` ile atlatmayın.
- **`ChannelEvent` analog kanalda telemetri tablosuna dönüşür ve temizleyen bir iş YOKTUR.**
  2026-09-10 kararı: olay satırı hem `Input` hem `AnalogInput` için yazılır
  (`ChannelEventService`). Dijital kanalda satır yalnızca durum değişince doğar; analogda
  değer neredeyse her ingest'te değiştiği için **her ingest bir satır** demektir. Saklama /
  temizlik işi **bilinçli olarak ertelendi** — proje sahibi sonra ekleyecek. Tablo analog
  kurulumda sınırsız büyür; **sormadan bir silme işi yazmayın.**
- **Sunucudan gelen `...Utc` damgalarında `Z` soneki YOKTUR.** Kolonlar `datetime2`, EF onları
  `DateTimeKind.Unspecified` döndürüyor ve System.Text.Json sonek koymuyor:
  `"occurredAtUtc":"2026-09-10T09:06:56.4089716"`. Frontend'de çıplak `new Date(damga)` bunu
  **yerel saat** sayar ve değeri saat farkı kadar kaydırır. Eksikse `Z` eklenmeli — ortak
  yardımcı: `src/lib/utils.ts > toUtcDate` ve `formatUtcDateTime`. Bu fonksiyonlar `events`,
  `home` ve `command-history.tsx` içerisinde kullanılmaktadır. Yeni ekranlarda da mutlaka bu
  yardımcıyı kullanın.

- **Ingest gövdesinde `cabinetId` YOKTUR; kabin isteği gönderen kontrol kartının MAC kimliğinden çözülür (2026-10-28).**
  Gerçek sözleşme `{ type: "I"|"A", channelNumber, value, timestampUtc }` (`ScadaIngestRequest`) + `DeviceId: <MAC>` header'ı.
  Handler kartın `CabinetId`'sini `cabinet_id` claim'ine yazar, `ScadaController` onu `IChannelEventService.IngestAsync(cabinetId, …)`'a
  verir; header'sız / eşleşmeyen MAC'li istek 401'dir. `ScadaPinAddress.CheckAndParseIngestPin` yalnızca `"I"` ve `"A"`
  kabul eder; `IN<n>`/`OUT<n>` metni (`ScadaPinAddress.Format`) yalnızca ekran ve günlük içindir — tele çıkmaz. **Giden komut
  (2026-10-28):** `POST {ScadaBaseUrl}/command` + `{ channelNumber, value }` (`ScadaCommandRequest`, Docs/scada-http-api.md § 3);
  `delay` (sn; kart çıkışı uygular, bekler, ESKİ hâline döndürür — güç döngüsü, gecikmeli başlatma değil) modelde durur ama
  bugün gönderilmez; `null` tele hiç çıkmaz (`JsonIgnore WhenWritingNull`).
- **Kart okuma ayrı bir uçtur: `POST /api/Scada/card` → `{ cardId, timestampUtc }`
  (2026-09-11).** Kart numarası ölçüm değildir: `IoChannel`'a yazılmaz, `ChannelEvent` üretmez;
  çekirdeğin yazdığı tek şey SCADA temasıdır (kontrol modülü + kabin `LastSeen`, 2026-09-24).
  Kabin ingest ile aynı yoldan kartın kimliğinden gelir (`ICardReadService.IngestAsync(cabinetId, …)`), kart **aktif** kullanıcının
  `User.IdentityCardId`'siyle ham string olarak eşlenir ve sonuç gözlemcilere iletilir. Tanımsız
  kart SCADA için hata değildir (200). `IdentityCardId` aktif kullanıcılar arasında tekildir
  (`IX_User_IdentityCardId`, filtreli); `""` sunucuda `null`'a çekilir — çekmezseniz ikinci boş
  kart 500 üretir. **Kart tanımlama (2026-10-13) çekirdektedir:** her okuma gözlemcilerden önce `DiagramHub`'a `CardRead` olarak
  yayınlanır, Kabinler → "Kart tanımlama" penceresi dinler; atama `PUT /api/User/{id}/identity-card` (`AssignIdentityCardAsync`, yalnızca
  kart alanı — `PUT /api/User` tüm alanları ezer). Pencere okumayı modüllerden **bilerek** saklamaz/baypas etmez (kullanıcı kararı).
- **`IpAddress` ve ek tablo alanları (kimlik bilgileri, kart MAC'i / PC sırrı) diyagramın cihaz uçlarıyla yazılır** (`DeviceCreateRequest` /
  `DeviceUpdateRequest`; ek nesneler `.Camera` / `.ControlCard` / `.Pc`). `WriteDevice` IP'yi, `WriteDeviceExtensions` ek satırı yazar;
  editörde cihaz formundan girilir. Bunlar `DeviceStatusId` / `LastSeen` / `LastConnectionError` gibi telemetri alanı **değildir** — o
  üçü istekte yok ve `WriteDevice`'ta dokunulmaz. İzleme ayarları (`MonitoringPort`, `PingIntervalSec`, `IsMonitoringEnabled`,
  2026-09-24) da aynı istekle yazılır; izleme açıkken IP zorunludur (port değil — boşsa ping, 2026-10-12), izlenemeyen şablonda
  açmak 400'dür (`ValidateDeviceAsync`). Şablonların
  güncelleme ucu yoktur: `IsMonitorable` oluşturmada (ve sistem şablonlarında seed'de) belirlenir.

## Sormadan "düzeltmeyin"

PROJECT_OVERVIEW.md §7'deki **bilinçli boşluklar** unutulmuş değil, sıraya alınmıştır. Talep
edilmeden bunlara girmeyin: kiracı izolasyonu yok, SCADA komutlarında retry/kuyruk yok (tekrarlanan röle darbesi başarısız
komuttan kötüdür), kamera parolası düz metin saklanır, saklama/temizlik işleri yok,
`ChannelEvent` bir değişim günlüğüdür — zaman serisi değildir.

Bir alanı değiştirmeden önce entity'nin XML doc'unu okuyun: gerekçe oraya yazılmıştır.
