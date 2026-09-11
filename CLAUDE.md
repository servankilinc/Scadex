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

# İki DbContext var: --context ZORUNLU (vermezseniz "More than one DbContext was found")
dotnet ef migrations add <Ad> --project Scadex.DataAccess --startup-project Scadex.WebAPI --context AppDbContext
dotnet ef database update     --project Scadex.DataAccess --startup-project Scadex.WebAPI --context AppDbContext

# Sinyalizasyon modülü (signalization şeması, kendi migration geçmişi)
dotnet ef migrations add <Ad> --project Scadex.Signalization --startup-project Scadex.WebAPI --context SignalizationDbContext --output-dir Data/Migrations
dotnet ef database update     --project Scadex.Signalization --startup-project Scadex.WebAPI --context SignalizationDbContext

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

- **`Scadex.WebUI` çözüme dahil değildir.** `Scadex.slnx` yalnızca 6 .NET projesini taşır
  (5 çekirdek katman + `Scadex.Signalization` modülü); frontend ayrı çalıştırılır.
- **Test paketi yoktur.** Otomatik kontrol yalnızca `dotnet build` ve frontend tarafında
  `npm run lint` + `npm run build`. Davranış, uygulamayı çalıştırarak doğrulanır — bir
  değişikliğin çalıştığını iddia etmeden önce gerçekten çalıştırın. (`npm run lint` ve
  `npm run build` bugün yeşil; yeşil kalmalı.)
- **`npm run typecheck` diye bir script yoktur** (`build` zaten `tsc -b` çalıştırır).
- MediaMTX uygulama tarafından başlatılmaz; ayrı süreçtir
  (`Scadex.WebAPI/mediamtx_v1.20.1/mediamtx.exe`).

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
- `dotnet ef` tasarım zamanında `Development` ortamını kullanır; modül orada açık olduğu için
  `--context SignalizationDbContext` komutları çalışır. Modülü Development'ta kapatırsanız EF
  context'i bulamaz.

- **Çekirdek modülü bilmez.** Tek temas noktası `IScadaEventObserver` (Business/Utils/ScadaEvents):
  ingest (değer gerçekten değişince) ve `POST /api/Scada/card` gözlemcileri `SaveChanges`'ten
  SONRA çağırır. Gözlemci **sıcak yoldadır — yalnızca kuyruğa bırakır**; içinde SCADA'ya komut
  göndermek, kart isteğini bekleyen SCADA kartıyla kilitlenme demektir.
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
`DiagramService.cs` + `.Save` + `.SaveHelpers`, `DeviceCommandService.cs` + `.Comunication`.

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
  kanallarını serbest bırakmaz (`IN1` işgal edilmeye devam eder), `ExternalCode` ise serbest kalır.
- **`MacAddress` benzersizliği kabin genelinde DEĞİL, sistem genelindedir.**
  `IX_Device_MacAddress` (unique, `WHERE MacAddress IS NOT NULL AND IsActive = 1`) globaldir:
  bir fiziksel kartın tek MAC'i vardır ve ingest kabini bu adresten çözer — aynı adres iki
  kabinde olsaydı telemetri yanlış kabine yazılırdı. Bu yüzden `LoadDeviceMacAddressesAsync`
  kabinle değil, **gönderilen adreslerle** daraltılır; `ExternalCode`'un kabin bazlı sürümünü
  kopyalarken bu farkı atlamayın. Ön doğrulama (`ValidateDeviceMacAddresses`) DB kısıtına
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
- **Medya geçidi ve kamera çekim ayarları `appsettings.json`'da DEĞİL, veritabanındadır.**
  Tip başına tek satırlık tablo + ayar nesnesi başına ayrı servis
  (`IMediaGatewaySettingService`, `ICameraCaptureSettingService`), `ICacheService` ile
  önbeleklenir, her yazma kendi anahtarını düşürür. `appsettings.json`'a `MediaGateway` /
  `Cameras` bölümü geri eklemeyin — **okunmuyor**, sessizce yok sayılır.
  Adlandırılmış `HttpClient` yine `Program.cs`'te kurulur ama `BaseAddress`/`Timeout` orada
  **verilmez**; `MediaMtxGateway.CreateConfiguredClient` her çağrıda ayardan uygular.
  Ekranı `/admin/settings`; zod şemaları (`models/mediaGatewaySetting`,
  `models/cameraCaptureSetting`) sunucudaki `FluentValidation` kurallarının **elle tutulan
  kopyasıdır** — sunucudaki kuralı değiştirirseniz şemayı da değiştirin, codegen yok.
- **Yoklama (ayakta mı) akışının HTTP yüzeyi yoktur ve tipe özel değildir.**
  `MonitoredAssetProbeWorker` kayıtlı her `IMonitoredAssetProbeSource` üzerinden döner; yeni
  bir izlenen tip eklemek = yeni kaynak + tek satır DI kaydı, worker'a dokunulmaz. Sonda
  **yalnızca TCP connect** yapar — ICMP dalı bilerek yoktur, `MonitoringPort` null ise varlık
  atlanır. Buraya "şimdi dene" tarzı bir uç eklemeyin.
- **MediaMTX yol temizliği üç şeyi asla silmez:** bizim üretmediğimiz adlar (yalnızca `cam_` /
  `clip_` önekliler adaydır — `mediamtx.yml`'deki **`all_others`** silinseydi geçit
  yapılandırmasız kalırdı), `record: true` olan yollar (devam eden klip çekimi) ve
  `readers > 0` olan yollar. Bu kuralları gevşetmeyin; `MediaPathCleanupWorker.ShouldDelete`.
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

Yığın: React 19 + Vite 8 + TS 6, React Flow (`@xyflow/react`), TanStack Query, Redux Toolkit,
shadcn + Tailwind 4, react-hook-form + zod, MapLibre, Recharts.

**Bir C# DTO'sunu değiştirdiğinizde TS aynasını elle güncelleyin** — codegen de test de yok,
kimse bunu sizin için yakalamaz.

## Bugünün bilinen tutarsızlıkları

Bir şeyin çalıştığını varsaymadan önce doğrulayın:

- **Kanonik API adresi `http://localhost:5208`.** `mediamtx.yml > authHTTPAddress`,
  `Scadex.WebUI/.env.development`, `.env.production` ve
  [axios-helper.ts:7](Scadex.WebUI/src/lib/axios-helper.ts#L7) bu adreste birleştirildi.
  Portu değiştirirseniz dördünü birden güncelleyin. (`src/lib/diagram/template-image.ts`
  içindeki bir yorum hâlâ eski `:7042`'yi anıyor — yalnızca yorum.)
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
  (`ChannelEventService.IngestAsync` adım 3), karşılığı yoksa 404 döner. Karşılaştırma **ham
  string** karşılaştırmasıdır — ayraç/harf normalizasyonu bilerek yoktur, adres veritabanındaki
  yazımıyla gönderilmelidir. `ScadaPinAddress.CheckAndParseIngestPin` yalnızca `"I"` ve `"A"`
  kabul eder; `IN<n>`/`OUT<n>` metni yalnızca **giden** komut gövdesinde (`Format`) kullanılır.
- **Kart okuma ayrı bir uçtur: `POST /api/Scada/card` → `{ macAddress, cardId, timestampUtc }`
  (2026-09-11).** Kart numarası ölçüm değildir: `IoChannel`'a yazılmaz, `ChannelEvent` üretmez,
  çekirdek hiçbir satır yazmaz. Kabin ingest ile aynı yoldan MAC'ten çözülür
  (`IDeviceRepository.GetCabinetIdByControlModuleMacAsync`), kart **aktif** kullanıcının
  `User.IdentityCardId`'siyle ham string olarak eşlenir ve sonuç gözlemcilere iletilir. Tanımsız
  kart SCADA için hata değildir (200). `IdentityCardId` aktif kullanıcılar arasında tekildir
  (`IX_User_IdentityCardId`, filtreli); `""` sunucuda `null`'a çekilir — çekmezseniz ikinci boş
  kart 500 üretir.
- **`MacAddress` / `IpAddress` artık diyagram deltasıyla yazılır (2026-09-10).** `DeviceDraft`
  bu iki alanı taşır, `WriteDevice` yazar, editörde cihaz özellik panelinden girilir. Bunlar
  `DeviceStatusId` / `LastSeen` gibi telemetri alanı **değildir** — o ikisi hâlâ taslakta yok
  ve `WriteDevice`'ta dokunulmaz.

## Sormadan "düzeltmeyin"

PROJECT_OVERVIEW.md §7'deki **bilinçli boşluklar** unutulmuş değil, sıraya alınmıştır. Talep
edilmeden bunlara girmeyin: yetki zorlaması yok (`permission` claim'i üretilir ama okunmaz),
kiracı izolasyonu yok, SCADA komutlarında retry/kuyruk yok (tekrarlanan röle darbesi başarısız
komuttan kötüdür), kamera parolası düz metin saklanır, saklama/temizlik işleri yok,
`ChannelEvent` bir değişim günlüğüdür — zaman serisi değildir.

Bir alanı değiştirmeden önce entity'nin XML doc'unu okuyun: gerekçe oraya yazılmıştır.
