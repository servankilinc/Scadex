# Merkez Yazılım ↔ SCADA HTTP API

## Cihaz kimliği (SCADA → Merkez isteklerinde)

SCADA, Merkeze gönderdiği her isteğin header kısmında kendi cihaz kimliğini taşır:

```
DeviceId: 00:1A:2B:3C:4D:5E
```

- `DeviceId`: kontrol kartının MAC adresi; 6 bayt, büyük harf onaltılık, `:` ayraçlı (`AA:BB:CC:DD:EE:FF`).
- Merkez bu MAC adresini kendi tarafında tanımlı kontrol kartıyla eşleştirir ve isteğin hangi kontrol kartından geldiğini buradan çözer.
- Header yoksa, MAC biçiminde değilse ya da Merkezde bu MAC adresiyle eşleşen kontrol kartı yoksa http status`401`döner.

---

## 1. SCADA → Merkez: Input kartı merkeze veri gönderme endpointi

```
POST /api/Scada/ingest
Content-Type: application/json
```

```json
{
  "type": "I",
  "channelNumber": 7,
  "value": "1",
  "timestampUtc": "2026-10-28T09:15:30Z"
}
```

| Alan | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `type` | string | Evet | `"I"` dijital giriş, `"A"` analog giriş |
| `channelNumber` | int | Evet | Kart üzerindeki giriş numarası |
| `value` | string | Hayır | Değer, metin olarak (`"0"`, `"1"`, `"23.5"`) |
| `timestampUtc` | string | Hayır |  UTC |

Yanıt:

| Kod | Anlam |
|---|---|
| `200` | Alındı (gövde boş) |
| `400` | Gövde hatalı |
| `401` | Kimlik hatalı |
| `429` | Çok fazla istek (IP başına 300 istek / 5 sn) |

---

## 2. SCADA → Merkez: Kabinde ki kart okuyucudan okunan kart bilgisinin geldiği endpoint 

```
POST /api/Scada/card
Content-Type: application/json
```

```json
{
  "cardId": "04A1B2C3D4",
  "timestampUtc": "2026-10-28T09:16:02Z"
}
```

| Alan | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `cardId` | string | Evet | Kart numarası, en fazla 64 karakter |
| `timestampUtc` | string | Hayır | ISO 8601 UTC |

Yanıt: ingest ile aynı (`200` / `400` / `401` / `429`).

---

## 3. Merkez → SCADA: Output kartı komut gönderimi yapılan endpoint

SCADA'nın sağlayacağı uç:

```
POST {ScadaBaseUrl}/command
Content-Type: application/json
```

```json
{
  "channelNumber": 5,
  "value": "1",
  "delay": 0
}
```

| Alan | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `channelNumber` | int | Evet | Çıkış numarası (röle 1–16, LED 17–24) |
| `value` | string | Evet | `"1"` aç, `"0"` kapat |
| `delay` | int | Hayır | Saniye. Verilirse komut **hemen** uygulanır, bu süre kadar beklenir ve çıkış **eski hâline geri alınır**. Yoksa ya da `0` ise çıkış yeni değerde kalır. Scadex şimdilik göndermez |

Örnek: `{ "channelNumber": 5, "value": "0", "delay": 10 }` → 5 numaralı çıkışa bağlı cihazın gücü kesilir, 10 sn sonra yeniden verilir (cihaz yeniden başlatılır).

Kimlik yok. `delay` verildiğinde yanıt bekleme bitince değil, **komut uygulanınca** döner (Merkezin zaman aşımına takılmasın).

SCADA'nın döneceği:

| Yanıt | Scadex'in yorumu |
|---|---|
| `2xx` | Başarılı |
| `400` | Gövde hatalı |
| `4xx` / `5xx` | Başarısız (gövdedeki metin hata mesajı olarak gösterilir) |
| Yanıt yok / zaman aşımı | Kart çevrimdışı |

---

## 4. Merkez → SCADA: Input durumlarnı okuma

SCADA aynı adreste iki metot sağlar:

| Metot | Ne döner |
|---|---|
| `GET {ScadaBaseUrl}/status` | Kartta bulunan **tüm** kanallar |
| `POST {ScadaBaseUrl}/status` | Yalnızca gövdede istenen kanallar |

İki metotta da kimlik yok ve yanıt biçimi aynıdır (aşağıda "Yanıt").

### 4.1 Tüm kanallar — `GET`

```
GET {ScadaBaseUrl}/status
```

Gövde yok.

### 4.2 İstenen kanallar — `POST`

```
POST {ScadaBaseUrl}/status
Content-Type: application/json
```

Gövdede okunmak istenen kanallar dizi olarak gelir:

```json
[
  { "type": "O", "channelNumber": 5 },
  { "type": "I", "channelNumber": 7 },
  { "type": "A", "channelNumber": 1 }
]
```

| Alan | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `type` | string | Evet | `"I"` dijital giriş, `"A"` analog giriş, `"O"` çıkış |
| `channelNumber` | int | Evet | Kanal numarası (çıkışta röle 1–16, LED 17–24) |

- Dizi en az bir kanal içermelidir; boş dizi `400` (tüm kanallar için `GET` kullanılır).

### Yanıt

`200` + JSON dizi, her eleman bir kanal:

```json
[
  { "type": "O", "channelNumber": 5, "value": "1" },
  { "type": "I", "channelNumber": 7, "value": "1" },
  { "type": "A", "channelNumber": 1, "value": "23.5" }
]
```

| Alan | Tip | Açıklama |
|---|---|---|
| `type` | string | `"I"` dijital giriş, `"A"` analog giriş, `"O"` çıkış |
| `channelNumber` | int | Kanal numarası |
| `value` | string | Değer, metin olarak. `I` / `O`: `"0"` / `"1"`; `A`: `"23.5"` gibi. `O` için rölenin **o anki fiziksel** durumu (son komut değil) |

- Sıra önemsizdir; aynı `type` + `channelNumber` bir kez geçer.
- Değeri okunamayan kanal diziye konmaz (boş string / `null` gönderilmez). `POST`'ta kartta bulunmayan kanal da konmaz; istenen diğer kanallar yine döner.

SCADA'nın döneceği:

| Yanıt | Scadex'in yorumu |
|---|---|
| `2xx` | Başarılı |
| `400` | Gövde hatalı (yalnızca `POST`) |
| `4xx` / `5xx` | Başarısız (gövdedeki metin hata mesajı olarak gösterilir) |
| Yanıt yok / zaman aşımı | Kart çevrimdışı |
