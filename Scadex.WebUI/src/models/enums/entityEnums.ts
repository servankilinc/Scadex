// ─────────────────────────────────────────────────────────────
//  Cihaz
// ─────────────────────────────────────────────────────────────

/** Cihazın ağ/donanım durumu. Telemetri (SCADA ingest) tarafından yazılır. */
export const DeviceStatus = {
  Offline: 0,
  Online: 1,
  Warning: 2,
  Critical: 3,
  Maintenance: 4
} as const;
export type DeviceStatus = (typeof DeviceStatus)[keyof typeof DeviceStatus];

export const DeviceStatusLabels: Record<DeviceStatus, string> = {
  [DeviceStatus.Offline]: 'Çevrimdışı',
  [DeviceStatus.Online]: 'Çevrimiçi',
  [DeviceStatus.Warning]: 'Uyarı',
  [DeviceStatus.Critical]: 'Kritik',
  [DeviceStatus.Maintenance]: 'Bakımda'
};

/** Diyagram üzerinde yer alan (pinli, kablolanabilir) cihazların kategorisi. 0 değeri YOKTUR. */
export const DeviceType = {
  /** Ana kontrolcü kart (Ethernet + RS485 master). */
  ControlModule: 1,
  /** Dijital/analog giriş kartı (IN1..IN16). */
  InputModule: 2,
  /** Röle çıkış kartı (OUT1..OUT15, NC/COM/NO). */
  OutputModule: 3,
  /** LED gösterge kartı (LD1..LD8). */
  LedModule: 4,
  /** Klemens / dağıtım bloğu — pasif, sadece kablo toplar. */
  TerminalBlock: 5,
  /** Harici sensör (kabloyla bir giriş pinine bağlanan). */
  Sensor: 6,
  /** Çevre birimi — siren, kilit, lamba, yazıcı, POS, barkod okuyucu. */
  Peripheral: 7,
  /** Güç kaynağı / adaptör kartı. */
  PowerSupply: 8,
  /** Panoya monteli ölçüm cihazı — enerji analizörü, voltmetre, akım trafosu. */
  MeasurementDevice: 9,
  /** Kart okuyucu — geçiş kontrolünde kart ID'sini gönderen modül. */
  CardReader: 10,
  /** Şebeke girişi — 220 AC beslemenin kabine girdiği nokta (L / N / PE). */
  Mains: 11,
  /** Sigorta / devre kesici. */
  CircuitBreaker: 12
} as const;
export type DeviceType = (typeof DeviceType)[keyof typeof DeviceType];

export const DeviceTypeLabels: Record<DeviceType, string> = {
  [DeviceType.ControlModule]: 'Kontrol Modülü',
  [DeviceType.InputModule]: 'Giriş Kartı',
  [DeviceType.OutputModule]: 'Çıkış Kartı',
  [DeviceType.LedModule]: 'LED Kartı',
  [DeviceType.TerminalBlock]: 'Klemens',
  [DeviceType.Sensor]: 'Sensör',
  [DeviceType.Peripheral]: 'Çevre Birimi',
  [DeviceType.PowerSupply]: 'Güç Kaynağı',
  [DeviceType.MeasurementDevice]: 'Ölçüm Cihazı',
  [DeviceType.CardReader]: 'Kart Okuyucu',
  [DeviceType.Mains]: 'Şebeke Girişi',
  [DeviceType.CircuitBreaker]: 'Sigorta / Kesici'
};

// ─────────────────────────────────────────────────────────────
//  Pin
// ─────────────────────────────────────────────────────────────

/**
 * Pinin veri/enerji akış yönü.
 *
 * `AnalogInput` sıcaklık/nem gibi ölçüm noktalarıdır ve dijital girişten AYRI bir
 * adres uzayıdır: kartın çerçeve başlığı dijital girişi `'I'`, analogu `'A'` ile
 * ayırır ve ikisinin kanal numaraları bağımsızdır — aynı kartta hem `A/1`
 * (sıcaklık) hem `I/1` (darbe sensörü) bulunur. Sunucuda ayrımı yapan
 * `IX_IoChannel_CabinetId_Direction_ChannelNumber` benzersiz indeksidir.
 */
export const PinDirection = {
  Input: 0,
  Output: 1,
  Bidirectional: 2,
  AnalogInput: 3
} as const;
export type PinDirection = (typeof PinDirection)[keyof typeof PinDirection];

export const PinDirectionLabels: Record<PinDirection, string> = {
  [PinDirection.Input]: 'Giriş',
  [PinDirection.Output]: 'Çıkış',
  [PinDirection.Bidirectional]: 'Çift Yönlü',
  [PinDirection.AnalogInput]: 'Analog Giriş'
};

/**  Pinin bileşenin hangi kenarında durduğu — React Flow `<Handle position>` karşılığı. `RelativeX/Y` tek başına yetmez */
export const HandleSide = {
  Left: 0,
  Right: 1,
  Top: 2,
  Bottom: 3
} as const;
export type HandleSide = (typeof HandleSide)[keyof typeof HandleSide];

export const HandleSideLabels: Record<HandleSide, string> = {
  [HandleSide.Left]: 'Sol',
  [HandleSide.Right]: 'Sağ',
  [HandleSide.Top]: 'Üst',
  [HandleSide.Bottom]: 'Alt'
};

/** Pinin spesifik elektriksel fonksiyonu. `General = 99` — değerler süreksizdir. */
export const PinFunction = {
  /** Ortak uç (röle). */
  COM: 0,
  /** Normally Open (röle). */
  NO: 1,
  /** Normally Closed (röle). */
  NC: 2,
  /** Pozitif besleme (+VCC). */
  VCC: 3,
  /** Negatif / Toprak (GND). */
  GND: 4,
  /** RS485 Data+ hattı. */
  RS485_POS: 5,
  /** RS485 Data- hattı. */
  RS485_NEG: 6,
  /** RJ45 Ethernet portu. */
  RJ45: 7,
  /** LED anot (+). */
  LED_Anode: 8,
  /** LED katot (-). */
  LED_Cathode: 9,
  /** Dijital giriş sinyali. */
  Signal_In: 10,
  /** Dijital çıkış sinyali. */
  Signal_Out: 11,
  /** Analog giriş. */
  Analog_In: 12,
  /** Kuru kontak (Dry Contact). */
  DryContact: 13,
  /** Faz (220 AC). */
  Line_L: 14,
  /** Nötr (220 AC). */
  Neutral_N: 15,
  /** Toprak / koruma hattı (PE). */
  Earth_PE: 16,
  /** Genel amaçlı (özel tanımlı). */
  General: 99
} as const;
export type PinFunction = (typeof PinFunction)[keyof typeof PinFunction];

export const PinFunctionLabels: Record<PinFunction, string> = {
  [PinFunction.COM]: 'COM (Ortak Uç)',
  [PinFunction.NO]: 'NO (Normalde Açık)',
  [PinFunction.NC]: 'NC (Normalde Kapalı)',
  [PinFunction.VCC]: 'VCC (+)',
  [PinFunction.GND]: 'GND (-)',
  [PinFunction.RS485_POS]: 'RS485 A (+)',
  [PinFunction.RS485_NEG]: 'RS485 B (-)',
  [PinFunction.RJ45]: 'RJ45 Ethernet',
  [PinFunction.LED_Anode]: 'LED Anot (+)',
  [PinFunction.LED_Cathode]: 'LED Katot (-)',
  [PinFunction.Signal_In]: 'Dijital Giriş',
  [PinFunction.Signal_Out]: 'Dijital Çıkış',
  [PinFunction.Analog_In]: 'Analog Giriş',
  [PinFunction.DryContact]: 'Kuru Kontak',
  [PinFunction.Line_L]: 'Faz (L)',
  [PinFunction.Neutral_N]: 'Nötr (N)',
  [PinFunction.Earth_PE]: 'Toprak (PE)',
  [PinFunction.General]: 'Genel'
};

/**
 * Gerilim seviyesi — kablo doğrulaması ve renklendirme için.
 * DC/AC ayrımı değerin içinde kodludur; ayrı bir VoltageType yoktur.
 */
export const VoltageLevel = {
  None: 0,
  DC_12V: 1,
  DC_24V: 2,
  AC_220V: 3,
  Signal_5V: 4,
  Data: 5
} as const;
export type VoltageLevel = (typeof VoltageLevel)[keyof typeof VoltageLevel];

export const VoltageLevelLabels: Record<VoltageLevel, string> = {
  [VoltageLevel.None]: 'Belirtilmemiş',
  [VoltageLevel.DC_12V]: '12V DC',
  [VoltageLevel.DC_24V]: '24V DC',
  [VoltageLevel.AC_220V]: '220V AC',
  [VoltageLevel.Signal_5V]: '5V Sinyal',
  [VoltageLevel.Data]: 'Veri'
};

// ─────────────────────────────────────────────────────────────
//  Kablo
// ─────────────────────────────────────────────────────────────

/** Kablo fiziksel türü. */
export const WireType = {
  Power: 0,
  Signal: 1,
  DataRS485: 2,
  DataEthernet: 3,
  Relay: 4,
  Sensor: 5
} as const;
export type WireType = (typeof WireType)[keyof typeof WireType];

export const WireTypeLabels: Record<WireType, string> = {
  [WireType.Power]: 'Güç Kablosu',
  [WireType.Signal]: 'Sinyal Kablosu',
  [WireType.DataRS485]: 'RS485',
  [WireType.DataEthernet]: 'Ethernet',
  [WireType.Relay]: 'Röle',
  [WireType.Sensor]: 'Sensör'
};

/** Kablo çizim stili (UI). */
export const LineStyle = {
  Solid: 0,
  Dashed: 1,
  Dotted: 2
} as const;
export type LineStyle = (typeof LineStyle)[keyof typeof LineStyle];

export const LineStyleLabels: Record<LineStyle, string> = {
  [LineStyle.Solid]: 'Düz',
  [LineStyle.Dashed]: 'Kesikli',
  [LineStyle.Dotted]: 'Noktalı'
};

/** Kablonun canvas üzerinde çizim şekli. */
export const EdgeRouting = {
  /** Dik açılı kırılmalar (draw.io orthogonalEdgeStyle). */
  Orthogonal: 0,
  /** Uçtan uca düz çizgi. */
  Straight: 1,
  /** Yumuşak eğri (bezier). */
  Curved: 2
} as const;
export type EdgeRouting = (typeof EdgeRouting)[keyof typeof EdgeRouting];

export const EdgeRoutingLabels: Record<EdgeRouting, string> = {
  [EdgeRouting.Orthogonal]: 'Dik Açılı',
  [EdgeRouting.Straight]: 'Düz',
  [EdgeRouting.Curved]: 'Eğri'
};

// ─────────────────────────────────────────────────────────────
//  Canvas
// ─────────────────────────────────────────────────────────────

/** Cihaz olmayan diyagram elemanlarının görsel biçimi. `Group` sabiti KALDIRILDI. */
export const AnnotationShape = {
  /** Çerçevesiz düz metin. */
  Text: 0,
  /** Çerçeveli kutu. */
  Rectangle: 1,
  /** Not / açıklama balonu. */
  Note: 2,
  /** Yön oku. */
  Arrow: 3
} as const;
export type AnnotationShape = (typeof AnnotationShape)[keyof typeof AnnotationShape];

export const AnnotationShapeLabels: Record<AnnotationShape, string> = {
  [AnnotationShape.Text]: 'Metin',
  [AnnotationShape.Rectangle]: 'Kutu',
  [AnnotationShape.Note]: 'Not',
  [AnnotationShape.Arrow]: 'Ok'
};

/** Canvas arka plan deseni — React Flow `<Background variant>` karşılığı. */
export const BackgroundVariant = {
  /** Desen yok — düz zemin rengi. */
  None: 0,
  Dots: 1,
  Lines: 2,
  Cross: 3
} as const;
export type BackgroundVariant = (typeof BackgroundVariant)[keyof typeof BackgroundVariant];

export const BackgroundVariantLabels: Record<BackgroundVariant, string> = {
  [BackgroundVariant.None]: 'Desensiz',
  [BackgroundVariant.Dots]: 'Nokta',
  [BackgroundVariant.Lines]: 'Çizgi',
  [BackgroundVariant.Cross]: 'Artı'
};

// ─────────────────────────────────────────────────────────────
//  Kumanda (SCADA)
// ─────────────────────────────────────────────────────────────

/**
 * SCADA'ya gönderilen kontrol isteğinin türü.
 *
 * **Tek değer vardır: `SetOutput = 1`.** Darbe, değer yazma, modül reset ve
 * senkronizasyon türleri kaldırıldı; her kumanda bir çıkış kanalını hedefler ve
 * bir değer taşır, dolayısıyla `ioChannelId` ve `value` koşulsuz zorunludur.
 */
export const DeviceCommandType = {
  /** Çıkışı kalıcı sürer. Payload: `{ "value": "1" }` */
  SetOutput: 1
} as const;
export type DeviceCommandType = (typeof DeviceCommandType)[keyof typeof DeviceCommandType];

export const DeviceCommandTypeLabels: Record<DeviceCommandType, string> = {
  [DeviceCommandType.SetOutput]: 'Çıkış Sür'
};

/**
 * Kumandanın sonucu. Kuyruk olmadığı için `Pending`/`Cancelled` YOKTUR:
 * satır ancak istek gönderildikten sonra yazılır. 0 değeri YOKTUR.
 */
export const CommandStatus = {
  /** İstek gönderildi, cevap henüz işlenmedi (geçici ara durum). */
  Sent: 1,
  /** SCADA komutu kabul etti (2xx). */
  Succeeded: 2,
  /** SCADA hata döndürdü (4xx/5xx) — gövdesi resultMessage'dadır. */
  Failed: 3,
  /** Zaman aşımı veya bağlantı hatası — SCADA'ya hiç ulaşılamadı. */
  NoResponse: 4
} as const;
export type CommandStatus = (typeof CommandStatus)[keyof typeof CommandStatus];

export const CommandStatusLabels: Record<CommandStatus, string> = {
  [CommandStatus.Sent]: 'Gönderildi',
  [CommandStatus.Succeeded]: 'Başarılı',
  [CommandStatus.Failed]: 'Başarısız',
  [CommandStatus.NoResponse]: 'Yanıt Yok'
};

// ─────────────────────────────────────────────────────────────
//  İzleme (kamera)
// ─────────────────────────────────────────────────────────────

// VideoCodec KALDIRILDI. Sahada yalnızca H.264 kullanılıyor ve medya geçidinde
// transcoding yapılmıyor; seçilecek bir şey olmadığı için ne kolon ne alan
// kaldı. Kamera H.265 yayınlarsa tarayıcı çözemez ve görüntü siyah kalır —
// teşhis kameranın kendi arayüzünden yapılır.

/**
 * Hangi akışın izleneceği. 0 değeri YOKTUR.
 *
 * Ayrı bir seçim olarak var, çünkü aksi hâlde arayüz her yerde ana akımı açar:
 * 16 kameralık bir liste ekranında 16 adet 1080p akış demektir.
 */
export const StreamProfile = {
  /** Yüksek kalite — tam ekran / tek kamera görünümü. */
  Main: 1,
  /** Düşük bant genişliği — liste, küçük önizleme. */
  Sub: 2
} as const;
export type StreamProfile = (typeof StreamProfile)[keyof typeof StreamProfile];

export const StreamProfileLabels: Record<StreamProfile, string> = {
  [StreamProfile.Main]: 'Ana Akım',
  [StreamProfile.Sub]: 'Tali Akım'
};

/**
 * Merkeze alınan görüntünün cinsi. 0 değeri YOKTUR.
 *
 * NOT: Çekim yolu (ISAPI / medya geçidi) HENÜZ YAZILMADI — bu enum ve
 * `CaptureStatus` şemada hazır durur ama bugün onlara yazan bir kod yolu yok.
 * Bkz. `docs/api-contract/11-camera.md` § Kapsam dışı.
 */
export const CaptureType = {
  Snapshot: 1,
  Clip: 2
} as const;
export type CaptureType = (typeof CaptureType)[keyof typeof CaptureType];

export const CaptureTypeLabels: Record<CaptureType, string> = {
  [CaptureType.Snapshot]: 'Anlık Görüntü',
  [CaptureType.Clip]: 'Klip'
};

/**
 * Çekimin akıbeti. 0 değeri YOKTUR.
 *
 * `Failed` de bir SATIR bırakır: "o anda görüntü YOK" bilgisinin kendisi delildir.
 */
export const CaptureStatus = {
  Pending: 1,
  Available: 2,
  Failed: 3
} as const;
export type CaptureStatus = (typeof CaptureStatus)[keyof typeof CaptureStatus];

export const CaptureStatusLabels: Record<CaptureStatus, string> = {
  [CaptureStatus.Pending]: 'Hazırlanıyor',
  [CaptureStatus.Available]: 'Hazır',
  [CaptureStatus.Failed]: 'Başarısız'
};

// ─────────────────────────────────────────────────────────────
//  Yetki
// ─────────────────────────────────────────────────────────────

/**
 * Kullanıcı izin türleri.
 */
export const Permission = {
  ViewDiagram: 0,
  EditDiagram: 1,
  ControlOutput: 2,
  AcknowledgeAlarm: 3,
  ManageUsers: 4,
  ConfigureSystem: 5,
  ViewCamera: 6,
  ExportData: 7,
  ManageWorkflow: 8,
  /** Geçiş kartı tanımlama/yetkilendirme/iptal — kabin kapısını açan kimliği yönetir. */
  ManageAccessCards: 9
} as const;
export type Permission = (typeof Permission)[keyof typeof Permission];

export const PermissionLabels: Record<Permission, string> = {
  [Permission.ViewDiagram]: 'Diyagram Görüntüleme',
  [Permission.EditDiagram]: 'Diyagram Düzenleme',
  [Permission.ControlOutput]: 'Çıkış Kumandası',
  [Permission.AcknowledgeAlarm]: 'Alarm Onaylama',
  [Permission.ManageUsers]: 'Kullanıcı Yönetimi',
  [Permission.ConfigureSystem]: 'Sistem Ayarları',
  [Permission.ViewCamera]: 'Kamera Görüntüleme',
  [Permission.ExportData]: 'Veri Dışa Aktarma',
  [Permission.ManageWorkflow]: 'İş Akışı Yönetimi',
  [Permission.ManageAccessCards]: 'Geçiş Kartı Yönetimi'
};
