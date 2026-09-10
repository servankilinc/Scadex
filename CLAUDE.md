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

dotnet ef migrations add <Ad> --project Scadex.DataAccess --startup-project Scadex.WebAPI
dotnet ef database update     --project Scadex.DataAccess --startup-project Scadex.WebAPI

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

- **`Scadex.WebUI` çözüme dahil değildir.** `Scadex.slnx` yalnızca 5 .NET projesini taşır;
  frontend ayrı çalıştırılır.
- **Test paketi yoktur.** Otomatik kontrol yalnızca `dotnet build` ve frontend tarafında
  `npm run lint` + `npm run build`. Davranış, uygulamayı çalıştırarak doğrulanır — bir
  değişikliğin çalıştığını iddia etmeden önce gerçekten çalıştırın. (Her ikisi de bugün
  kırmızı; aşağıdaki "bilinen tutarsızlıklar"a bakın.)
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
- **Rate limit politika adı `Program.cs`'te tanımlı değilse o uç HER istekte 500 döner.**
  Bir `[EnableRateLimiting]` adını silmeden/değiştirmeden önce `RateLimiterKey`'e bakın.
  Politikalar: `Default`, `Scada`, `MediaGateway`.
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
  5 uyarı beklenen gürültüdür; "düzeltmeye" çalışmayın.
- **`npm run lint` şu an kırmızı:** 21 mevcut hata (11 `react-hooks/refs`,
  8 `react-refresh/only-export-components`, 2 `react-hooks/set-state-in-effect`). Kendi
  değişikliğinizin yeni hata eklemediğini doğrulayın; bu 21'i temizlemek ayrı bir iştir.
- **`ChannelEvent` analog kanalda telemetri tablosuna dönüşür ve temizleyen bir iş yoktur.**
  2026-09-10 kararı: olay satırı hem `Input` hem `AnalogInput` için yazılır
  (`ChannelEventService`). Dijital kanalda satır yalnızca durum değişince doğar; analogda
  değer neredeyse her ingest'te değiştiği için **her ingest bir satır** demektir. Saklama /
  temizlik işi henüz yazılmadı — analog kart kullanan bir kurulumda tablo sınırsız büyür.
- **PROJECT_OVERVIEW.md §5.3'teki ingest gövdesi eskimiştir.** Doküman
  `{ cabinetId, pin: "IN7", value }` diyor; **gerçek sözleşme**
  `{ cabinetId, type: "I"|"A", channelNumber, value, timestampUtc }`
  (`ScadaIngestRequest`). `ScadaPinAddress.TryParseType` yalnızca `"I"` ve `"A"` kabul eder;
  `IN<n>`/`OUT<n>` metni artık yalnızca **giden** komut gövdesinde (`Format`) kullanılır.

## Sormadan "düzeltmeyin"

PROJECT_OVERVIEW.md §7'deki **bilinçli boşluklar** unutulmuş değil, sıraya alınmıştır. Talep
edilmeden bunlara girmeyin: yetki zorlaması yok (`permission` claim'i üretilir ama okunmaz),
kiracı izolasyonu yok, SCADA komutlarında retry/kuyruk yok (tekrarlanan röle darbesi başarısız
komuttan kötüdür), kamera parolası düz metin saklanır, saklama/temizlik işleri yok,
`ChannelEvent` bir değişim günlüğüdür — zaman serisi değildir.

Bir alanı değiştirmeden önce entity'nin XML doc'unu okuyun: gerekçe oraya yazılmıştır.
