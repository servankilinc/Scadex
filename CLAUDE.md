# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Bu dosya ne değildir

Bu dosya bir özet değildir. Sistemin **ne yaptığı, hangi kararın neden verildiği ve neyin
bilinçli olarak eksik bırakıldığı** [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) içinde yazar —
600 satırlık o doküman tek doğruluk kaynağıdır. Buradaki içerik yalnızca **koda dokunmadan
önce bilinmesi gerekenlerdir**: çalışma dili, komutlar, katman yönü ve tek bir dosyaya bakarak
asla fark edilemeyecek kurallar.

Ek kaynaklar: [Docs/scada_communication_guide.md](Docs/scada_communication_guide.md) (SCADA
protokol detayı), [Docs/data-structor.md](Docs/data-structor.md) (veri modeli notları).

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

- **`Scadex.WebUI` çözüme dahil değildir.** `Scadex.slnx` 9 .NET projesini taşır (5 çekirdek katman +
  `Scadex.Signalization` + `Modules/RemoteDesk/` altındaki üç proje); frontend ayrı çalıştırılır. Çözümde WPF
  istemcisi (`Scadex.RemoteDesk.Windows`, `net10.0-windows`) olduğu için `dotnet build Scadex.slnx` yalnızca Windows'ta derlenir.
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
  bilerek yoktur (gösterge + `ScreenViewLog`). **Modülün HTTP uçları `RemotePcView` iznini ister** (projede zorlanan ilk izin,
  aşağıdaki "yetki zorlaması yok" kuralının tek istisnası): policy modülün kaydında (`RemoteDeskModule.ViewPolicy`), izin kodu
  `Permission.RemotePcView` adıyla aynı — enum'u yeniden adlandırmayın. `PcHub` anonim kalır.
  **Uzaktan kontrol (Faz 8, yalnızca fare) ayrı izindir: `RemotePcControl`** — `/hubs/remote-desk/viewer` bu policy ile korunur
  (`RemoteDeskModule.ControlPolicy`). Kontrol PC başına tek kullanıcıdadır ve yalnızca o PC'yi **izleyene** verilir (canlı kiralama;
  izleme biterse kontrol süpürmede düşer) — bu bağı gevşetmeyin, görmeden tıklamak demektir. Girdi sunucuda saklanmaz/sıraya alınmaz,
  yalnızca `RemoteControlSession` denetim satırı yazılır (RemoteDesk.md § 12.7).
  **Windows istemcisinin kurulum paketi bilerek yoktur (2026-09-30):** `dotnet publish ... -c Release -r win-x64 --self-contained true -o <klasör>`
  ile klasöre alınıp elle dağıtılır, başlangıç uygulamalarına `--tray` ile eklenir (RemoteDesk.md § 9.6). `tools\ffmpeg\` (LGPL exe +
  `LICENSE.txt`) git'te yoktur; eksikse publish bilerek hata verir. İstemci günlüğü `%LocalAppData%\Scadex\RemoteDesk\logs`'tadır.

- **Çekirdek modülü bilmez.** Tek temas noktası `IScadaEventObserver` (Business/Utils/ScadaEvents):
  ingest (değer gerçekten değişince), **başarılı çıkış komutu** (kanal değeri değişince, `Direction =
  Output` + mantıksal `TurnOn`; 2026-09-21) ve `POST /api/Scada/card` gözlemcileri yazımdan SONRA
  çağırır. Gözlemci **sıcak yoldadır — yalnızca kuyruğa bırakır**; içinde SCADA'ya komut göndermek,
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
  aynı ProblemDetails sözleşmesini Core'un `GetProblemDetail`'i ile kurar. Modül uçlarına
  **bilinçli olarak rate limit takılmaz** (2026-09-11 kararı) — `[EnableRateLimiting]` eklemeyin;
  eklenirse politika adı modülün kendi kaydında da tanımlanmalı, yoksa uç her istekte 500 döner.

**Büyük servisler `#region` ile değil, partial sınıflara bölünür** — gruplama birimi dosyadır:
`CameraService.cs` + `.Streaming` + `.Snapshot` + `.Capture` + `.Monitoring`,
`DeviceCommandService.cs` + `.Comunication`,
`DiagramService.cs` + `.Save` + `.SaveContext` + `.SaveDevices` + `.SaveDevicePins` +
`.SaveConnections` + `.SaveAnnotations`.

Diyagram kaydetme iki eksende bölünür: `.Save` orkestrasyon (transaction, iki
`SaveChangesAsync`), `.SaveContext` dört dağıtıcı + üç ailenin de okuduğu `SaveContext` +
**akış haritası**; aile dosyalarının her biri kendi **yükleme → doğrulama → uygulama**
dikey dilimini taşır. Nereye ne ekleneceğini `.SaveContext` başındaki haritadan okuyun.
**`.Save` dosyasına tek satır bile eklemeyin** — `Result.Validation` `[CallerLineNumber]`
ile hata metadata'sı üretiyor, satır kaydırmak üretimdeki kayıtların izini değiştirir.

## Kırılmaması gereken kurallar

Bunlar tek bir dosyaya bakarak görülemez; gerekçeleri PROJECT_OVERVIEW.md §5-§6'dadır.

- **Her tablonun tek bir yazım yolu vardır.** `Device`, `Connection`, `DiagramAnnotation`,
  `Pin`, `IoChannel`, `ComponentTemplatePin` yalnızca `POST /api/Diagram/cabinet/{id}/save`
  deltası üzerinden yazılır; `CanvasSettings` yalnızca
  `PUT /api/CanvasSettings/cabinet/{cabinetId}` üzerinden. Bu tabloların generic
  controller'ları **bilerek salt okunurdur** — oraya Create/Update/Delete geri eklemeyin.
  İhtiyaç varsa sahibinin controller'ına ekran bazlı bir uç ekleyin.
- **Kaydetme sözleşmesinde `created`/`updated` ayrımı yoktur.** Gövde
  `{ upserted: [...], deleted: [ids] }`; **Guid'i istemci üretir**, sunucu kimliği arar.
  Pin ve kanal kimlikleri salt-oluşturmadır; mevcut cihaza `pins`/`ioChannels` göndermek 400'dür.
- **Kanal adresleri kabin genelinde tekildir**, cihaz genelinde değil. Bir cihazı silmek
  kanallarını serbest bırakmaz (`IN1` işgal edilmeye devam eder), `MacAddress` ise serbest kalır.
- **`MacAddress` benzersizliği kabin genelinde DEĞİL, sistem genelindedir.**
  `IX_Device_MacAddress` (unique, `WHERE MacAddress IS NOT NULL AND IsActive = 1`) globaldir:
  bir fiziksel kartın tek MAC'i vardır ve ingest kabini bu adresten çözer — aynı adres iki
  kabinde olsaydı telemetri yanlış kabine yazılırdı. Bu yüzden `LoadDeviceMacAddressesAsync`
  kabinle değil, **gönderilen adreslerle** daraltılır; yeni bir benzersizlik kuralını bu kalıptan
  kopyalarken kabin/sistem farkını atlamayın. Ön doğrulama (`ValidateDeviceMacAddresses`) DB kısıtına
  çarpıp 500 üretmemek içindir, süs değil.
- **Lifecycle interceptor'ları sessizce geçmez, istisna atar.** `IImmutableEntity` güncelleme
  ve silmede, `IActivatableEntity` silmede patlar (`IsActive = false` kullanın);
  `ISoftDeletableEntity` fiziksel silmeyi `IsDeleted = true`'ya çevirir. `IsActive` üzerinde
  global query filter **yoktur**, `IsDeleted` üzerinde **vardır**.
- **`DiagramService.SaveAsync` projedeki tek istisnadır:** repository'nin `*AndSave*`
  kısayollarını kullanmaz, transaction açar. İçindeki **iki `SaveChangesAsync`** (önce silmeler,
  sonra yazmalar) bilinçlidir — filtreli unique index yüzünden tek batch'te sıra garanti değil
  ve INSERT önce giderse 500 döner. Sıradan CRUD servisleri `*AndSave*` kullanmaya devam eder.
- **Yanıt zarfı yoktur.** `Result<T>.Success(data)` → `200` + çıplak DTO. Hatalar RFC 7807
  `ProblemDetails`; gövdedeki `code` uzantısı HTTP kodu **değildir**.
- **Enum'lar sayı olarak serileştirilir** (`JsonStringEnumConverter` bilerek kayıtlı değil) ve
  numaraları boşlukludur. **`DictionaryKeyPolicy` bilerek `null`** — `ProblemDetails.errors`
  anahtarları PascalCase kalır (`"Devices.Upserted[0].Pins"`); iki taraftan birini "düzeltmeyin".
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
  **yalnızca TCP connect** yapar — ICMP dalı bilerek yoktur, `MonitoringPort` null ise varlık
  atlanır. Buraya "şimdi dene" tarzı bir uç eklemeyin. Kaynaklar: `CameraProbeSource`,
  `DeviceProbeSource` (2026-09-24 — `Device : IMonitoredAsset`; yalnızca izlemesi açık **ve**
  şablonu `ComponentTemplate.IsMonitorable` olan cihaz). Worker son sonda anı olarak sondanın
  bitişini yazar (bilinçli); `PingIntervalSec` tur aralığının tam katıysa hedef bir tur geç
  yoklanır (60 sn → fiilen 90 sn).
- **Kabin durumu ve cihaz CANLILIK durumu yalnızca `ICabinetStatusService` üzerinden yazılır
  (2026-09-24).** `Cabinet.DeviceStatusId`, `Device.DeviceStatusId` / `LastSeen` /
  `LastConnectionError`'a başka yerden dokunmayın; kanıtı (yoklama, SCADA teması, komut
  `NoResponse`) servise verin. Kabin durumu = cihaz katkılarının en kötüsü ve **tabanı kontrol
  modülüdür**: kabini Online'a da Offline'a da yalnızca kontrol modülü çeker; diğer cihazların ve
  izlenen kameraların `Offline`'ı kabine `Warning` olarak yansır, `Online`'ları hiç katılmaz
  (`CabinetContribution`). Durum tabloları: PROJECT_OVERVIEW.md § 5.3 "Canlılık". SCADA canlılığı **kontrol modülünde** tutulur
  (kanıt kart başınadır): her ingest (tanımsız kanal dahil), kart okuma ve başarılı komut temas
  sayılır; giriş modülüne canlılık yazmayın. Başarısız sonda **ilk başarısızlıkta** Offline yapar
  (kamerayla aynı); `NoResponse` anında. **`LastSeen`'e bakıp Offline yargısına varılmaz** — yalnızca
  izlemesi kapalı cihaz/kameraların `LastSeen`'i dolu ve 23 saatten eski Online/Offline'ı taramada `null`'a
  ("Bilinmiyor") çekilir; boş `LastSeen` eski sayılmaz (hiç görülmemiş kartın `NoResponse` Offline'ı silinmesin)
  (izlenenleri katmayın: yoklamayla her turda Bilinmiyor ↔ Offline salınırlar),
  23 saat bilerek sabittir. Canlılık yalnızca Online/Offline/`null`'a karar verir,
  Warning/Critical/Maintenance korunur. Sinyalizasyon modülü kabin durumuna **bilerek**
  yansımaz (alarm haritada ayrı ikon; `AppModule.alertCabinetsQuery`). **Kabinde `Maintenance` yapışkan bir
  operatör kararıdır (2026-09-28):** ayrı kolon yok, `Cabinet.DeviceStatusId` = Maintenance; kabin düzenleme ekranından
  `ICabinetStatusService.SetMaintenanceAsync` ile yazılır. Hesaplama ve tarama bakımdaki kabine dokunmaz, hesaplama da
  hiçbir zaman Maintenance üretmez (cihazın Maintenance'ı kabine taşınmaz) — yoksa kabin kendiliğinden bakıma düşüp kalırdı.
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
  Bir `[EnableRateLimiting]` adını silmeden/değiştirmeden önce `RateLimiterKey`'e bakın.
  Politikalar: `Default`, `Scada`, `MediaGateway`. Sinyalizasyon modülünün uçlarında politika
  yoktur (bilinçli).
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
  Portu değiştirirseniz dördünü birden güncelleyin. (`src/lib/diagram/template-image.ts`
  içindeki bir yorum hâlâ eski `:7042`'yi anıyor — yalnızca yorum.) IIS'te HTTPS bağlaması
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

- **Ingest gövdesinde `cabinetId` YOKTUR; kabin MAC adresinden çözülür.** Gerçek sözleşme
  `{ macAddress, type: "I"|"A", channelNumber, value, timestampUtc }` (`ScadaIngestRequest`).
  Sahadaki SCADA bizim ürettiğimiz Guid'i bilemez; sunucu gelen adresle **birebir eşleşen**,
  aktif ve şablonu `DeviceType.ControlModule` olan cihazın `CabinetId`'sini kullanır
  (`ChannelEventService.IngestAsync` adım 3), karşılığı yoksa 404 döner. **MAC tek tip saklanır ve
  gelirken tek tipe çevrilir (2026-09-30):** `AA:BB:CC:DD:EE:FF` (büyük harf, `:`), kural
  `Scadex.Core/Utils/MacAddressFormat.cs`. Çeviri giriş DTO'larının setter'ındadır (`DeviceDraft`,
  `ScadaIngestRequest`, `ScadaCardReadRequest`), bu yüzden karşılaştırma yine düz string eşitliğidir ve
  SCADA `50-8d-…` de gönderse eşleşir. Hex şartı **bilinçli yok** (12 harf/rakam): test kabinlerindeki
  `CL:AU:…` adresleri geçerli kalmalı. Yeni bir MAC girişi eklerseniz aynı setter kalıbını kullanın; `AutoMapper`
  bu alanı yazmaz (Device'ın tek yazım yolu diyagram deltası). `ScadaPinAddress.CheckAndParseIngestPin` yalnızca `"I"` ve `"A"`
  kabul eder; `IN<n>`/`OUT<n>` metni yalnızca **giden** komut gövdesinde (`Format`) kullanılır.
- **Kart okuma ayrı bir uçtur: `POST /api/Scada/card` → `{ macAddress, cardId, timestampUtc }`
  (2026-09-11).** Kart numarası ölçüm değildir: `IoChannel`'a yazılmaz, `ChannelEvent` üretmez;
  çekirdeğin yazdığı tek şey SCADA temasıdır (kontrol modülü + kabin `LastSeen`, 2026-09-24).
  Kabin ingest ile aynı yoldan MAC'ten çözülür
  (`IDeviceRepository.GetCabinetIdByControlModuleMacAsync`), kart **aktif** kullanıcının
  `User.IdentityCardId`'siyle ham string olarak eşlenir ve sonuç gözlemcilere iletilir. Tanımsız
  kart SCADA için hata değildir (200). `IdentityCardId` aktif kullanıcılar arasında tekildir
  (`IX_User_IdentityCardId`, filtreli); `""` sunucuda `null`'a çekilir — çekmezseniz ikinci boş
  kart 500 üretir.
- **`MacAddress` / `IpAddress` artık diyagram deltasıyla yazılır (2026-09-10).** `DeviceDraft`
  bu iki alanı taşır, `WriteDevice` yazar, editörde cihaz özellik panelinden girilir. Bunlar
  `DeviceStatusId` / `LastSeen` / `LastConnectionError` gibi telemetri alanı **değildir** — o
  üçü taslakta yok ve `WriteDevice`'ta dokunulmaz. İzleme ayarları (`MonitoringPort`,
  `PingIntervalSec`, `IsMonitoringEnabled`, 2026-09-24) da deltayla yazılır; izleme açıkken IP ve
  port zorunludur, izlenemeyen şablonda açmak 400'dür (`ValidateDeviceMonitoring`). Şablonların
  güncelleme ucu yoktur: `IsMonitorable` oluşturmada (ve sistem şablonlarında seed'de) belirlenir.

## Sormadan "düzeltmeyin"

PROJECT_OVERVIEW.md §7'deki **bilinçli boşluklar** unutulmuş değil, sıraya alınmıştır. Talep
edilmeden bunlara girmeyin: yetki zorlaması yok (`permission` claim'i üretilir ama okunmaz; tek istisna RemoteDesk'in
`RemotePcView` policy'si, 2026-09-30),
kiracı izolasyonu yok, SCADA komutlarında retry/kuyruk yok (tekrarlanan röle darbesi başarısız
komuttan kötüdür), kamera parolası düz metin saklanır, saklama/temizlik işleri yok,
`ChannelEvent` bir değişim günlüğüdür — zaman serisi değildir.

Bir alanı değiştirmeden önce entity'nin XML doc'unu okuyun: gerekçe oraya yazılmıştır.
