# Scadex — Proje Tanıtımı

> Bu dosya, projeye ilk kez bakan bir insana **veya** bir AI agent'ına bağlamı tek seferde
> vermek için yazıldı: sistem ne yapar, hangi kararlar bilinçlidir, bugün ne çalışır,
> sırada ne var. Kod okumadan önce buradan başlayın.
>
> **Çalışma dili Türkçe'dir.** Yorumlar, XML doc'lar, hata mesajları ve dokümanlar Türkçe
> yazılır; sınıf/metot/DTO isimleri İngilizce kalır. Mevcut dosyaları düzenlerken bu ayrımı koruyun.

---

## 1. Scadex nedir?

Scadex, sahadaki bir **SCADA sisteminin üstünde** çalışan, sektörden bağımsız bir
süpervizyon platformudur. Kendisi Modbus/RS485 konuşmaz, seri porta inmez, PLC sürmez.
SCADA ile **HTTP üzerinden, iki yönlü olarak** yalnızca şu üçlüyü değiş tokuş eder:

```
{ kabin kimliği, pin adresi (IN7 / OUT5), değer }
```

Projenin kalbindeki fikir şudur:

> **Anlamın kaynağı pano bağlantı şemasıdır.**
> Operatör hangi pinin hangi sensöre gittiğini diyagram editöründe kendisi çizdiği için,
> ham "7 numaralı kanal 1 oldu" verisi ekranda "dış kapı hareket algıladı" olarak
> görüntülenebilir. Bu eşleşme koda gömülmez, veriden okunur — bu yüzden yeni bir cihaz
> tipi eklemek için deploy gerekmez.

Üç yönetici ilke:

1. **Hiçbir şey hard-code edilmez.** Cihaz tipleri, pin şemaları, kanal numaraları, portlar
   ve stream kanalları veri/ayardır; kod değildir.
2. **Her tablonun tek bir yazım yolu vardır.** İkinci bir yazım yolu, doğrulamayı atlatan
   bir arka kapı demektir.
3. **Sessiz başarısızlık yoktur, gereksiz gürültü de yoktur.** Tanımsız referans loglanır,
   isteği düşürmez; değişmeyen değer yazılmaz ve yayınlanmaz.

Bu depo, aynı ürünün önceki sürümünün (CabinetOS) **temize çekilmiş backend'idir**:
katmanlar yeniden kuruldu, ASP.NET Identity + refresh token akışı eklendi, DeviceStatus /
DeviceType / Permission birer lookup tablosuna dönüştürüldü, SCADA ingest tekil-okuma
sözleşmesine sadeleştirildi.

---

## 2. Depo yapısı

```
Scadex/
├─ Scadex.slnx              # .NET 10 çözümü (WebUI çözüme dahil DEĞİL)
├─ Scadex.Core/             # Altyapı — domain bilgisi yok
├─ Scadex.Model/            # Entity'ler, DTO'lar, enum'lar
├─ Scadex.DataAccess/       # AppDbContext, repository'ler, UoW, interceptor'lar
├─ Scadex.Business/         # Servisler, gateway'ler, mapping, ayar sınıfları
├─ Scadex.WebAPI/           # Controller'lar, SignalR hub, hosted service'ler, Program.cs
│  └─ mediamtx_v1.20.1/     # Medya sunucusu (uygulama tarafından başlatılmaz)
└─ Scadex.WebUI/            # React 19 + Vite 8 + TS 6 — diyagram editörü, kamera izleme
                            # ve auth akışı yazıldı (çözüme dahil DEĞİL, ayrı çalıştırılır)
```

Katman yönü tek yönlüdür ve kırılmamalıdır:

```
Core → Model → DataAccess → Business → WebAPI
```

Her katmanın kendi `ServiceRegistration.AddXServices()` uzantısı vardır ve `Program.cs`
bunları sırayla çağırır.

| Katman | İçerdiği |
|---|---|
| **Core** | `Result` / `Result<T>` deseni, `IQueryable` üzerinde dinamik filtre/sıralama/sayfalama/datatable motoru, `ProjectJsonOptions`, cache, localization, `IHttpContextManager`, FluentValidation altyapısı, Serilog |
| **Model** | Entity'ler (`Entities/`), lifecycle arayüzleri (`Core/Model/IEntity.cs`), DTO'lar (`Dtos/<Aggregate>/{Commands,Queries}/`), `Enums/EntityEnums.cs` |
| **DataAccess** | `AppDbContext` (Identity tabanlı), generic `RepositoryBase`, entity başına repository, tüm repository'leri + transaction'ı taşıyan `IUnitOfWork`, üç `SaveChanges` interceptor'ı |
| **Business** | Aggregate başına bir servis (`Abstract/I*Service.cs` + `Concrete/*Service.cs`), `Result<T>` döner, `IValidationService` ile doğrular, AutoMapper `ProjectTo` ile projeksiyon yapar; dış dünyaya açılan gateway'ler `Utils/` altında |
| **WebAPI** | `BaseController`'dan türeyen ince controller'lar, `DiagramHub`, hosted service'ler, exception middleware |

**Büyük servisler partial sınıflara bölünür, `#region` ile değil — gruplama birimi dosyadır:**
`CameraService.cs` + `.Streaming` + `.Snapshot` + `.Capture` + `.Monitoring`,
`DiagramService.cs` + `.Save` + `.SaveHelpers`,
`DeviceCommandService.cs` + `.Comunication`.

---

## 3. Teknoloji ve sürümler

| | |
|---|---|
| Runtime | .NET 10 (`net10.0`) |
| Veritabanı | SQL Server + EF Core 10 |
| Kimlik | ASP.NET Core Identity (`IdentityDbContext<User, Role, Guid>`) + JWT + refresh token |
| Gerçek zaman | SignalR — `/hubs/diagram` |
| API dokümanı | OpenAPI + Scalar UI |
| Doğrulama | FluentValidation 12 |
| Mapping | AutoMapper 14 |
| Log | Serilog (dosya sink'i, async) |
| Medya | MediaMTX v1.20.1 (uygulamanın **dışında** çalışır) |
| Frontend | React 19 + Vite 8 + TypeScript 6 — React Flow (`@xyflow/react`), TanStack Query, Redux Toolkit, shadcn + Tailwind 4, react-hook-form + zod, SignalR istemcisi, MapLibre, Recharts |

---

## 4. Veri modeli — kısa tur

**Organizasyon:** `Company` → `Cabinet` → `Device` → `Pin` / `IoChannel`

- **`Cabinet`** — bir pano. SCADA adresi (`ScadaBaseUrl`), açık/kapalı bayrağı
  (`ScadaIsEnabled`), komut zaman aşımı (`ScadaCommandTimeoutMs`), son ingest zamanı ve
  toplu durum (`DeviceStatusId`) burada durur. Kabin durumu, içindeki cihazların **en kötü**
  durumundan hesaplanır (`DeviceStatusSeverityRank`).
- **`ComponentTemplate` + `ComponentTemplatePin`** — stencil kütüphanesi. Şablon, cihazın
  kutu boyutunu, zemin rengini ve pin şemasını taşır. Palet kabinden bağımsızdır.
- **`Device`** — diyagramdaki bir kutu. Şablondan üretilir; `ExternalCode` SCADA'nın modül
  kodudur (kabin içinde tekil).
- **`Pin`** — kutunun bir bacağı. `RelativeX/Y` şablonun genişlik/yüksekliğinin **0..1
  normalize kesridir** (veritabanında `CHECK` ile zorlanır), böylece şablon yeniden
  boyutlandığında pinler bozulmaz. `Device.CoordinateX/Y`, açıklama koordinatları ve kablo
  `waypoints[]` ise ortak **canvas piksel** uzayındadır.
- **`IoChannel`** — SCADA'nın konuştuğu kanal. Kabin + yön + kanal numarası tekildir.
  Anlık değer (`CurrentValue`) string tutulur; `null` = "kanal var, okunamadı" ve `"0"` ile
  aynı şey **değildir**.
- **`Connection`** — iki pin arasındaki kablo. Aynı pin kendisine bağlanamaz (DB `CHECK`),
  aynı pin çifti birden fazla kez bağlanamaz (filtreli unique index).
- **`DiagramAnnotation`**, **`CanvasSettings`** — diyagramın serbest notları ve tuval ayarı.
- **`ChannelEvent`** — giriş kanalındaki değer değişimlerinin kaydı.
- **`DeviceCommand`** — bize giden değil, **bizden SCADA'ya giden** komutun geçmişi.
- **`Camera` + `CameraCapture`** — izleme tarafı; diyagramın parçası değildir.
- **`RefreshToken`**, **`Permission`**, **`RolePermission`** — kimlik/yetki tarafı.
- **`Log`**, **`Archive`** — `IProjectEntity`, interceptor'ların dışında kalır.

### Entity lifecycle işaretleri

`EntityLifecycleInterceptor` bunları `SaveChanges` anında zorlar ve **sessizce geçmez,
istisna atar**:

| Arayüz | Davranış |
|---|---|
| `IImmutableEntity` | Güncelleme **ve** silme istisna atar |
| `IActivatableEntity` | **Silme istisna atar** — `IsActive = false` ile pasifleştirilir |
| `ISoftDeletableEntity` | Fiziksel silme `IsDeleted = true`'ya çevrilir |
| `IAuditableEntity` | `CreatedBy` / `UpdatedBy` / tarih alanları interceptor'la doldurulur |
| `IProjectEntity` | Yukarıdakilerin hepsinden muaf |

`IsActive` üzerinde **global query filter yoktur** — aktif/pasif kontrolü okuma
çağrılarında yapılır, böylece pasif kayıtlar görünür ve geri alınabilir kalır.
Soft-delete'li tablolarda (`Pin`, `IoChannel`, `Connection`, `DeviceCommand`) `IsDeleted`
için query filter **vardır**.

### Seed verisi

`AppDbContext.SeedData` deterministik veri basar (ID'ler sabittir; rastgele üretim EF'in
her derlemede yeni migration istemesine yol açar):

- `System` şirketi, `Owner` / `Admin` / `Manager` / `User` rolleri
- 5 `DeviceStatus` (renk + ikon ile), 12 `DeviceType`, 10 `Permission`
- Admin'e tüm izinler
- **Admin kullanıcısı: `admin` / `Admin!2345` — ilk girişten sonra değiştirin.**
- 10 sistem şablonu + 66 pin (kontrol modülü, 8 kanal giriş/röle/LED kartı, 4 kanal analog
  giriş kartı, klemens, güç kaynağı, şebeke girişi, sigorta, 3 telli sensör) — palet boş
  açılmasın diye.

---

## 5. Bugün ne çalışıyor?

### 5.1 Kimlik ve yetki

`POST /api/Account/Login | SignUp | RefreshAuth | Logout | RevokeAll`

- Identity + JWT. Access token 24 saat, refresh token 7 gün (`TokenSettings`).
- `Logout` ve `RevokeAll` kullanıcı kimliğini **gövdeden değil, token'dan** okur.
- Login yanıtı ve token, kullanıcının rollerinden hesaplanan `permission` claim'lerini taşır.
- **Ama hiçbir uç bu claim'i okumaz** — bkz. § 6.

`TokenSettings:SecurityKey` yoksa uygulama açılışta istisna atar (dev anahtarı
`appsettings.json` içinde duruyor).

### 5.2 Diyagram editörü (çekirdek özellik)

Uçlar **ekran başına** tasarlanmıştır, entity başına değil; her uç, gerçekten yazdığı
aggregate'in controller'ında durur.

| Uç | Not |
|---|---|
| `GET /api/Diagram/cabinet/{id}` | Tüm aggregate: kabin, cihazlar (+şablon, pinler, kanallar), bağlantılar, notlar, tuval ayarı. **Palet yok, canlı değer yok.** |
| `POST /api/Diagram/cabinet/{id}/save` | Delta — yalnızca üç aile: `devices`, `connections`, `diagramAnnotations`. Tek transaction. |
| `GET /api/ComponentTemplate/palette` | Stencil kütüphanesi; kabinden bağımsız olduğu için ayrı cache anahtarı hak eder |
| `POST /api/ComponentTemplate` | Şablon + pin şeması tek transaction'da (pinsiz şablon kullanılamaz) |
| `POST /api/ComponentTemplate/image` | Multipart; dosya adı sunucuda üretilir, `wwwroot/uploads/templates` altına yazılır (png/jpg/webp/svg, ≤4 MB) |
| `PUT /api/CanvasSettings/cabinet/{cabinetId}` | Upsert; `cabinetId` **route'tan** gelir. Grid boyutu değiştirmek bir diyagram düzenlemesi değildir |

**Kaydetme sözleşmesi:**

- Gövde `{ upserted: [...], deleted: [ids] }` şeklindedir. **`created` / `updated` ayrımı
  yoktur:** her taslak bir `Guid` taşır, **Guid'i istemci üretir**, sunucu kimliği arar —
  bulursa günceller, bulamazsa ekler.
- Aynı kaydın hem `upserted` hem `deleted` içinde olması doğrulamada **400**'dür.
- Cihazın pin *içeriği* burada yazılmaz: sunucu pinleri her zaman şablonun
  `ComponentTemplatePin` satırlarından üretir. İstemciden gelen tek şey **kimliklerdir**:
  `devices.upserted[].pins = [{ id, componentTemplatePinId }]` ve
  `.ioChannels = [{ id, direction, channelNumber }]`. Kanal adresi bir **çifttir**
  (`direction` + `channelNumber`), çünkü kartta `IN1` ile `OUT1` ayrı noktalardır. Bu sayede
  yeni bir cihaz **kaydedilmeden önce kablolanabilir**: bir kablo ucu aynı gönderide doğacak
  bir pini gösterebilir.
- Pin ve kanal kimlikleri **salt-oluşturmadır**. Mevcut bir cihaza `pins` ya da `ioChannels`
  göndermek `400`'dür; şablon değiştirmek de öyle ("silip yeniden ekleyin"). Gönderilen küme
  şablonun şemasıyla **birebir örtüşmek zorundadır** — eksik göndermek kopuk pinli cihaz,
  fazla göndermek şablon dışı uydurma pin üretirdi.
- **Kanal adresleri kabin genelinde benzersizdir**, cihaz genelinde değil
  (`IX_IoChannel_CabinetId_Direction_ChannelNumber`). Kabin *bir* kontrol kartıdır ve kartın
  adres uzayı düzdür. Aynı şablonu ikinci kez bırakmak bu yüzden `400` verir; hata mesajı
  adresi işgal eden cihazın **adını** da söyler.
- **Bir cihazı silmek kanallarını serbest bırakmaz.** Cihaz `IsActive = false` olur, pinleri
  ve kabloları düşer, ama `IoChannel` satırları yerinde kalır — dolayısıyla `IN1` işgal
  edilmeye devam eder. `ExternalCode` ise serbest kalır (index filtresi `IsActive = 1`).
- Karşılığı bulunamayan `deleted` kimlikleri **sessizce atlanır** ve sayılmaz — istemciyi
  "bu kayıt sunucuya gitti mi" bilgisini taşımaktan kurtaran karar budur. Buna karşılık
  `upserted`'da başka kabine ait ya da silinmiş/pasif bir kimlik `400`'dür: soft-delete
  edilmiş satır birincil anahtar uzayını işgal etmeye devam ettiği için sessizce INSERT'e
  düşmesi PK ihlaliyle 500 üretirdi.
- Kablo uçları salt-oluşturmadır ve çift kontrolü **yönsüzdür**: DB'deki unique index
  `(SourcePinId, TargetPinId)` sıralı olduğu için ters çizilmiş aynı kabloyu yakalamaz,
  servis daha katıdır. Ayrıca iki ucun gerilim seviyesi **ikisi de belirtilmişse** ve
  farklıysa bağlantı reddedilir; biri `null` ise susulur.
- `DiagramService.SaveAsync`, repository'nin `*AndSave*` kısayollarını **bilerek kullanmaz**:
  `BeginTransactionAsync` → kaydetmeyen overload'lar → commit. Aksi halde tek kaydetmede
  ~8 commit oluşurdu. Bu, projedeki tek istisnadır; sıradan CRUD servisleri `*AndSave*` kullanır.
  Transaction içinde **iki `SaveChangesAsync`** vardır ve bu bilinçlidir: önce silmeler, sonra
  yazmalar. Sebep `IX_Connection_SourcePinId_TargetPinId`'in `WHERE IsDeleted = 0` filtresi —
  kullanıcı bir kabloyu silip aynı iki pin arasına yenisini çizdiğinde silme bir UPDATE,
  oluşturma bir INSERT'tür; tek batch'te EF'in sırası garanti değildir ve INSERT önce giderse
  index ihlali 500 döner.
- Referans doğrulamaları transaction **açılmadan önce** yapılır: açıp geri almak yerine hiç
  açmamak kilit süresini de log gürültüsünü de azaltır.

**Diyagram tablolarına tek yazım yolu:** `Device`, `Connection`, `DiagramAnnotation`,
`Pin`, `IoChannel`, `ComponentTemplatePin` yalnızca `DiagramController` deltası üzerinden;
`ComponentTemplate` yalnızca `POST /api/ComponentTemplate`; `CanvasSettings` yalnızca
`PUT /api/CanvasSettings/cabinet/{id}` üzerinden yazılır. Bu tabloların generic
controller'ları **salt okunurdur** (`GET {id}`, `GET {id}/base|detail`, `POST list*`).
Buraya generic Create/Update/Delete geri eklemeyin; ihtiyaç varsa sahibinin
controller'ına ekran bazlı bir uç ekleyin.

### 5.3 SCADA entegrasyonu

Kuyruk yok, retry yok — tekrarlanan bir röle darbesi, başarısız bir komuttan daha kötüdür.

**Ingest (SCADA → biz):** `POST /api/Scada/ingest`, `[AllowAnonymous]`, kendi rate-limit
politikası ile.

```json
{ "cabinetId": "...", "type": "I", "channelNumber": 7, "value": "1", "timestampUtc": null }
```

- **İstek başına tek okuma** taşınır (toplu gönderim yoktur).
- Adres bir **çifttir**: `type` + `channelNumber`. `ScadaPinAddress.TryParseType` yalnızca iki
  başlık tanır — `"I"` dijital giriş (`PinDirection.Input`), `"A"` analog giriş
  (`PinDirection.AnalogInput`). Büyük/küçük harf duyarsızdır.
- **Çıkış başlığı (`"O"`) bilerek yoktur**: çıkıştan telemetri gelmez, sürdüğümüz rölenin
  yankısı zaten `DeviceCommand`'da durur. Geçersiz başlık `400` döner.
- `IN<n>` / `OUT<n>` / `AI<n>` **metinsel** adresi yalnızca **giden** komut gövdesinde
  kullanılır (`ScadaPinAddress.Format`); ingest'te böyle bir string ayrıştırması yoktur.
  LED için ayrı önek yoktur — LED `OUT17..OUT24`'tür.
- Kabin pasifse veya `ScadaIsEnabled` kapalıysa istek reddedilir.
- Tanımsız/devre dışı kanal veya bulunamayan cihaz **isteği düşürmez**: bir `Warning`
  satırı yazılır ve boş **200** döner.
- Değişmeyen kanal değeri yazılmaz ve yayınlanmaz. Değer değiştiyse `IoChannel` güncellenir ve
  kanal **giriş yönündeyse** (`Input` ya da `AnalogInput`) bir `ChannelEvent` satırı eklenir.
- Cihaz `LastSeen` her ingest'te güncellenir; cihaz Offline'dan Online'a çekilir (Warning /
  Critical / Maintenance durumları **korunur**, ingest bunları silmez). Cihaz durumu
  değiştiyse kabinin toplu durumu yeniden hesaplanır.
- Sonuç olarak SignalR'a `ChannelValuesChanged`, `DeviceStatusChanged` ve
  `CabinetStatusChanged` yayınlanır.

**Komut (biz → SCADA):** `POST /api/Device/{deviceId}/command`, geçmiş için
`GET /api/Device/{deviceId}/commands`.

- `ScadaCommandGateway`, `Timeout.InfiniteTimeSpan` ile tanımlı adlandırılmış bir
  `HttpClient` kullanır ve zaman aşımını kendi `CancellationTokenSource`'uyla uygular —
  `NoResponse`'u `Failed`'dan ayırt edilebilir kılan şey budur.
- Eşleme: 2xx → `Succeeded`, 4xx/5xx → `Failed` (gövde `ResultMessage`'a), zaman aşımı /
  bağlantı hatası → `NoResponse`. **Resilience/retry handler'ı bilerek kayıtlı değildir.**
- Gövde `{cabinetId, commandId, pin, commandType, value, issuedAtUtc}` olarak
  `{ScadaBaseUrl}/command` adresine gider. `commandId` SCADA tarafında tekrar tespiti içindir.
- Bu aşamada tek komut türü vardır: `SetOutput = 1`.

**Canlılık:** Push-only bir sistemde sessizliği yalnızca zaman tespit edebilir.
`OfflineDeviceChecker` hosted service'i `Scada:StaleAfterSeconds`'ı aşan cihazları Offline'a
çeker, kabin durumunu yeniden hesaplar ve değişiklikleri yayınlar.

**SignalR:** `/hubs/diagram`, grup `cabinet:{cabinetId}`, istemci `Subscribe(cabinetId)` /
`Unsubscribe(cabinetId)` çağırır. Olaylar: `ChannelValuesChanged`, `DeviceStatusChanged`,
`CabinetStatusChanged`, `CommandCompleted`. Hub, REST ile **aynı** JSON ayarlarını kullanır
(`ProjectJsonOptions`) ki tip aynaları tutsun. İş katmanı `IDiagramNotifier` üzerinden
yayınlar; SignalR implementasyonu WebAPI'dedir.

**Kanal olayları (`ChannelEvent`):** Okuma yolu `POST /api/ChannelEvent/list` (sayfalı;
kabin + kanal + tarih aralığı, `OccurredAtUtc`'ye göre). Yazım yolu **yoktur** — satırları
yalnızca ingest üretir.

Bir satır **üç koşul birden sağlanınca** yazılır: değer gerçekten değişti **ve** kanal giriş
yönlü (`Input` ya da `AnalogInput`) **ve** yeni değer `null` değil. Satır `Value`,
`PreviousValue`, `OccurredAtUtc` (sahada gerçekleştiği an) ve `ReceivedAtUtc` (bize ulaştığı
an) taşır.

Yazılmayan üç durum ve gerekçeleri:

- **Aynı değerin tekrarı.** SCADA saniyede bir `IN7 = 1` gönderirse bir saat sonra tabloda
  3600 değil **1** satır olur.
- **Çıkış kanalları.** Ingest zaten `"O"` başlığını tanımaz; sürdüğümüz rölenin kaydı
  `DeviceCommand`'dadır.
- **`null`'a düşen okuma.** "Kanal var ama okunamadı" durumunda `IoChannel.CurrentValue`
  null'a çekilir, ama `ChannelEvent.Value` non-nullable olduğu için olay satırı yazılmaz —
  yani okuma kesintisi olay geçmişinde görünmez, yalnızca anlık değerde.

Anlık değer bu tabloda değil, `IoChannel.CurrentValue` + `ValueUpdatedAt` alanlarında durur
ve üzerine yazılır.

**Dijital kanalda bu bir değişim günlüğüdür; analog kanalda fiilen telemetri tablosudur.**
0/1 kanalda satır yalnızca durum değişince doğar — durum değişimi zaten olayın kendisidir ve
tablo yavaş büyür. Analog bir kanalda (sıcaklık, akım) ise değer neredeyse her ingest'te
değişir, dolayısıyla **her ingest bir satır üretir**.

Bu bilinçli bir karardır (2026-09-10): analog geçmişi ayrı bir `TelemetryRecord` tablosu
yerine aynı tabloda tutuluyor. **Bedeli:** `ChannelEvent`'in büyümesini sınırlayan tek şey
artık saklama/temizlik işidir ve o iş **henüz yazılmadı** — bkz. § 7 ve yol haritası (j).
Analog kanal kullanan bir kurulumda bu iş yazılana kadar tablo sınırsız büyür.

### 5.4 İzleme — kameralar

Kameralar `Device` **değildir**: pinleri yoktur, diyagramda durmazlar, verileri SCADA'dan
gelmez — bu platform onları kendisi yoklar.

- **Tek bir `MonitoredDevice` tablosu bilerek yoktur.** Her izlenen tip kendi tablosunu alır;
  bugün yalnızca `Camera`. Ortak alanları `IMonitoredAsset` *arayüzü* garanti eder.
  TPH kalıtımı, EF'in her şeyi tek tabloya toplaması yüzünden reddedildi. SNMP kapsam dışıdır.
- **Hiçbir şey hard-code değildir.** Portlar (554/80), stream kanalları (101/102) ve snapshot
  kanalı DTO varsayılanlarıdır. RTSP URL'i ve ISAPI yolu **asla kolon değildir** — tam URL
  saklamak `IpAddress`/port bilgisini bir string içinde ikinci kez tutmak olurdu.
- **Tarayıcı RTSP görmez.** Zincir `Kamera → MediaMTX → Tarayıcı` (WHEP); medya ASP.NET'ten
  geçmez. Tek istisna snapshot JPEG proxy'sidir.
- **MediaMTX uygulamanın dışında çalışır**; API onu başlatmaz, yalnızca `127.0.0.1:9997`
  üzerindeki Control API ile konuşur.
- **Yayın biletleri yola bağlıdır**, kısa ömürlüdür (60 sn) ve TTL içinde çok kullanımlıktır —
  MediaMTX oturum ortasında hook'u yeniden çağırabilir. `POST /api/MediaGateway/auth`
  yalnızca `read` eylemini kabul eder, gövdesiz `200`/`401` döner (MediaMTX yalnızca duruma bakar).
- **Kapalı bir profil sessizce diğerine düşmez.** `CreateStreamTokenAsync`, istenen profilin
  `MainStreamEnabled` / `SubStreamEnabled` bayrağı kapalıysa bilet üretmez; `400` ile
  "bu kamerada kapalı" der. Geri düşüş olsaydı 12 kutucuklu bir ızgara kamera başına
  ~4 Mbps'ye çıkabilirdi. Hangi ekranın hangi profili isteyeceği bir **istemci kararıdır** ve
  `Scadex.WebUI` bunu beklendiği gibi uyguluyor: ızgara kutucuğu `StreamProfile.Sub`
  (`components/camera/camera-tile.tsx`), detay ekranı `StreamProfile.Main`
  (`views/app/cameras/detail.tsx`). Profil `stream-ticket` çağrısında query string'den gider;
  kapalı profil `hooks/use-camera-stream.ts` içinde istek atılmadan yakalanır.
- **`SnapshotLocks` `static`'tir ve öyle kalmalıdır.** Servis scoped'dur; alan örnek bazlı
  olsaydı her istek kendi kilidini alır ve sürü koruması hiçbir işe yaramazdı. Snapshot'lar
  ayrıca `IDistributedCache` üzerinde birkaç saniye tutulur.
- **Klipler yalnızca ileriye dönüktür**; olay öncesi görüntü kapsanmaz — bu, sürekli kayıt
  tamponu isterdi ve "7/24 kayıt değiliz" gerekçesiyle çelişirdi. `ClipCaptureWorker` hosted
  service'i HTTP isteğini klip süresi kadar bekletmemek için vardır. **Bugün seri çalışır:**
  kuyruktaki ikinci çekim, birincinin `Task.Delay(klip süresi)`'si bitene kadar başlamaz.
  Paralelleştirme sırada — bkz. § 7 yol haritası (g).
- **MediaMTX yolları düzenli olarak temizlenmez.** Klip yolları (`clip_{captureId}`) çekim
  akışının `finally` bloğunda düşürülür. Canlı izleme yolları (`cam_{id}_{profile}`) yalnızca
  **kameranın kendisi değiştiğinde** silinir: `CameraService.UpdateAsync`, bağlantıyı etkileyen
  bir alan (IP, RTSP portu, kullanıcı adı, parola, stream kanalları) değiştiğinde ya da kamera
  pasife alındığında iki profilin yolunu da düşürür — aksi halde MediaMTX eski bilgilerle
  bağlanmaya çalışıp zaman aşımına düşerdi. Ama **hiç dokunulmayan bir kameranın yolu sonsuza
  kadar kalır**, izleyicisi olmasa bile. Günlük temizlik işi sırada; bkz. § 7 yol haritası (f).
- Çekim dosyaları `wwwroot/uploads/captures/…` altına düşer ve bu yüzden **kimlik doğrulaması
  olmadan** servis edilir; tek koruma tahmin edilemez `Guid` dosya adıdır.
- **Kamera parolası düz metin saklanır ve okuma DTO'sunda düz metin döner** (kapalı ağ; bu
  aşamada gizlenmesi istenmedi). Bilinçli olarak bir koruyucu/şifreleme katmanı yoktur.
  `PUT`'ta parola üç durumludur: `null` = dokunma, `""` = temizle, dolu = değiştir.
- **`VideoCodec` yoktur.** Sahada yalnızca H.264 kullanılıyor ve transcoding yok; codec bir
  veri değil, bir kurulum varsayımıdır.

Uçlar: `GET|POST|PUT /api/Camera`, `GET /api/Camera/cabinet/{cabinetId}`,
`POST /{id}/stream-ticket`, `GET /{id}/snapshot`, `POST /{id}/capture`, `GET /{id}/captures`.

**Yoklamanın HTTP yüzeyi yoktur.** Kameranın ayakta olup olmadığını `MonitoredAssetProbeWorker`
yazar (§ 7 (d)); istemci durumu `CameraDto.deviceStatusId` üzerinden okur.

### 5.5 Generic CRUD tarafı

`Cabinet`, `Company`, `User`, `Role`, `RolePermission`, `DeviceCommand` controller'ları tam
CRUD + `selectlist` / `pagination` / `datatable/{client,server}` uçlarını taşır.
`DeviceStatus`, `DeviceType`, `Permission` lookup'ları salt okunurdur.
`Pin`, `IoChannel`, `Connection`, `DiagramAnnotation`, `ComponentTemplatePin` § 5.2'deki
gerekçeyle salt okunurdur.

---

## 6. API sözleşmesi kuralları

- **Yanıt zarfı yoktur.** `Result<T>.Success(data)` → `200` + çıplak DTO;
  `Result.Success()` → boş `200`. Create uçları `{ "id": "…" }` döner.
- **Hatalar RFC 7807 `ProblemDetails`'tir.** `ErrorType` → HTTP: `Failure` → 500,
  `NotFound` → 404, `Validation` → 400, `Forbidden` → 403. Gövdedeki `code` uzantısı **HTTP
  kodu değildir**; istemci `status`'a bakmalıdır.
- **Enum'lar sayı olarak serileştirilir** ve boşlukludur: `PinFunction.General = 99`,
  `DeviceType` 1..12 (0 yok), `DeviceCommandType` {1}, `CommandStatus` 1..4 (0 yok),
  `StreamProfile` {1,2}, `CaptureType` {1,2}, `CaptureStatus` 1..3. (`JsonStringEnumConverter`
  bilerek kayıtlı değil.)
- **Alan adları camelCase, ama sözlük anahtarları dönüştürülmez** — `DictionaryKeyPolicy`
  bilinçli olarak `null`, bu yüzden `ProblemDetails.errors`'ın anahtarları FluentValidation'dan
  geldiği gibi **PascalCase** kalır (`"Devices.Upserted[0].Pins"`). İstemci tarafında
  telafi edilir; iki taraftan birini "düzeltmeyin".
- **`null` alanlar gövdede kalır** (`DefaultIgnoreCondition = Never`), yani bir alanın yokluğu
  ile `null` olması istemci için ayırt edilebilir.
- Gelen gövde PascalCase de olsa bağlanır (`PropertyNameCaseInsensitive`), sayılar string'ten
  de okunabilir (`"1"` → `1`).
- **409 / optimistic concurrency / `rowVersion` yoktur** — tek operatörlü sistem, son yazan kazanır.
- **Hiçbir servis metodu `companyId` almaz ve hiçbir yerde `IgnoreQueryFilters` çağrılmaz** —
  çok kiracılılık sonradan tek bir global query filter olarak, imza değiştirmeden gelebilsin diye.
- Rate limit politikaları controller'lara **açıkça** `[EnableRateLimiting]` ile takılır:
  `Default` (50/10 sn, kullanıcı ya da IP bazlı), `Scada` (300/5 sn, IP bazlı — tüm kabinler
  SCADA'nın tek IP'sini paylaştığı için varsayılan sınır boğardı), `MediaGateway` (300/5 sn).
  **Bir attribute'ta adı geçen politika `Program.cs`'te tanımlı değilse middleware istisna
  atar ve o uç HER istekte 500 döner** — politika adı silmeden/yeniden adlandırmadan önce
  `RateLimiterKey`'e bakın.
- `AddJwtBearer`'ın `OnMessageReceived`'ı token'ı query string'den okur, ama **yalnızca
  `/hubs` yolları için** (WebSocket el sıkışması Authorization header taşıyamaz). Bunu
  normal uçlara genişletmeyin.

---

## 7. Şu anki durum — dürüst envanter

### Kapatılanlar

Bu blokta duran dört acil madde **kapandı** (bkz. aşağıdaki "kabul edilmiş risk" istisnası):

1. ~~Çözüm derlenmiyor~~ — `Program.cs`'e eksik iki `using` eklendi
   (`Scadex.WebAPI.BackgroundServices`, `Scadex.WebAPI.Hubs`); `dotnet build` yeşil.
2. ~~`Migrations/` klasörü yok~~ — `InitialCreate` üretildi ve uygulandı. Doğrulandı: `Pin` ve
   `ComponentTemplatePin` için `RelativeX/Y` 0..1 `CHECK`'leri, `CK_Connection_DistinctPins`,
   `IX_IoChannel_CabinetId_Direction_ChannelNumber` ve `IX_Connection_SourcePinId_TargetPinId`
   (`WHERE IsDeleted = 0`), `IX_Device_CabinetId_ExternalCode`
   (`WHERE ExternalCode IS NOT NULL AND IsActive = 1`) migration'da yerinde. Seed indi:
   5 DeviceStatus, 12 DeviceType, 10 Permission, **10 şablon + 66 pin**, 4 rol, admin, 1 şirket.
3. ~~Port tutarsızlıkları~~ — **kanonik adres `http://localhost:5208`**. `mediamtx.yml`
   (`authHTTPAddress`), `Scadex.WebUI/.env.development`, `.env.production` ve
   `axios-helper.ts`'teki yedek değer bu adreste birleştirildi. (`src/lib/diagram/template-image.ts`
   içindeki yorum hâlâ eski `:7042`'yi anıyor — yalnızca yorum, davranış etkilenmiyor.)

**Kabul edilmiş risk — AutoMapper 14.0.0.** NuGet `NU1903` (yüksek önem, GHSA-rvv3-g6hj-g44x)
uyarısı veriyor. **Yükseltilmeyecek:** AutoMapper 15 ticari lisans istiyor. Bu bir yapılacak iş
değil, bilinçli bir karardır; `dotnet build` çıktısındaki 5 uyarı bu yüzden beklenen gürültüdür.

### Bilinçli boşluklar (unutulmadı, sıraya alındı)

- **Yetki zorlanmıyor.** `permission` claim'i üretilip token'a konuyor, ama hiçbir
  `[Authorize(Policy = …)]` ya da handler onu okumuyor. `ControlOutput` dahil.
- ~~Yoklama servisi yok~~ — **yazıldı (2026-09-10)**, bkz. (d).
- **MediaMTX yolları birikiyor.** Canlı izleme yolları yalnızca kamera güncellendiğinde ya da
  pasife alındığında düşüyor; hiç dokunulmayan bir kameranınki kalıcı. Düzenli temizlik işi
  yok. Bkz. (f).
- **Ayarlar uzaktan düzenlenemiyor.** `MediaGatewaySettings` ve `CameraCaptureSettings`
  yalnızca `appsettings.json`'da; değiştirmek dosya erişimi + yeniden başlatma istiyor.
  Bkz. (e).
- **Kiracı izolasyonu yok** (§ 6'daki gerekçeyle sonradan tek noktadan gelecek).
- **Ingest yalnızca `cabinetId` ile "kimlik doğrular"** — paylaşılan sır / imza yok.
- **Ayrı bir `TelemetryRecord` tablosu yok — bilinçli.** 2026-09-10'da analog kanalların
  geçmişinin de `ChannelEvent`'te tutulmasına karar verildi (`ChannelEventService`'teki koşul
  `Input` **ve** `AnalogInput`'u kapsıyor). Analog kanalda tablo artık bir zaman serisi gibi
  davranır; dijital kanalda değişim günlüğü olmayı sürdürür. "Örnekleme kesintisiz miydi"
  sorusu hâlâ cevaplanamaz — `null`'a düşen okuma satır üretmez (§ 5.3).
- **`ChannelEvent` için saklama/temizlik işi yok — artık ACİL.** Yazan tek yol ingest, silen
  hiçbir yol yok. Dijital kanallarda tablo yavaş büyür, ama **2026-09-10'da analog kanallar da
  olay üretmeye başladı** (aşağıya bakın): analogda değer neredeyse her ingest'te değiştiği
  için her ingest bir satır demek. Analog kart kullanan bir kurulumda büyümeyi sınırlayan
  hiçbir şey yok. Bkz. yol haritası (j).
- **ÖLÇÜLDÜ (2026-09-10) — silinmiş kanalın olay satırları listeden tamamen düşüyor.**
  Repository yorumunun iddia ettiği gibi "türev alanlar null gelir" **değil**: satır hiç
  gelmiyor. `ChannelEvent.IoChannelId` non-nullable olduğu için `ProjectTo` INNER JOIN üretiyor
  ve `IoChannel`'ın soft-delete filtresi satırı boşa düşürüyor. **Yan bulgu:** sayfalama sayacı
  join'siz hesaplandığından `dataCount` dolu kalıyor — istemci "3 kayıt" görüp **boş tablo**
  çiziyor. Yorum ölçüm sonucuyla düzeltildi (`ChannelEventRepository.cs`).
  **Bugün ulaşılamaz durumdur:** hiçbir yazım yolu `IoChannel` silmiyor — cihaz silmek
  kanalları yerinde bırakıyor (§ 5.2). Bu yüzden kod yeniden yapılandırılmadı; kanal silen bir
  yol eklenirse olay geçmişi sessizce kaybolacağı için o gün birlikte ele alınmalı.
- **Çekim saklama temizliği yok** — `ExpiresAt` yazılıyor ama kimse okumuyor.
- `DeviceCommandController` generic Create/Update/Delete/Restore uçlarını taşıyor; bu,
  § 5.2'deki "tek yazım yolu" ilkesiyle gözden geçirilmesi gereken bir istisna.
- **Test paketi yok.** Otomatik kontroller yalnızca `dotnet build` ve frontend tarafında
  `typecheck` / `lint` / `build`. Davranış, uygulamayı çalıştırarak doğrulanır.
- **`docs/api-contract/` karşılığı bu depoda henüz yok.** Sözleşme dokümanı taşınana kadar
  tek doğruluk kaynağı DTO'ların kendisidir.

> **Ölçüm nasıl yapıldı.** Cihaz + giriş kanalı oluşturuldu, `POST /api/Scada/ingest` ile
> `IN7` üç kez değiştirilerek 3 olay satırı üretildi ve listelendi. Ardından cihaz diyagram
> deltasıyla silindi: `Device.IsActive = 0`, 10 pin `IsDeleted = 1`, **ama `IoChannel.IsDeleted`
> hâlâ 0** — yani "cihazı silmek kanalı silmez" (§ 5.2) kuralı yüzünden liste hiç bozulmadı.
> Senaryoyu gerçekten kurmak için kanal doğrudan SQL ile `IsDeleted = 1` yapıldı; ancak o zaman
> liste boş döndü. Kanal geri alındı.

### Yol haritası — sırada ne var

**(a)** ~~Derleme hatalarını kapat, ilk migration'ı üret, veritabanını ayağa kaldır.~~
**TAMAMLANDI (2026-09-10).** Duman testi geçti: login → `permission` claim'leri, palet (10
şablon), kabin oluşturma, diyagram delta kaydı (10 pin + 8 kanal şablondan türetildi) ve
`POST /api/Scada/ingest` → `ChannelEvent` üretimi uçtan uca çalışıyor.

**(b)** İzin zorlaması: `permission` claim'ini okuyan policy/handler + kritik uçların
işaretlenmesi (özellikle `ControlOutput` ve diyagram kaydetme).

**(c) `Scadex.WebUI` — büyük kısmı yazıldı.** Diyagram editörü (React Flow; palet, ortogonal
kablo, hizalama, özellik paneli, kaydedilmemiş değişiklik koruması), SignalR üzerinden canlı
değer katmanı (`lib/diagram/live-store.ts` + `hooks/use-diagram-live.ts`), kamera ızgarası ve
WHEP oynatıcı (`lib/camera/`), auth akışı ve admin ekranları (şirket, kamera, şablon) ayakta.

Kalanlar: **olay listesi ekranı** — `api/channel-event.ts` ve `hooks/use-channel-events.ts`
yazılı ama hiçbir görünüm bunları tüketmiyor; kullanıcı/rol/izin yönetimi ekranları;
`.env.development`'taki API adresinin düzeltilmesi (bkz. § 8).

**(d)** ~~`IMonitoredAsset` yoklama background servisi.~~ **TAMAMLANDI (2026-09-10).**

`MonitoredAssetProbeWorker` (WebAPI/BackgroundServices) `OfflineDeviceChecker` kalıbını izler.
**Tipe özel değildir:** kayıtlı her `IMonitoredAssetProbeSource` üzerinden döner, tipleri hiç
bilmez. Yeni bir izlenen tip eklemek = yeni bir kaynak implementasyonu + tek satırlık DI kaydı;
worker değişmez. Bugünkü tek kaynak `CameraProbeSource`.

Alınan kararlar:

- **ICMP dalı yok** — sonda yalnızca TCP connect yapar. `MonitoringPort` null ise varlık
  atlanır ve bir `Warning` loglanır; `IMonitoredAsset.MonitoringPort` doc'u buna göre
  düzeltildi. `MonitoringPort ?? RtspPort` mapping'i **korundu**, dolayısıyla API üzerinden
  oluşturulan kamerada alan zaten hiç null olmuyor.
- **Periyot varlık başınadır** (`PingIntervalSec`); worker `Monitoring:SweepIntervalSeconds`
  (30 sn) aralıklarla tur atar ve yalnızca periyodu dolanları yoklar. Son yoklama anı
  **bellekte** tutulur — bunun için kolon yok ve `LastSeen` uygun değil, çünkü o yalnızca
  ulaşıldığında yazılıyor; ona bakılsaydı erişilemeyen varlık her turda yeniden yoklanırdı.
- Sonuç `CameraService.RecordProbeResultAsync`'in bugünkü politikasıyla yazılır: durum
  değişmedikçe yazma, `LastSeen` bilinçli istisna.
- **`POST /api/Camera/{id}/probe-result` kaldırıldı** — yoklama frontend'in tetiklediği bir
  akış değildir. Servis metodu kaldı, HTTP yüzeyi gitti; `CameraProbeResultDto` yerine
  tip-bağımsız `MonitoredAssetProbeResultDto` geçti. Frontend'deki `recordCameraProbeResult`
  ve TS aynası da silindi.
- **Yol boyunca bulunan hata:** eski 

Doğrulandı: ulaşılabilir hedef → `DeviceStatusId = 1 (Online)`, `LastSeen` dolu,
`LastConnectionError` null; kapalı port → `DeviceStatusId = 0 (Offline)`,
`LastConnectionError = "Baglanti hatasi (ConnectionRefused): …"`.

**(e) `MediaGatewaySettings` ve `CameraCaptureSettings` veritabanına taşınır.** Tip başına
**tek satırlık tablo**; API + ekran üzerinden uzaktan düzenlenebilir, önbellekli bir sağlayıcı
üzerinden okunur. `appsettings.json` bölümleri yalnızca ilk tohumlama / geri düşüş değeri olur.
İki tuzak:
   - `Program.cs`'te `MediaGateway:ApiBaseUrl` adlandırılmış `HttpClient`'ın `BaseAddress`'ine
     **açılışta pişiriliyor** — ayar uzaktan değişebilir olunca adres çağrı anında çözülmeli.
   - Aynı yerde `Timeout` **5 sn olarak hard-code**; `MediaGatewaySettings.ApiTimeoutMs`
     (varsayılan 30000) hiç kullanılmıyor. Taşıma sırasında bu tutarsızlık da kapatılmalı.
   - Bugün her iki ayar `ServiceRegistration.cs`'te `.Get<T>()` ile **singleton anlık
     görüntü** olarak kayıtlı ve tüketicilere somut tip olarak enjekte ediliyor; araya
     sağlayıcı soyutlaması girmesi gerekecek.

**(f) Günlük MediaMTX yol temizliği background servisi.** Her gün gün sonunda MediaMTX
üzerindeki yolları düşürür. **İstisnalar:** aktif izleyicisi (`readers`) olan yollar ve kayıt
(`record: true`) durumundaki yollar — devam eden klip çekimleri bu ikinci kuralla korunur.
`IMediaGateway`'e bir **yol listeleme** yeteneği eklenmesi gerekir; bugün yalnızca
`EnsureLivePathAsync` / `EnsureClipPathAsync` / `DeletePathAsync` var. MediaMTX tarafında
çalışma zamanı durumu `v3/paths/list`, yapılandırma `v3/config/paths/list` uçlarındadır.

**(g) Klip çekiminin paralelleştirilmesi.** Aynı kamerada da farklı kamerada da eşzamanlı klip
alınabilmeli. **İnceleme yapıldı: temp dosya okuma tarafı bunu zaten destekliyor** —
`ClipPathName(captureId)` = `clip_{captureId}` olduğu için geçici klasör
(`RecordRoot/clip_{captureId}`), `FindNewestClip` taraması, `TryDeleteTempDirectory` ve
`finally`'deki `DeletePathAsync`'in hepsi **capture bazlıdır**, kamera bazlı değil; çapraz
okuma ya da çapraz silme mümkün değil. Değişmesi gereken yalnızca iki yer:
   - `ClipCaptureWorker`'ın seri `await foreach` döngüsü (her çekim öncekinin
     `Task.Delay(klip süresi)`'sini bekliyor) — ayrıca sınıfın "**Sıralı çalışır**, paralel
     degil" XML doc'u güncellenmeli.
   - `ClipCaptureQueue`'daki `SingleReader = true` bayrağı — çok tüketiciye geçilirse
     kaldırılmalı.

   Karar: **sınırsız paralel**, eşzamanlılık sınırı yok. Risk: aynı kameradan eşzamanlı
   çekimler `sourceOnDemand: false` ile **ayrı ayrı RTSP oturumu** açar; kameranın eşzamanlı
   oturum limiti aşılırsa çekim "Medya geçidi klip dosyası üretmedi" ile düşer.

**(h)** Kart okuyucu ingest'i: `DeviceType.CardReader` için ayrı bir uç (`{ "cardId": "…" }`);
kart kimliği bir ölçüm olmadığı için `IoChannel`'a yazılmaz, `ChannelEvent` üretmez.

---

## 8. Çalıştırma

### Backend (`Scadex/` içinden)

```bash
dotnet build                                        # tüm çözüm
dotnet run --project Scadex.WebAPI                  # https://localhost:7167 (+ http://localhost:5208)
                                                    # OpenAPI: /openapi/v1.json, Scalar UI: /scalar

dotnet ef migrations add InitialCreate --project Scadex.DataAccess --startup-project Scadex.WebAPI
dotnet ef database update            --project Scadex.DataAccess --startup-project Scadex.WebAPI
```

Bağlantı dizesi `ConnectionStrings:Database` (varsayılan: `localhost` / `Scadex` /
Windows kimlik doğrulaması). JWT imza anahtarı yoksa uygulama açılışta durur:

```bash
dotnet user-secrets set "TokenSettings:SecurityKey" "<64+ karakter>" --project Scadex.WebAPI
```

MediaMTX ayrı bir süreç olarak çalıştırılır (`Scadex.WebAPI/mediamtx_v1.20.1/mediamtx.exe`).
Depodaki `mediamtx.yml` kısmen yamalı: `authMethod: http` ve `authHTTPExclude` altında
`api` / `metrics` / `pprof` eylemleri tanımlı. Bu muafiyet kaldırılırsa MediaMTX kendi Control
API'sini kilitler — yol açmaya çalışan her istek kimlik doğrulamaya, o da Control API'ye düşer.

> **`authHTTPAddress` artık doğru portu gösteriyor** (`http://127.0.0.1:5208/...`). Bu satır
> uzun süre CabinetOS'un `5263`'ünde kalmıştı ve MediaMTX auth hook'una hiç ulaşamadığı için
> canlı izleme çalışmıyordu. API'nin dinlediği portu değiştirirseniz **bu satırı da**
> güncelleyin — sessizce bozulan tek yer burasıdır.

### Frontend (`Scadex.WebUI/` içinden)

```bash
npm install
npm run dev      # :5173 — appsettings'teki Cors:Origins ile eşleşmeli
npm run build    # tsc -b && vite build — tip kontrolü bunun içindedir
npm run lint
```

> **Kanonik API adresi `http://localhost:5208`'dir.** `.env.development`, `.env.production` ve
> `src/lib/axios-helper.ts`'teki yedek değer bu adreste birleştirildi; bir zamanlar üçü ayrı
> yerlere (`5263`, `7042`) bakıyordu. Adresi değiştirirken üçünü birden güncelleyin —
> `.env` dosyaları git'te izlendiği için tek makineye özel değişiklik yapmayın.

---

## 9. AI agent'ları için notlar

- **Önce bu dosyayı, sonra dokunacağınız aggregate'in entity XML doc'larını okuyun.** Alan
  gerekçeleri oraya yazılmıştır; bir kolonu değiştirmeden önce neden var olduğunu okuyun.
- **Türkçe yazın.** Yorum, XML doc, hata mesajı, doküman — hepsi Türkçe.
- **`#region` ile bölmeyin, dosyaya bölün.** Mevcut partial sınıf düzenini izleyin.
  (Not: `Program.cs` ve bazı controller'lar bu kuralın dışında kalmış durumda.)
- **Bir tabloya ikinci yazım yolu açmayın.** İhtiyaç varsa sahibinin controller'ına ekran
  bazlı bir uç ekleyin.
- **Sözleşme DTO'sunu değiştirdiğinizde** hem C# DTO'sunu hem (varsa) TS aynasını hem de
  sözleşme dokümanını elle güncelleyin — hiçbir codegen ya da test bunu sizin için yakalamaz.
- **§ 7'deki "bilinçli boşluklar"ı sormadan "düzeltmeyin".** Bunlar unutulmuş değil,
  sıraya alınmış maddelerdir.
- Değişikliğin sonunda `dotnet build` yeşil olmalı; frontend'e dokunduysanız
  `npm run lint && npm run build` de yeşil olmalı. (`typecheck` diye ayrı bir script **yoktur**;
  tip kontrolünü `build` içindeki `tsc -b` yapar.)
