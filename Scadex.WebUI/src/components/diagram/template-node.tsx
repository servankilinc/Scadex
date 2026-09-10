import { Handle, Position, type NodeProps } from '@xyflow/react';
import { DeviceStatus, DeviceTypeLabels, HandleSide } from '@/models/enums';
import type { DiagramPinDto } from '@/models/diagram';
import { readableTextColor, safeCssColor } from '@/lib/diagram/colors';
import { useLiveChannel, useLiveDevice } from '@/lib/diagram/live-store';
import { SIDE_TO_POSITION, pinPlacementStyle } from '@/lib/diagram/pin-side';
import { templateImageSrc } from '@/lib/diagram/template-image';
import type { DeviceNode } from '@/lib/diagram/to-rf-nodes';
import { cn } from '@/lib/utils';
import { DeviceNodeMenu } from './node-menu';

/**
 * TÜM cihazları render eden tek node bileşeni.
 *
 * DeviceType başına ayrı bileşen yok: `ComponentTemplate` zaten genişlik,
 * yükseklik, renk ve port şemasını taşıyor — görsel spec budur. 12 ayrı bileşen
 * aynı renderer'ın 12 kopyası olurdu ve backend'e yeni bir tip eklemek (ki
 * migration bile gerektirmiyor) frontend deploy'u zorunlu kılardı.
 *
 * Tipe özgü fark burada bir lookup ile çözülür, ayrı bir bileşenle değil.
 *
 * **Canlı veri buraya PROP OLARAK GELMEZ.** Durum ve kanal değerleri harici
 * store'dan kendi kimlikleriyle okunur (`live-store.ts`). Sebep: React Flow
 * node nesnelerinin `data`'sı değişmediği için RF'in reconciliation'ı hiç
 * tetiklenmez ve bir kanal değiştiğinde yalnızca o değeri okuyan küçük bileşen
 * yeniden render olur — 500 kanallı bir kabinde tüm graf değil.
 */
export function TemplateNode({ data, selected }: NodeProps<DeviceNode>) {
  const { device } = data;
  const background = safeCssColor(device.template.backgroundColor);
  const foreground = readableTextColor(device.template.backgroundColor);
  const imageSrc = templateImageSrc(device.template.backgroundImageUrl);

  // Canlı durum sunucu anlık görüntüsünü EZER: snapshot grafın çekildiği andaki
  // durumu taşır, store ondan sonrasını.
  const live = useLiveDevice(device.id);
  const statusId = live ? live.statusId : device.deviceStatusId;

  return (
    // Sağ tık menüsü node'un DIŞINDA sarmalıyor: içeri konsaydı
    // `overflow-hidden` menüyü kırpar, ayrıca pin handle'larının üzerinde sağ tık
    // menüyü açmazdı.
    <DeviceNodeMenu device={device}>
      <div
        className={cn(
          'relative flex h-full w-full flex-col overflow-hidden rounded-md border-2 text-[11px] shadow-sm transition-all',
        // Secim vurgusu yalnizca kenarlik rengiyle yapilirsa acik zeminli bir
        // sablonda fark edilmiyor; disa tasan bir halka her renkte gorunur.
          selected ? 'border-sky-500 ring-2 ring-sky-500/40 ring-offset-1 shadow-lg' : 'border-slate-400/70',
          !device.isActive && 'opacity-50'
        )}
        style={{
          background,
          color: foreground,
          // React Flow'da rotation prop'u yoktur; dönme node kökünde CSS ile
          // uygulanir. Handle konumlari da bu donusumle birlikte doner.
          transform: device.rotation ? `rotate(${device.rotation}deg)` : undefined
        }}>
        {/* Arka plan gorseli EN ALTTA ve pointer-events-none: uzerine tiklamak
            node'u secmeli, resmi surukleme baslatmamali (tarayicilar <img>'yi
            varsayilan olarak suruklenebilir sayar).

            `object-fit: fill` bilerek: sablonun en-boy orani gorselin oraniyla
            AYNI tutuluyor (bkz. `fitToTemplate`), dolayisiyla germe olmaz —
            `contain` kullansaydik oran kucuk bir yuvarlamayla kaysa bile
            kenarlarda bosluk olusur ve pinler cizimden kayardi. */}
        {imageSrc && (
          <img
            src={imageSrc}
            alt=''
            aria-hidden
            draggable={false}
            className='pointer-events-none absolute inset-0 size-full select-none object-fill'
          />
        )}

        {/* Gorsel varken metin katmani BASTIRILIYOR: cizimin uzerine yazilan ad
            ve tip, klemens etiketlerinin uzerine biner ve ikisi de okunmaz olur.
            Ad zaten ozellikler panelinde ve minimap renginde mevcut. */}
        {!imageSrc && (
          <>
            <header className='flex items-center justify-between gap-1 border-b border-current/20 px-1.5 py-1'>
              <span className='truncate font-semibold'>{device.name}</span>
              <StatusDot statusId={statusId} />
            </header>

            <div className='flex flex-1 flex-col justify-center px-1.5 text-center'>
              <span className='truncate opacity-80'>{DeviceTypeLabels[device.template.deviceTypeId]}</span>
              {device.externalCode && <span className='truncate font-mono text-[10px] opacity-60'>{device.externalCode}</span>}
            </div>
          </>
        )}

        {/* Gorselli node'da durum yine de gorunmeli — kumandanin en kritik
            bilgisi o. Sag ust kosede, cizimin uzerinde kucuk bir rozet. */}
        {imageSrc && (
          <span className='absolute top-1 right-1 z-10 flex items-center gap-1 rounded-sm bg-slate-900/75 px-1 py-0.5' title={device.name}>
            <StatusDot statusId={statusId} />
          </span>
        )}

        {device.pins.map(pin => (
          <PinHandle key={pin.id} pin={pin} />
        ))}
      </div>
    </DeviceNodeMenu>
  );
}

/**
 * Pin başına TEK handle, `type="source"`.
 *
 * `PinDirection.Bidirectional` için çift handle üretmek pin id ↔ handle id 1:1
 * eşlemesini bozardı. Bunun yerine canvas `ConnectionMode.Loose` ile çalışır:
 * loose modda bağlantı herhangi bir handle'da başlayıp bitebilir — kablolama
 * diyagramında "source"/"target" zaten keyfi bir ayrımdır.
 */
function PinHandle({ pin }: { pin: DiagramPinDto }) {
  const position = SIDE_TO_POSITION[pin.side] ?? Position.Left;

  return (
    <>
      <Handle
        id={pin.id}
        type='source'
        position={position}
        // `pinPlacementStyle` İKİ ekseni birden veriyor ve React Flow'un
        // kenar sınıfını ezip geçiyor. Bu ŞART: arka plan görselli şablonlarda
        // pin kenarda değil, çizimin ortasındaki bir klemensin üzerinde
        // durabiliyor — RF'in "sağ kenar" konumlandırması orada yanlış olurdu.
        style={pinPlacementStyle(pin)}
        className='size-2! border! border-slate-600! bg-slate-200!'
        title={`${pin.name}${pin.channelNumber != null ? ` (CH${pin.channelNumber})` : ''}`}
      />
      <PinValue pin={pin} />
    </>
  );
}

/**
 * Pinin canlı kanal değeri.
 *
 * AYRI bir bileşen olması bilinçli: `useSyncExternalStore` aboneliği bu bileşene
 * ait olduğu için bir kanal değiştiğinde yalnızca burası yeniden render olur —
 * ne cihaz kutusu ne de diğer pinler.
 *
 * Etiket `<Handle>`'ın İÇİNE konmuyor: handle bir bağlantı hedefidir ve içine
 * konan metin fare olaylarını yakalayıp kablo çizmeyi zorlaştırırdı.
 */
function PinValue({ pin }: { pin: DiagramPinDto }) {
  const live = useLiveChannel(pin.ioChannelId);
  if (!live || live.value == null) return null;

  return (
    <span
      className='pointer-events-none absolute z-10 max-w-[46%] truncate rounded-sm bg-slate-900/85 px-1 font-mono text-[9px] leading-[1.35] text-slate-50'
      style={{
        ...pinPlacementStyle(pin),
        // Etiket pinin konumundan İÇERİ doğru itilir; pinin tam üstünde dursaydı
        // handle'ı örter, dışarı taşsaydı komşu node'un üstüne binerdi.
        //
        // Kaydırma `side`'a göre: `side` artık "hangi kenarda" değil "kablo
        // hangi yöne çıkıyor" demek, dolayısıyla etiketi ters yöne — yani
        // gövdenin içine — itmek her iki yerleşimde de doğru sonucu veriyor.
        translate: LABEL_OFFSET[pin.side]
      }}
      title={`${pin.name}: ${live.value}`}>
      {live.value}
    </span>
  );
}

/**
 * Değer etiketinin pinden ne kadar içeri itileceği.
 *
 * `transform` DEĞİL `translate` kullanılıyor: `pinPlacementStyle` zaten
 * `transform: translate(-50%, -50%)` yazıyor ve onu ezmek ortalamayı bozardı.
 * CSS `translate` özelliği `transform`'dan SONRA uygulanır, yani ikisi birikir.
 */
const LABEL_OFFSET: Record<HandleSide, string> = {
  [HandleSide.Left]: '60% 0',
  [HandleSide.Right]: '-60% 0',
  [HandleSide.Top]: '0 120%',
  [HandleSide.Bottom]: '0 -120%'
};

/** Durum rozeti. `null` = hiç telemetri alınmadı; `Offline` ile AYNI ŞEY DEĞİL. */
function StatusDot({ statusId }: { statusId: DeviceStatus | null }) {
  if (statusId == null) {
    return <span className='size-1.5 shrink-0 rounded-full bg-current opacity-25' title='Telemetri yok' />;
  }

  const color =
    statusId === DeviceStatus.Online
      ? 'bg-emerald-500'
      : statusId === DeviceStatus.Warning
        ? 'bg-amber-500'
        : statusId === DeviceStatus.Critical
          ? 'bg-red-500'
          : statusId === DeviceStatus.Maintenance
            ? 'bg-sky-500'
            : 'bg-slate-500';

  // Online'da hafif bir nabiz: sabit bir noktanin canli mi yoksa donmus mu
  // oldugu ancak baska bir seyle karsilastirilarak anlasilir.
  return <span className={cn('size-1.5 shrink-0 rounded-full', color, statusId === DeviceStatus.Online && 'animate-pulse')} />;
}
