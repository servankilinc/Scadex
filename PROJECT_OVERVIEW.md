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
├─ Scadex.Signalization/    # Müşteri modülü: sinyalizasyon operatör işlemi takibi (§ 10)
│                           # — kendi context'i (signalization şeması), uçları, arka plan işleri
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
  edilmeye devam eder. `ExternalCode` ve `MacAddress` ise serbest kalır (index filtreleri
  `IsActive = 1`).
- **`macAddress` / `ipAddress` deltada taşınır (2026-09-10).** MAC, SCADA ingest'inin kabini
  çözdüğü adrestir (§ 5.3) ve operatörün girebilmesi gerekir; ikisi de cihaz özellik
  panelinden yazılır. `deviceStatusId` / `lastSeen` ise **taslakta yoktur** ve `WriteDevice`'ta
  dokunulmaz — onlar telemetriyle yazılır.
- **`macAddress` benzersizliği kabin geneli değil, SİSTEM GENELİDİR**
  (`IX_Device_MacAddress`, unique, `WHERE MacAddress IS NOT NULL AND IsActive = 1`). Bir
  fiziksel kartın tek MAC'i vardır ve ingest kabini bu adresten çözer; aynı adres iki kabinde
  olsaydı telemetri yanlış kabine yazılırdı. Bu yüzden çarpışma **başka bir kabindeki** cihazla
  da olabilir ve `LoadDeviceMacAddressesAsync` kabinle değil gönderilen adreslerle daraltılır.
  Çakışma `400` döner (`Devices.Upserted[i].MacAddress`) — `ExternalCode`'daki gibi, DB
  kısıtına çarpıp 500 üretmemek için.
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
{ "macAddress": "AA:BB:CC:DD:EE:FF", "type": "I", "channelNumber": 7, "value": "1", "timestampUtc": null }
```

- **Kabin `macAddress`'ten çözülür, gövdede `cabinetId` YOKTUR (2026-09-10).** Sahadaki SCADA
  bizim ürettiğimiz kabin `Guid`'ini bilemez; bildiğimiz ortak değer kartın MAC adresidir.
  Sunucu, gelen adresle **birebir eşleşen** (`Device.MacAddress == macAddress`), aktif ve
  şablonu `DeviceType.ControlModule` olan cihazı arar ve o cihazın `CabinetId`'sini kullanır.
  Karşılığı yoksa **404** döner. Karşılaştırma ham string karşılaştırmasıdır: adres veritabanına
  **nasıl kaydedildiyse SCADA da öyle göndermek zorundadır** (normalizasyon/ayraç toleransı yok).
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

**Kart okuma (SCADA → biz, 2026-09-11):** `POST /api/Scada/card`, ingest ile aynı controller
(`[AllowAnonymous]`, `Scada` rate-limit politikası).

```json
{ "macAddress": "AA:BB:CC:DD:EE:FF", "cardId": "04A2B9C1", "timestampUtc": null }
```

- Kart numarası bir **ölçüm değildir**: `IoChannel`'a yazılmaz, `ChannelEvent` üretmez; çekirdek
  **hiçbir satır yazmaz**. Okuyucu kimliği taşınmaz — kartın ne anlama geldiğine gözlemci karar verir.
- Kabin ingest ile **aynı** yoldan çözülür (`IDeviceRepository.GetCabinetIdByControlModuleMacAsync`;
  ingest'in 3. adımı da artık bunu kullanır). Bilinmeyen MAC 404, kabin pasif/SCADA kapalıysa red.
- Kart, **aktif** kullanıcının `User.IdentityCardId`'siyle ham string olarak eşlenir. Tanımsız kart
  SCADA için hata değildir (200) — kullanıcısız iletilir; güvenlik incelemesi ham kimliği ister.
- `User.IdentityCardId`: aktif kullanıcılar arasında tekil (`IX_User_IdentityCardId`, filtre
  `IdentityCardId IS NOT NULL AND IsActive = 1`), pasif kullanıcının kartı devredilebilir. Servis
  `""`'ı `null`'a çeker ve çakışmayı DB kısıtından önce 400 ile yakalar (`errors.IdentityCardId`).
  Kullanıcı ekranında (`/admin/users`) yazılır.

**Gözlemci kancası — `IScadaEventObserver` (2026-09-11).** Çekirdek dışı modüllerin SCADA
olaylarını dinlediği tek nokta (Business/Utils/ScadaEvents). Ingest, değer **gerçekten
değişince** `OnChannelChangedAsync`, kart ucu `OnCardPresentedAsync` çağırır — ikisi de veri
yazıldıktan ve SignalR yayınından **sonra**. Kayıtlı gözlemci yoksa maliyet sıfırdır; bir
gözlemcinin istisnası loglanır ve isteği düşürmez. **Sözleşme:** gözlemci sıcak yoldadır, yalnızca
kendi kuyruğuna bırakıp döner. İçinde SCADA'ya komut göndermek yasaktır: SCADA kartı bizim
yanıtımızı beklerken aynı kartın web sunucusunu çağırmak tek iş parçacıklı firmware'de kilitlenmedir.

**Komut (biz → SCADA):** `POST /api/Device/{deviceId}/command`, geçmiş için
`GET /api/Device/{deviceId}/commands`.

- `ScadaCommandGateway`, `Timeout.InfiniteTimeSpan` ile tanımlı adlandırılmış bir
  `HttpClient` kullanır ve zaman aşımını kendi `CancellationTokenSource`'uyla uygular —
  `NoResponse`'u `Failed`'dan ayırt edilebilir kılan şey budur.
- Eşleme: 2xx → `Succeeded`, 4xx/5xx → `Failed` (gövde `ResultMessage`'a), zaman aşımı /
  bağlantı hatası → `NoResponse`. **Resilience/retry handler'ı bilerek kayıtlı değildir.**
- Komut, kartın kendi web sunucusuna **query string ile** gider — gövde yoktur:
  `GET {ScadaBaseUrl}/updat?output=<kanal>&state=<0|1>`. Yoldaki `/updat` yazımı firmware'de
  böyledir, `/update` değildir.
- Tele yalnızca **kanal numarası ve değer** çıkar. `ScadaCommandEnvelope`'un taşıdığı
  `cabinetId` (bir kabin = bir kart olduğu için adreste örtük), `commandId`, `commandType` ve
  `issuedAtUtc` karta gitmez; `DeviceCommand` satırında kayıt altında kalır. `commandId`'nin
  tekrar tespiti anlamı, araya böyle bir kontrol yapan bir SCADA katmanı girerse doğar —
  bugün ne o katman ne de retry vardır.
- Değerdeki NO/NC terslemesi `DeviceCommandService` içinde çözülür; geçit hiçbir tersleme
  yapmaz, `state` parametresine geleni olduğu gibi yazar.
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
artık saklama/temizlik işidir ve o iş **bilinçli olarak ertelenmiştir** — bkz. § 7.
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
  service'i HTTP isteğini klip süresi kadar bekletmemek için vardır. **Paralel çalışır**
  (2026-09-10): kuyruktan alınan her çekim kendi görevinde başlar, eşzamanlılık sınırı yoktur.
  Bkz. § 7 yol haritası (g).
- **MediaMTX yolları düzenli olarak temizlenmez.** Klip yolları (`clip_{captureId}`) çekim
  akışının `finally` bloğunda düşürülür. Canlı izleme yolları (`cam_{id}_{profile}`) yalnızca
  **kameranın kendisi değiştiğinde** silinir: `CameraService.UpdateAsync`, bağlantıyı etkileyen
  bir alan (IP, RTSP portu, kullanıcı adı, parola, stream kanalları) değiştiğinde ya da kamera
  pasife alındığında iki profilin yolunu da düşürür — aksi halde MediaMTX eski bilgilerle
  bağlanmaya çalışıp zaman aşımına düşerdi. Hiç dokunulmayan bir kameranın yolunu ise artık
  **`MediaPathCleanupWorker` gün sonunda düşürür**; izleyicisi olan ve kayıt yapan yollara
  dokunmaz (bkz. § 7 (f)).
- Çekim dosyaları `wwwroot/uploads/captures/…` altına düşer ve bu yüzden **kimlik doğrulaması
  olmadan** servis edilir; tek koruma tahmin edilemez `Guid` dosya adıdır. Süresi dolan
  dosyaları `CaptureRetentionWorker` siler; `CameraCapture` satırı kalır (§ 7).
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

**Kullanıcı / rol / izin yönetimi (2026-09-11).** Ekranlar `/admin/users` ve `/admin/roles`.
Silme yoktur (`IActivatableEntity`); pasife alma tek yoldur.

- Kullanıcının rolleri tek istekte eşitlenir: `PUT /api/UserRole/user/{userId}/sync` (gövde rol
  **adları**; `RolePermission` sync'inin aynası, tek transaction). Tanımsız ad ya da **yeni**
  eklenen pasif rol `400`'dür (`errors.roleNames`); zaten atanmış pasif rol listede kalabilir.
  Tekil `Assign` / `Remove` uçları duruyor.
- Kendi hesabını pasife almak `400`'dür (`errors.IsActive`) — login `IsActive`'e baktığı için
  yönetici kendini kilitlerdi. Pasife alınan kullanıcının refresh token'ları iptal edilir ve
  `RefreshAuth` pasif kullanıcıyı `403` ile reddeder.
- Pasif rol izin türetmez (`AuthService.GetPermissionCodesAsync` `IsActive` filtreler); rol
  claim'i yine yazılır.
- Sistem rolleri (`IsImmutable` — seed'deki dördünün hepsi) yeniden adlandırılamaz ve pasife
  alınamaz (`403`), ama **izinleri düzenlenebilir**; kilitlenseydi izin ekranı seed rolleri için
  işe yaramazdı.
- `UserDetailDto` artık `UserName`, `RoleDto` artık `IsImmutable` taşır. Parola sıfırlama ucu
  bilinçli olarak yoktur; parola yalnızca oluştururken verilir.

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
  `RateLimiterKey`'e bakın. **İstisna:** Sinyalizasyon modülünün uçlarına (§ 10) bilinçli olarak
  politika takılmaz (2026-09-11 kararı); `Program.cs`'te genel bir limit olmadığı için bu uçlar
  sınırsızdır, yalnızca `[Authorize]` arkasındadır.
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
  **Eklenirken dikkat (2026-09-11):** denetim **controller/policy katmanında** yapılmalı,
  `DeviceCommandService.SendAsync` içinde değil. Sinyalizasyon motoru (§ 10) kilit ve siren
  komutlarını HTTP kullanıcısı olmadan bu servisten gönderir; servis içi bir izin kontrolü onu
  bloke ederdi. Cihaz bazlı izin (kullanıcı şu cihaza komut gönderebilir) ile fiziksel geçiş
  yetkisi (operatör hangi kapıyı açabilir) farklı sorulardır ve çakışmaz.
- **Pasife alınan kullanıcının access token'ı süresi dolana kadar (24 sa) geçerlidir.** İstek
  başına `IsActive` kontrolü yok; pasife alma yalnızca yeni girişi ve `RefreshAuth`'u keser.
  Rol / izin değişikliği de aynı sebeple kullanıcının **bir sonraki** giriş ya da yenilemesinde
  token'a yansır (§ 5.5).
- ~~Yoklama servisi yok~~ — **yazıldı (2026-09-10)**, bkz. (d).
- ~~MediaMTX yolları birikiyor~~ — **günlük temizlik yazıldı (2026-09-10)**, bkz. (f).
  Kalan tek birikme kaynağı: kayıt bayrağı açık kalmış **artık klip yolları** (çekimi çökmüş
  olanlar) bilerek korunuyor, dolayısıyla temizlenmiyor.
- ~~Ayarlar uzaktan düzenlenemiyor~~ — **veritabanına taşındı ve ekranı yazıldı (2026-09-10)**,
  bkz. (e). `/admin/settings`; kaydedilen değer yeniden başlatma olmadan etkili.
- **Kiracı izolasyonu yok** (§ 6'daki gerekçeyle sonradan tek noktadan gelecek).
- **Ingest yalnızca `macAddress` ile "kimlik doğrular"** — paylaşılan sır / imza yok. MAC
  adresi taklit edilebilir bir değerdir; kimlik doğrulama değil, adresleme yapar.
- ~~`Device.MacAddress`'in yazım yolu yok~~ — **diyagram deltasına eklendi (2026-09-10)**,
  `IpAddress` ile birlikte; bkz. § 5.2. Kalan risk: `IX_Device_MacAddress` **global** olduğu
  için ileride gelecek **kiracı izolasyonu** (companyId global query filter) ile çakışır —
  ön doğrulama başka kiracının cihazını göremez hale gelirken DB kısıtı global kalır ve
  yinelenen MAC `400` yerine `500` üretir. O filtre eklendiğinde `LoadDeviceMacAddressesAsync`
  `IgnoreQueryFilters` ile çalışmalıdır (§ 6'daki "hiçbir yerde `IgnoreQueryFilters`
  çağrılmaz" kuralının bilinen tek istisnası bu olacak).
- **Ayrı bir `TelemetryRecord` tablosu yok — bilinçli.** 2026-09-10'da analog kanalların
  geçmişinin de `ChannelEvent`'te tutulmasına karar verildi (`ChannelEventService`'teki koşul
  `Input` **ve** `AnalogInput`'u kapsıyor). Analog kanalda tablo artık bir zaman serisi gibi
  davranır; dijital kanalda değişim günlüğü olmayı sürdürür. "Örnekleme kesintisiz miydi"
  sorusu hâlâ cevaplanamaz — `null`'a düşen okuma satır üretmez (§ 5.3).
- **`ChannelEvent` için saklama/temizlik işi YOK — bilinçli olarak ertelendi (2026-09-10).**
  Yazan tek yol ingest, silen hiçbir yol yok. Dijital kanallarda tablo yavaş büyür, ama aynı
  gün **analog kanallar da olay üretmeye başladı**: analogda değer neredeyse her ingest'te
  değiştiği için her ingest bir satır demek. Yani analog kart kullanan bir kurulumda tablonun
  büyümesini sınırlayan hiçbir şey yok ve **bu bilinerek kabul edildi** — saklama politikasını
  proje sahibi sonra kendisi ekleyecek. Sormadan bir silme işi yazmayın.
- **ÖLÇÜLDÜ (2026-09-10) — silinmiş kanalın olay satırları listeden tamamen düşüyor.**
  Repository yorumunun iddia ettiği gibi "türev alanlar null gelir" **değil**: satır hiç
  gelmiyor. `ChannelEvent.IoChannelId` non-nullable olduğu için `ProjectTo` INNER JOIN üretiyor
  ve `IoChannel`'ın soft-delete filtresi satırı boşa düşürüyor. **Yan bulgu:** sayfalama sayacı
  join'siz hesaplandığından `dataCount` dolu kalıyor — istemci "3 kayıt" görüp **boş tablo**
  çiziyor. Yorum ölçüm sonucuyla düzeltildi (`ChannelEventRepository.cs`).
  **Bugün ulaşılamaz durumdur:** hiçbir yazım yolu `IoChannel` silmiyor — cihaz silmek
  kanalları yerinde bırakıyor (§ 5.2). Bu yüzden kod yeniden yapılandırılmadı; kanal silen bir
  yol eklenirse olay geçmişi sessizce kaybolacağı için o gün birlikte ele alınmalı.
- ~~Çekim saklama temizliği yok~~ — **yazıldı (2026-09-10)**. `CaptureRetentionWorker` her gün
  `Jobs:CaptureRetentionSweepHour` saatinde (varsayılan 04:00) süresi dolmuş çekimlerin
  **dosyasını** siler. **Satır silinmez:** `RelativePath` null'lanır, çekimin yapıldığı bilgisi
  geçmişte kalır. Dosya silinemezse `RelativePath` korunur — kolonu null'lamak, diskte duran
  dosyayı bir daha bulunamaz hâle getirip kalıcı çöp bırakırdı.
  **Açık uç:** `Status` `Available` olarak kalıyor; süresi dolmuş bir çekim ancak
  `RelativePath == null` **ve** `ExpiresAt` geçmişte olmasından anlaşılıyor. Ayrı bir
  `CaptureStatus.Expired` değeri eklenmedi (sözleşme değişikliği olurdu).
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

~~Olay listesi ekranı~~ — **yazıldı (2026-09-10)**: `views/app/events/`, rota `/events`.
Kabin zorunlu (sunucu kabinsiz isteği 400'lüyor), kanal ve tarih aralığı isteğe bağlı, sayfa
boyutu 25'te sabit. Yazarken **iki mevcut hata** çıktı ve düzeltildi:

- `PaginationResponse.HasNext` bir eksik sayıyordu (`Page + 1 < PageCount`). 36 satır / 25 =
  2 sayfa kurulumunda 1. sayfada `false` dönüp **son sayfayı erişilemez** kılıyordu; artık
  `Page < PageCount`. Bugüne kadar hiçbir istemci bu alanı okumadığı için görünür etkisi yoktu.
- Damgalar `datetime2` sütunundan `DateTimeKind.Unspecified` olarak dönüyor, dolayısıyla JSON'da
  **`Z` soneki yok** (`"2026-09-10T09:06:56.4089716"`). Çıplak `new Date(...)` bunu yerel saat
  sayıp damgayı kaydırır; ekran eksikse `Z`'yi ekleyen bir yardımcı kullanıyor.

~~Ayar ekranı~~ — **yazıldı (2026-09-10)**: `views/admin/settings/`, rota `/admin/settings`.
İki ayrı form (medya geçidi, kamera çekimi) tek ekranda; sekme değil kart, çünkü uçlar,
tablolar ve önbellek anahtarları da ayrı ve bir grubu kaydetmek diğerine dokunmuyor.
Formlar sunucu verisiyle `useForm({ values, resetOptions: { keepDirtyValues: true } })` ile
eşitleniyor — `useEffect` + `reset` kalıbı bilerek kullanılmadı. Zod şemaları sunucudaki
`FluentValidation` kurallarının **elle tutulan kopyasıdır**; codegen yok, sunucudaki kural
değişirse şema da elle değişmeli.

~~Kullanıcı/rol/izin yönetimi ekranları~~ — **yazıldı (2026-09-11)**: `views/admin/users/`
(`/admin/users`) ve `views/admin/roles/` (`/admin/roles`); kurallar § 5.5'te. Kart ızgarası +
dialog kalıbı firma ekranıyla aynı, tek fark dialogların `useEffect` + `reset` yerine koşullu
mount + `key` ile tazelenmesi.

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
- **Yol boyunca bulunan hata:** eski `CameraProbeResultDtoValidator`, `Error` alanına koşulsuz
  bir `NotEmpty()` koyuyordu — yani **başarılı** bir yoklama (`Reachable = true`, `Error = null`)
  doğrulamadan hiçbir zaman geçemezdi. Yeni DTO'da kural `.When(v => !v.Reachable)` ile
  koşullandırıldı.

Doğrulandı: ulaşılabilir hedef → `DeviceStatusId = 1 (Online)`, `LastSeen` dolu,
`LastConnectionError` null; kapalı port → `DeviceStatusId = 0 (Offline)`,
`LastConnectionError = "Baglanti hatasi (ConnectionRefused): …"`.

**(e)** ~~`MediaGatewaySettings` ve `CameraCaptureSettings` veritabanına taşınır.~~
**TAMAMLANDI (2026-09-10).**

Tip başına **tek satırlık tablo** (`MediaGatewaySetting`, `CameraCaptureSetting`; `Id = 1`),
migration ile tohumlanır. Okuma yolu ayar nesnesi başına **ayrı bir servistir** —
`IMediaGatewaySettingService` ve `ICameraCaptureSettingService` — ve `ICacheService` ile
önbeleklenir; her yazma kendi anahtarını düşürür.

- **`appsettings.json` bu ayarları ARTIK HİÇ TAŞIMAZ.** `MediaGateway` ve `Cameras` bölümleri
  kaldırıldı; geri düşüş de okunmuyor. Satır bulunamazsa sınıf varsayılanlarına (seed ile aynı
  değerler) düşülür ve `Warning` loglanır. Buraya bölüm geri eklemek sessizce yok sayılır.
- **Adlandırılmış `HttpClient` yine `Program.cs`'te kurulur**, ama `BaseAddress` ve `Timeout`
  orada **verilmez**: `MediaMtxGateway` her `CreateClient` çağrısından sonra ikisini de
  ayardan uygular (`CreateConfiguredClient`). `CreateClient` her çağrıda yeni bir `HttpClient`
  döndürdüğü için bu güvenlidir — kullanılmış bir client'ın `Timeout`'unu değiştirmek istisna
  atardı. Böylece **açılışta pişen adres** ve **5 sn hard-code `Timeout`** (ki
  `ApiTimeoutMs` hiç okunmuyordu) tuzaklarının ikisi de kapandı.
- `ServiceRegistration.cs`'teki `.Get<T>()` **singleton anlık görüntü** kayıtları kaldırıldı.
  `ICaptureFileStore` singleton'dan **scoped**'a çekildi (çekim kökü artık scoped bir
  servisten geliyor).
- **Süre alanı sözleşmesi:** istemci `sourceOnDemandCloseAfterSec` olarak **saniye** gönderir;
  MediaMTX'in istediği `"10s"` biçimine çeviri yalnızca servis içinde yapılır.
- Uçlar: `GET|PUT /api/MediaGatewaySetting`, `GET|PUT /api/CameraCaptureSetting`. Tablo tek
  satırlık olduğu için kimlik taşıyan uç yoktur.

**Doğrulandı (yeniden başlatma olmadan):** `MaxClipDurationSec` 600 → 5 yapıldıktan hemen sonra
10 sn'lik klip isteği `400` ile reddedildi; `ApiBaseUrl` boş bir porta çevrildiğinde
`stream-ticket` yeni adrese gidip başarısız oldu, eski adrese düşmedi.

Ekranı da yazıldı — bkz. (c).

**(f)** ~~Günlük MediaMTX yol temizliği background servisi.~~ **TAMAMLANDI (2026-09-10).**

`MediaPathCleanupWorker` her gün `Jobs:MediaPathCleanupHour` saatinde (varsayılan 03:00,
yerel saat) çalışır. `IMediaGateway.ListPathsAsync` eklendi: `v3/config/paths/list` (kayıt
bayrağı) ile `v3/paths/list` (izleyici sayısı) **birleştirilerek** okunur — yalnızca birine
bakmak ya kayıttaki ya da izlenen yolu kaçırırdı. İki uç da sayfalı olduğu için liste sonuna
kadar okunur.

**Üç koruma kuralı** (şüpheli her durumda korur — yanlış silmenin bedeli, beklemenin
bedelinden büyüktür):

1. **Bizim üretmediğimiz yollara hiç dokunulmaz.** Yalnızca `cam_` / `clip_` önekli adlar
   aday olur (`IMediaGateway.IsManagedPathName`). Bu kural şart: `mediamtx.yml` içinde
   **`all_others`** adında bir girdi var ve silinseydi geçit komple yapılandırmasız kalırdı.
2. **`record: true` olan yol asla silinmez** — devam eden klip çekimleri böyle korunur; yolu
   düşürmek yazılmakta olan segmenti yarıda keserdi. Çekimi çökmüş bir artık klip yolu da bu
   kurala takılır (ikisi ayırt edilemiyor), bu yüzden korunur ve görünür olsun diye loglanır.
3. **İzleyicisi olan (`readers > 0`) yol silinmez** — birinin ekranındaki yayın kesilirdi.

Doğrulandı (gerçek MediaMTX v1.20.1 üzerinde): izleyicisiz `cam_…_sub` **silindi**,
`record: true` olan `clip_999` **korundu**, `all_others` **hiç dokunulmadı**.

**(g)** ~~Klip çekiminin paralelleştirilmesi.~~ **TAMAMLANDI (2026-09-10).**

`ClipCaptureWorker` kuyruktan aldığı her çekimi kendi görevinde başlatır; öncekinin
`Task.Delay(klip süresi)`'sini beklemez. **Sınırsız paralel**, eşzamanlılık sınırı yok.

- **Çakışma yok**, çünkü akıştaki her şey çekim bazlıdır: yol adı `clip_{captureId}`, geçici
  klasör `RecordRoot/clip_{captureId}`, `FindNewestClip` taraması, `TryDeleteTempDirectory` ve
  `finally`'deki `DeletePathAsync`. Her çekim ayrıca **kendi DI scope'unu** açar — `DbContext`
  paylaşılamaz.
- **`SingleReader = true` KALDI.** Kaldırılması "çok tüketiciye geçilirse" şartına bağlıydı;
  geçilmedi: kanalı okuyan hâlâ tek bir döngü, paralellik okumada değil **işlemede**. Bayrağın
  yorumu bunu açıklayacak şekilde güncellendi.
- Kapanışta devam eden çekimler beklenir (`Task.WhenAll`) — yarıda kesilen çekim, düşürülmemiş
  bir MediaMTX yolu ve silinmemiş bir geçici klasör bırakırdı.
- Görevler beklenmediği için istisnalar görev **içinde** yakalanır; aksi halde gözlenmemiş
  istisna olarak kaybolurdu.

**Ölçüldü:** iki klip çekimi arka arkaya kuyruğa alındı; `CapturedAtUtc` (yol kurulduktan hemen
sonra yazılır) damgaları **5,8 ms** arayla düştü. Seri çalışsaydı ikincisi ~12 sn sonra
başlayacaktı. Her iki çekimin `finally` bloğu da kendi yolunu düşürdü.

**Kalan risk:** aynı kameradan eşzamanlı çekimler `sourceOnDemand: false` ile **ayrı ayrı RTSP
oturumu** açar; kameranın eşzamanlı oturum limiti aşılırsa çekim "Medya geçidi klip dosyası
üretmedi" ile düşer.

**(h)** ~~Kart okuyucu ingest'i.~~ **TAMAMLANDI (2026-09-11)** — `POST /api/Scada/card`, § 5.3.
Okuyucu kimliği gövdede yok: kartın hangi kapıyı açacağına gözlemci (modül) karar veriyor.

**(i)** Geçiş kontrolü — **müşteriye özel modül olarak yazıldı (2026-09-11)**, bkz. § 10.
Genel otomasyon / iş akışı motoru (`Docs/data-structor.md` § 4.1) hâlâ yazılmadı; gelirse
modülün yayınladığı olayları tetikleyici olarak tüketebilir.

**(j)** ~~Saklama ve temizlik işleri.~~ **KISMEN TAMAMLANDI (2026-09-10).** Çekim dosyaları için
`CaptureRetentionWorker` yazıldı. **Kanal olayları için hâlâ hiçbir temizlik yok ve bu bilinçli**
— proje sahibi saklama politikasını kendisi ekleyecek; yukarıdaki "bilinçli boşluklar"a bakın ve
sormadan bir silme işi yazmayın.

**(k)** CI / dağıtım.

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

---

## 10. Müşteri modülleri — `Scadex.Signalization` (2026-09-11)

Bir sinyalizasyon müşterisi için **operatör işlemi takibi**: dış kapı açılır → kamera 1 sn arayla
5 kare çeker, sayaç başlar → operatör kart okutur, kurumunun iç kapı kilidi açılır → iç kapı
anahtarı (switch) açılışı/kapanışı doğrular → iç kapı kapalıyken kart tekrar okutulunca kapı
kilitlenir ve kabin sireni çalar → dış kapı kapanınca siren susar, işlem biter ve raporlanır.

### Neden ayrı bir modül (genel workflow motoru değil)

Senaryo durum tutan bir oturum makinesidir (korelasyon, kimlik, kapı bazlı yetki, anahtar
doğrulaması, zamanlı çekim). Genel bir düğüm grafiği motoru bunu ancak BPM motoruna dönüşerek
taşıyabilirdi; `Docs/data-structor.md` § 4.3 de geçiş kontrolünü workflow'dan ayrı bir modül
olarak tasarlamıştı. Firmaya özel iş çekirdeğe **girmez**:

```
Core → Model → DataAccess → Business → WebAPI
                                ↑          │
                    Scadex.Signalization ←─┘   (yalnızca WebAPI referans alır; Modules:Signalization:Enabled ile yüklenir)
```

Çekirdeğe eklenen üç şey de **genel amaçlıdır**: `User.IdentityCardId`, `POST /api/Scada/card`,
`IScadaEventObserver` (§ 5.3).

### Modül açma/kapama — `Modules:Signalization:Enabled`

Modül **kurulum başına** açılır; firma kimliği kodda sorulmaz. Tanımsız = kapalı:
`appsettings.json`'da `false`, `appsettings.Development.json`'da `true`. Modülü kullanan müşteri
kurulumu kendi ortam dosyasında ya da `Modules__Signalization__Enabled=true` ile açar.

Kayıt `Program.cs`'te `AddControllers()` zincirindedir: `.AddSignalizationModule(configuration)`.
MVC builder'a bağlanmasının sebebi, kapalıyken iki şeyin birden yapılması gerekmesidir:

- Context, servisler, üç arka plan işi ve `IScadaEventObserver`
  **kaydedilmez**. Modülün migration'ı uygulanmamış bir kurulumda zamanlayıcı 2 sn'de bir hata
  loglamaz; ingest kuyruğa olay bırakmaz; kart okuması "kayıtlı gözlemci yok" uyarısıyla loglanır.
- Modülün controller'ları **ApplicationPart listesinden çıkarılır**. Assembly WebAPI'nin referansı
  olduğu için controller'lar otomatik keşfedilir; yalnızca servisler koşullansaydı uçlar her
  istekte DI hatasıyla 500 dönerdi. Çıkarılınca 404 döner ve OpenAPI'de görünmezler. Açıkken
  assembly açıkça eklenir (iki kez eklenmez — uçlar belirsiz eşleşirdi).

**Yeni modül aynı kalıbı izler:** `Modules:<Ad>:Enabled` + zincire `.Add<Ad>Module(...)` satırı.
Reflection ile modül keşfi bilinçli olarak yok — kompozisyon kökündeki açık liste, neyin
yüklendiğini okunur tutar. Frontend'deki `VITE_MODULES` bunun aynasıdır ve ayrıca ayarlanır.
`dotnet ef` tasarım zamanında `Development` ortamını kullanır; modül orada açık olduğu için
modülün migration komutları çalışır.

**Doğrulandı (2026-09-11):** Production ortamında (kapalı) modül uçları 404, OpenAPI'de yok,
modül işleri başlamadı, çekirdek uçlar/ingest/kart 200. Development'ta (açık) uçlar 200, üç iş
başladı, dış kapı açılıp kapanınca oturum açılıp kapandı.

### Kararlar

- **Kimlik = mevcut `User`**, ayrı Operator tablosu yok. **Kurum = rol**: Belediye / Emniyet /
  Sinyalizasyon çekirdekte *veri* olarak açılan rollerdir; modül hangi rolün "kurum rolü"
  olduğunu `signalization.Authority` tablosunda bilir. Böylece ikinci bir yetki yapısı doğmaz.
  **Bir personelin tek kurumu vardır** → kabin başına tek iç kapısı. Modülün operatör ekranı
  (`PUT /api/SignalOperator/{userId}/authority`) diğer kurum rollerini çıkarıp seçileni ekler ve
  bunu çekirdeğin rol sync'i ile yazar; `/admin/users`'tan elle iki kurum rolü verilmişse kart
  `MultipleAuthorities` ile reddedilir.
- **Kapı sanaldır; ilişki kanala (`IoChannel`) kurulur, `Device`'a değil.** `Device` bir kartın
  tamamıdır — üç kurumun anahtarları aynı giriş kartında, kilitleri aynı röle kartında durabilir;
  yetki karta bağlansaydı belediye operatörü emniyet kilidine de yetkili olurdu.
  `SignalInnerDoor` = anahtar kanalı + kilit kanalı + ad + kurum; `SignalOuterDoor` = anahtar
  kanalı + kamera. Diyagramdaki saha cihazı yalnızca **etiket** olarak kullanılır (tek adımlık
  kablo; klemens üzerinden izleme yok).
- **Hiyerarşi:** kabin (ortak siren) → dış kapılar (anahtar, kamera) → iç kapılar (kurum, anahtar,
  kilit). Kabinde her kurumun en fazla bir aktif iç kapısı olur — kart okuma bu sayede tek kapıya
  çözülür. Okuyucu kimliği gövdede yoktur.
- **Oturum dış kapı başınadır**; iki dış kapı aynı anda bağımsız oturum taşır
  (`IX_OperatorSession_OuterDoorId`, unique, `WHERE EndedAtUtc IS NULL`).
- **Kilit tipi sürekli (aç / kilitle).** Darbeli kilit kapsam dışı. `UnlockTurnsOn` kilidin
  kendi mantığıdır (fail-secure/fail-safe); NO/NC terslemesi ayrıca çekirdekte çözülür.
  ⚠ Rölenin hem NO hem NC pini olan bir şablonda kilit/siren kanalının pini diyagramda
  **kablolu olmalıdır**, yoksa çekirdek komutu reddeder ("Output pini çözülemedi") ve olay
  `CommandFailed` olarak görünür.
- **Siren kabin başına tektir ve ortaktır.** Oturumlar yalnızca **talep** açar/kapatır; fiziksel
  siren "en az bir açık talep var mı" sorusuna **uzlaştırılır** (`SignalCabinetState`). Talep:
  kartla kilitleme başarılı **ve** o dış kapının ardındaki tüm iç kapılar kilitliyse açılır;
  `SirenDurationSec` dolunca, ilgili dış kapı kapanınca ya da aynı dış kapının ardında kilit
  yeniden açılınca kapanır. İki talep aynı anda açıkken siren ikisi de kapanana kadar çalar ve
  SCADA'ya tek "aç", tek "kapat" gider.
- **Oturum sonu:** dış kapı kapanırken kilitsiz iç kapının anahtarı kapalıysa otomatik kilitlenir
  (`AutoLocked`), açıksa kilitlenmez ve `InnerDoorLeftOpen` bayrağı konur.
- **Kartsız giriş:** `AwaitingCardTimeoutSec` içinde yetkili kart okutulmazsa `UnauthorizedEntry`
  bayrağı; oturum kapanmaz. `UnauthorizedEntry` ve `ForcedOpen` **güvenlik uyarısıdır**
  (`AlertFlags`, DTO'da `hasAlert`): canlı panelde vurgulanır. **Onay akışı yoktur**
  (2026-09-11 kararı) — bayrak oturum kaydında kalıcıdır, rapordan `flags` filtresiyle bulunur.
  Uyarılı bir oturum iki yoklama arasında açılıp kapanırsa canlı panelde görünmeyebilir; kayıt kalır.
  `SessionEventType` 16 boştur (eski `AlertAcknowledged`) — numara başka bir tipe verilmez.

### Çalışma zamanı

- `SignalizationScadaObserver` yalnızca kuyruğa bırakır. `SignalEventWorker` **kabin bazında
  sıralı, kabinler arasında paralel** işler (kabin başına bir şerit): durum makinesinde yarış
  olmaz, bir kabinde SCADA'nın 180 sn'lik zaman aşımı diğerlerini bekletmez.
- `EntrySnapshotWorker` kareleri şeridin **dışında**, başlangıçtan başlangıca `T0 + i × aralık`
  zamanlamasıyla çeker (`ICameraService.CreateCaptureAsync` — snapshot önbelleğini atlar).
- `SignalTimerWorker` 2 sn'de bir tarar (siren süresi, kart bekleme, azami oturum süresi) ve
  kararları **aynı kabin şeridine** bırakır. Süreler veritabanında (`*DueAtUtc`) — yeniden
  başlatmada kaybolmaz; hassasiyet ±2 sn.
- Komutlar `IDeviceCommandService.SendAsync`'ten geçer, **retry yoktur**; başarısız komutta kapı
  durumu değişmez, `CommandFailed` olayı ve bayrağı yazılır.

### Uçlar

| Uç | Not |
|---|---|
| `GET` · `PUT /api/SignalAuthority` | Kurum ↔ rol; PUT tam liste, aktif iç kapıda kullanılan kurum pasife alınamaz |
| `GET /api/SignalOperator` · `PUT /api/SignalOperator/{userId}/authority` | Tek kurum seçimi, çekirdeğin rol sync'iyle yazılır |
| `GET` · `PUT /api/SignalCabinet/{cabinetId}` · `GET …/options` | Yapılandırma ağacı (tam ağaç, Guid'i istemci üretir, çıkan kapı pasife); seçenekler kanalları kullanım bilgisi ve kablo etiketiyle verir |
| `GET /api/OperatorSession/open` | Canlı panel: yalnızca açık oturumlar; aşama türetilir, `hasAlert` güvenlik uyarısını gösterir |
| `POST /api/OperatorSession/list` · `GET /{id}` · `POST /summary` | Rapor: sayfalı liste, detay (zaman çizelgesi, kapı özeti, kareler), operatör/kurum/kabin özeti |

### Bilinen sonuçlar

- Kare dosyaları global `CaptureRetentionDays`'e tabidir; süresi dolunca rapordaki görüntü gider
  (satır kalır). Kanıt kalıcı olsun isteniyorsa ayar 0 yapılmalı.
- Aynı kurumdan iki operatör aynı kapıda çalışırsa, kapı kapalıyken okutulan ikinci kart kilitler.
- Kurum yetkisi tüm kabinlerde geçerlidir; kabin bazlı kısıt gelecekteki izin sistemine kalır.
- Bellek içi kuyruk: yeniden başlatmada işlenmemiş SCADA olayı kaybolabilir (zamanlayıcı işleri
  kaybolmaz).

### Doğrulandı (2026-09-11, sahte SCADA + sahte kamera ile)

Tek operatör akışı (5 kare ~1 sn arayla, aç → doğrula → kilitle → siren → dış kapı kapanınca
sus), iki operatör (siren yalnızca hepsi kilitliyken, yeniden açılışta susar), paralel dış kapılar
ve ortak siren (tek aç / tek kapat), siren zaman aşımı, kartsız giriş (kalıcı uyarı bayrağı), red gerekçeleri
(`UnknownCard`, `NoAuthority`, `MultipleAuthorities`), `ForcedOpen`, kapı açıkken kilitleme
reddi, SCADA 500'de `CommandFailed` ve tek deneme, `InnerDoorLeftOpen`, yapılandırma doğrulama
400'leri ve yapılandırmasız kabinde çekirdeğin değişmeden çalışması.
