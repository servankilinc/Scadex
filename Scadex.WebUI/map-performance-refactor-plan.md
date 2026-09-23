# Ana sayfa haritası — performans refactor planı

> İlk taslak ChatGPT'den geldi; bu sürüm kodla karşılaştırılarak gözden geçirildi. Eleme ve ekleme
> gerekçeleri § 3 ve § 4'te.

## 1. Sorun

Ana sayfa haritası ([`src/views/app/home/index.tsx`](src/views/app/home/index.tsx)) kabinleri tek tek
DOM işaretçisi olarak çiziyor. Koddan doğrulanan darboğazlar:

1. **Controlled viewport — asıl darboğaz.** `<Map viewport onViewportChange>` mapcn'de her `move`
   olayında (pan sırasında ~60/sn) `setViewport`'u çağırıyor. Her çağrı `Home`'u baştan render ediyor ve N
   adet `MapMarker` + `MarkerContent` portalı + `MarkerLabel` + `MarkerPopup` portalı yeniden
   uzlaştırılıyor. Home viewport'u yalnızca ilk ortalama ve filtre sonrası ortalama için tutuyor.
2. Kabin başına bir MapLibre `Marker` (DOM), bir `Popup` örneği ve iki React portalı üretiliyor.
3. Meşgul kabin sorgusu 10 sn'de bir yoklanıyor (`LIVE_POLL_MS`). `combineBusyCabinetIds` her seferinde yeni
   bir `Set` döndürüyor, bu yüzden içerik aynı olsa bile tüm işaretçiler yeniden render oluyor.
4. İkon PNG'leri 264×493 ve 314×490 piksel; ekranda 48 piksel yüksekliğinde gösteriliyor.

## 2. Kütüphane kararı — MapLibre'de kalınır, yeni bağımlılık eklenmez

- `src/components/ui/map.tsx` bir paket değil, **projeye kopyalanmış mapcn kodu**. İçinde MapLibre GL JS
  6.10 (WebGL) çalışıyor. `useMap()` ham `maplibregl.Map`'i ve `isLoaded`'ı verdiği için
  source/layer/cluster API'lerinin tamamı zaten erişilebilir. Yavaş olan mapcn değil, `MapMarker`
  (DOM işaretçisi) kullanımı.
- Değerlendirilen alternatifler:
  - **react-map-gl:** yalnızca daha bildirimsel bir API sunar, hız kazandırmaz; üstüne ikinci bir
    sarmalayıcı ekler.
  - **deck.gl:** milyon noktalık işler içindir; bu ölçek için fazla. Bundle'ı büyütür ve Türkçe karakter
    için font atlası ayarı ister.
  - **Leaflet:** DOM/Canvas tabanlıdır; bugünkünden yavaş olur.
  - **OpenLayers:** bütün harita yığınını değiştirmek demektir.
- `map.tsx`'e dokunulmaz (vendored; upstream'den ayrışmasın). `location-picker.tsx` tek işaretçi
  kullandığı için olduğu gibi kalır.

## 3. İlk taslağın maddeleri — karar tablosu

| § | Madde | Karar | Gerekçe / değişiklik |
|---|---|---|---|
| — | Korunacak davranış listesi | **Tutuldu, düzeltildi** | "Merkeze taşı" → "tamamını çerçeveye sığdır" (`fitBounds`, § 5). Hover'da büyüme ve imleç eklendi |
| 1 | Önce map implementasyonunu incele | **Kaldırıldı** | Cevaplandı (§ 2) |
| 2 | GeoJSON veri modeli | **Tutuldu, sadeleştirildi** | Özellikler yalnızca `id`, `name`, `isBusy`. Filtre JS'te uygulandığı için `statusId` / `isActive` worker'a boşuna taşınmaz |
| 3 | İkonları "bir kez" kaydet | **Düzeltildi** | Yanlıştı: mapcn tema değişiminde `setStyle(diff:false)` yapıyor ve bu image/source/layer'ların hepsini siliyor. **Her stil yüklenişinde** yeniden eklenir. İkon seçimi tek katmanda `case` ifadesiyle yapılır; iki katmanlı yedek gereksiz |
| 4 | Etiketler symbol text katmanında | **Tutuldu, genişletildi** | Glyph/font ve çakışma kuralları eklendi (§ 4) |
| 5 | Tek popup | **Tutuldu, düzeltildi** | mapcn'de zaten `MapPopup` var, yeni bileşen yazılmaz. State'te nesne değil **id** tutulur. Kapanma yarışı giderildi (§ 4) |
| 6 | Sağ panele dokunma | **Tutuldu** | — |
| 7 | Viewport olaylarını throttle et | **Değiştirildi** | Throttle yerine controlled mod **tamamen kaldırıldı**; pan/zoom sırasında Home 0 kez render olur |
| 8 | Render sırasında setState | **Tutuldu** | State yerine ref + map örneğine bağlı effect. Hiç setState olmadığı için "render kaskadı" kaygısı da ortadan kalkar |
| 9 | Memoization listesi | **Büyük kısmı kaldırıldı** | Hepsi zaten `useMemo`'daydı. Yalnızca GeoJSON memosu ve meşgul kümesinin referans kararlılığı (yeni) eklendi |
| 10 | Tek source + `setData` | **Tutuldu** | Katman filtresi (`setFilter`) değil `setData`, çünkü küme sayıları durum filtresini yansıtmalı |
| 11 | Kümeleme | **Eklendi** (ürün kararı) | Uzak zoom'da sayılı kümeler. Meşgul kabin içeren küme işaretlenir (§ 4) |
| 12 | Profiler analizi | **Doğrulamaya taşındı** | § 7 |
| 13 | 100/500/1k/5k/10k testleri | **Kısaltıldı** | 100 / 1k / 5k / 10k; commit edilmeyen geçici sahte veri yamasıyla |
| 14 | Gerçek zamanlı hazırlık | **Not** | § 8 |
| 15 | MapLibre'a doğrudan geçiş kriteri | **Kaldırıldı** | Karar verildi (§ 2) |
| 16–18 | Kod kalitesi / kabul / rapor | **Kısaltıldı** | § 6 ve § 7 |

## 4. İlk taslağın atladığı maddeler

1. **Controlled viewport'un kaldırılması**: en büyük kazanç bu (taslağın § 7'sinin yerini alır).
2. **Tema değişimi özel katmanları siliyor.** Kurulum effect'i mapcn'in kendi katmanları gibi
   `[map, isLoaded]` anahtarıyla çalışır; temizlik `try/catch` içindedir. StrictMode'daki çift mount için
   `getSource` / `hasImage` korumaları vardır.
3. **Meşgul kümesinin referans kararlılığı.** `combine` sıralanmış bir anahtar string döndürür ve `Set` bu
   anahtar üzerinden `useMemo`'lanır. Böylece 10 sn'lik yoklama, içerik değişmedikçe `setData` tetiklemez.
4. **Popup kapanma yarışı.** MapLibre popup'ının `closeOnClick` dinleyicisi ile katmanın tıklama dinleyicisi
   aynı `click` olayında çalışır; hangisinin önce kaydedildiğine göre (tema değişimi dinleyicileri yeniden
   bağlar) popup ya B'ye geçmeden kaybolur ya da state "açık" derken haritadan silinmiş hâlde takılı kalır.
   Çözüm: popup'ın `closeOnClick`'i **kapalı**. Kapanmayı yalnızca React state'i yönetir; boş alana ya da
   kümeye tıklama, katmanın tek tıklama işleyicisinde yakalanır.
5. **İkonlar önceden küçültülür.** WebGL ikon atlası mipmap kullanmaz; 493 pikseli ~10 kat küçültmek kenarları
   tırtıklı gösterir. Görsel bir kez canvas'ta 96 piksel yüksekliğe (`pixelRatio: 2`) çizilir,
   `drop-shadow-lg` gölgesi de canvas'a gömülür, böylece çalışma anında maliyeti yoktur. Sonuç modül
   düzeyinde bir Promise'te önbelleklenir; tema değişiminde yeniden indirilmez.
6. **Glyph / Türkçe karakter.** Carto stilinin glyph sunucusunda `Open Sans Regular`, `Open Sans Bold` ve
   `Montserrat Medium` var. mapcn'in küme katmanının kullandığı `Open Sans Semibold` stil listesinde **yok**,
   bu yüzden kullanılmaz. `ş ğ ı İ ç ö ü` gözle doğrulanır.
7. **Etiket çakışması.** İkonlar her zaman çizilir (`icon-allow-overlap`). Çakışan etiket düşer
   (`text-optional`), yakın zoom'da hepsi görünür. DOM'daki beyaz hap görünümünün yerini tema duyarlı
   `text-halo` alır.
8. **Çift tıklama.** `stopPropagation` yerine katman `dblclick` olayında `e.preventDefault()` çağrılır; bu
   MapLibre'nin çift tıkla yakınlaştırmasını durdurur. Bugünkü sonuçla aynı olsun diye (iki tık popup'ı açıp
   kapatıyordu) popup kapatılır ve panel açılır.
9. **Kümedeki meşgul kabin kaybolmaz.** `clusterProperties.busyCount` ile, içinde işlem yapılan kabin
   olan kümenin kenarı amber ve kalın çizilir.
10. **`symbol-sort-key`:** üst üste binmede meşgul kabin simgesi boştakilerin üstünde kalır.
11. **Hover'da büyüme.** `icon-size` bir layout özelliği olduğu için feature-state ile değiştirilemez. Bunun
    yerine ayrı bir hover katmanı kullanılır: ikonu 1,2 kat büyük çizer ve filtresi yalnızca hover edilen
    id değiştiğinde `setFilter` ile güncellenir.
12. **`Map` adı gölgeleniyor.** Home'daki `import { Map }` global `Map` kurucusunun adını kapatıyor. Id →
    kabin dizini, mapcn'in `Map`'ini import etmeyen katman dosyasında tutulur.

## 5. Uygulama

### `src/views/app/home/cabinet-map-layer.tsx` (yeni)

`useMap()` ile çalışan ve `null` render eden `CabinetMapLayer` bileşeni:

- **Veri:** `FeatureCollection<Point, { id, name, isBusy }>`, `[cabinets, busyCabinetIds]` üzerinden
  `useMemo`'lanır. Değişince `setData` çağrılır.
- **Kaynak:** tek bir `cabinets` GeoJSON source'u. Ayarlar: `cluster: true`, `clusterMaxZoom: 14`,
  `clusterRadius: 50`, `clusterProperties.busyCount`.
- **Katmanlar:**
  - `cabinet-clusters`: sayıya göre renk ve yarıçap; içinde meşgul kabin varsa amber kenar.
  - `cabinet-cluster-count`: küme sayısı.
  - `cabinet-points`: ikon (`case(isBusy)`) ve ikonun altında etiket.
  - `cabinet-point-hover`: hover edilen kabin için büyütülmüş ikon.
- **Olaylar:**
  - Tek bir `click` işleyicisi: kabine tıklama popup'ı açar ya da kapatır; boş alana veya kümeye tıklama
    popup'ı kapatır; küme tıklaması ayrıca açılma zoom'una `easeTo` yapar.
  - Nokta çift tıklaması → yakınlaştırma engellenir, panel açılır.
  - Hover → imleç ve büyüme.
- **Kurulum:** her stil yüklenişinde (`isLoaded` / tema) yapılır. Callback'ler ref üzerinden, projedeki
  kalıpla (`useLayoutEffect`) okunur.

### `src/views/app/home/index.tsx`

- `MapMarker` / `MarkerContent` / `MarkerLabel` / `MarkerPopup` kullanımı kalkar.
- `viewport` ve `hasAutoCentered` state'leri ile render sırasındaki setState kalkar. Harita uncontrolled
  çalışır; örneğe `ref` ile erişilir.
- Ortalama `fitBounds` ile yapılır (en fazla zoom 17; tek kabinde sonuç bugünküyle aynı):
  - ilk açılışta bir kez ve animasyonsuz (refetch'lerde tekrar çalışmaz),
  - durum rozetinde ve kabin filtresinde animasyonlu.
- Tek popup: mapcn `MapPopup`'ı (`closeOnClick={false}`); id state'i, `visibleCabinets`'ten türetilir. Aynı
  kabine yeniden tıklamak popup'ı kapatır (eski `MarkerPopup`'ın aç/kapa davranışı). İçerik aynı: firma, ad,
  durum, Detay.
- Sağ panel, durum rozetleri, filtre paneli (`Select` dahil) ve `DashboardMetrics` değişmez.

## 6. Kod kuralları

- `any` yok. `map.on` ile eklenen her dinleyici `map.off` ile, eklenen her katman ve source temizlikte
  kaldırılır.
- Render sırasında ref yazılmaz. Lint `queueMicrotask` / `requestAnimationFrame` ile atlatılmaz.
- Yorumlar Türkçe, kod isimleri İngilizce. `src/components/ui/**` altındaki vendored dosyaya dokunulmaz.

## 7. Kabul ve doğrulama

**Otomatik:** `npm run lint` ve `npm run build` yeşil. ✔

**İşlevsel** (çalışan backend + `npm run dev`; headless Chrome ile sürüldü: gerçek veri ve 1k sahte kabin):

- [x] İlk açılışta kabinler çerçeveye sığıyor. Kaydırılmış harita, 10 sn'lik yoklama döngüsünden sonra yerinde kalıyor.
- [x] Boşta ve meşgul ikonları doğru. Meşgul görünüm katman düzeyinde doğrulandı; canlı yoklama yolu ise sahada açık bir operatör işlemiyle ayrıca kontrol edilmeli.
- [x] Kabin adları görünüyor; Türkçe karakterler (`Şişli Güneşçi İğdır`) doğru çiziliyor.
- [x] Uzak zoom'da kümeler çıkıyor, tıklayınca açılıyor; meşgul kabin içeren kümenin kenarı amber.
- [x] Durum rozetleri süzüp çerçeveye sığdırıyor; "Tümü" filtreyi kaldırıyor; kabin filtresi (Select → Filtrele) seçilen kabine zoom 17'de iniyor.
- [x] Tek tık popup açıyor; başka kabine tıklamak popup'ı oraya taşıyor; aynı kabine tekrar tıklamak kapatıyor; boş alana tıklamak kapatıyor. Tema değişiminden sonra da aynı.
- [x] Çift tık paneli açıyor ve yakınlaştırmıyor.
- [x] Hover'da büyüme katmanı ve imleç çalışıyor.
- [x] Koyu/açık tema geçişinden sonra ikonlar, etiketler (koyu hale) ve kümeler geri geliyor.
- [x] Modül panel bileşeni (Sanal Kabin) ve DashboardMetrics görünüyor. Harita kontrolleri (zoom, pusula, konum, tam ekran) koda dokunulmadan kaldı, elle denenmedi.

**Performans:** kaynak koda yama yapılmaz. Tarayıcı sürücüsü (Playwright) `POST /api/Cabinet/list`
yanıtını yakalar ve listeyi Türkiye geneline dağılmış 1k / 5k / 10k kabine çoğaltır.

- [x] `document.querySelectorAll('.maplibregl-marker').length === 0`: 100 / 1k / 5k / 10k.
- [x] Pan + tekerlek zoom boyunca React commit sayısı **0**: 100 / 1k / 5k / 10k.
- [x] Kümeleme, küme tıklaması, kümedeki meşgul kabinin amber kenarı ve Türkçe glifler: 10k'da doğrulandı.
- [ ] Gerçek GPU'lu Chrome'da FPS ve filtre süresi. Headless ölçüm SwiftShader (yazılımsal WebGL)
      üzerindeydi; oradaki ~11–17 FPS ve 50–370 ms'lik uzun görevler karelerin CPU'da çizilmesinden
      kaynaklanır, bu yüzden temsilî değildir.

## 8. Kapsam dışı (bilinçli)

- mapcn, MapLibre worker'ını çalışma anında **unpkg CDN**'den yüklüyor (`map.tsx:25`). İnternetsiz kurulum
  ve CSP açısından risklidir; ayrı bir iş olarak ele alınmalı (Vite `?worker&url`).
- 10k kabinde `getCabinetList` yükünün boyutu (sayfalama yok) backend konusudur.
- Canlı konum/durum akışı gelirse `setData` yerine `GeoJSONSource.updateData` (fark güncellemesi)
  değerlendirilir; o gün kümelemeyle uyumu doğrulanmalı.
- Kabin filtresindeki `Select` büyük listede yavaş açılabilir; ürün kararıyla şimdilik kalıyor.
