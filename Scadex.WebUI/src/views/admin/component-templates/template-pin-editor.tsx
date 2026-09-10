import { useRef, useState, type MouseEvent, type PointerEvent as ReactPointerEvent } from 'react';
import { Trash2Icon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  HandleSide,
  HandleSideLabels,
  PinDirection,
  PinDirectionLabels,
  PinFunction,
  PinFunctionLabels,
  VoltageLevel,
  VoltageLevelLabels
} from '@/models/enums';
import type { TemplatePinDraft } from '@/models/componentTemplate';
import { readableTextColor, safeCssColor } from '@/lib/diagram/colors';
import { pinPlacementStyle, snapToEdge } from '@/lib/diagram/pin-side';
import { templateImageSrc } from '@/lib/diagram/template-image';
import { cn } from '@/lib/utils';

/**
 * Pin şemasının görsel editörü.
 *
 * **Önizleme, canvas ile AYNI yerleşim fonksiyonunu kullanıyor**
 * (`pinPlacementStyle`). Editör kendi matematiğini yazsaydı, yazarken görülen
 * pin konumu ile şablon kullanıldığında çizilen konum ayrışabilirdi — ve bu
 * ayrışma ancak şablonu kullanmaya kalkınca fark edilirdi. (Tam olarak bu oldu:
 * yerleşim iki yere bölünmüşken sağ pinler sola düşüyordu.)
 *
 * **İki yerleştirme kipi var:**
 *
 * - **Renkli şablon** → tıklanan nokta en yakın kenara oturur (`snapToEdge`).
 *   Düz bir kutuda pini gövdenin ortasına koymanın görsel bir karşılığı yok.
 * - **Görselli şablon** → pin tıklandığı YERE konur. Bir cihaz çiziminde
 *   klemensler kenarda değil, gövdenin içinde kümeler halinde durur; kenara
 *   yapıştırmak pini gerçek klemensinden kaydırırdı.
 *
 * Her iki kipte de `side` atanıyor — ama artık anlamı "hangi kenarda duruyor"
 * değil, **"kablo hangi yöne çıkıyor"**. Alttaki OUT klemenslerinin kablosu
 * aşağı, üstteki IN klemenslerininki yukarı çıkmalı.
 */

const SIDES: HandleSide[] = [HandleSide.Left, HandleSide.Right, HandleSide.Top, HandleSide.Bottom];
const DIRECTIONS: PinDirection[] = [
  PinDirection.Input,
  PinDirection.Output,
  PinDirection.Bidirectional,
  PinDirection.AnalogInput
];

const FUNCTIONS: PinFunction[] = [
  PinFunction.COM,
  PinFunction.NO,
  PinFunction.NC,
  PinFunction.VCC,
  PinFunction.GND,
  PinFunction.RS485_POS,
  PinFunction.RS485_NEG,
  PinFunction.RJ45,
  PinFunction.LED_Anode,
  PinFunction.LED_Cathode,
  PinFunction.Signal_In,
  PinFunction.Signal_Out,
  PinFunction.Analog_In,
  PinFunction.DryContact,
  PinFunction.Line_L,
  PinFunction.Neutral_N,
  PinFunction.Earth_PE,
  PinFunction.General
];
const VOLTAGES: VoltageLevel[] = [
  VoltageLevel.None,
  VoltageLevel.DC_12V,
  VoltageLevel.DC_24V,
  VoltageLevel.AC_220V,
  VoltageLevel.Signal_5V,
  VoltageLevel.Data
];

/**
 * Önizlemenin %100 yakınlaştırmadaki azami kenarı.
 *
 * Eskiden 320'ydi ve dar geliyordu: 800×200'lük yatay bir kart 320×80'e
 * düşüyor, klemensler ~13 px aralığa sıkışıyor ve hangi pinin hangi klemense
 * geldiği görülemiyordu. Şablonun genişliğini artırmak da işe yaramıyordu —
 * ölçek küçülüyor, kutu aynı kalıyordu.
 */
const PREVIEW_MAX = 560;

/** Yakınlaştırma adımları. Yoğun bir kartta klemens başına birkaç piksel yetmiyor. */
const ZOOM_STEPS = [1, 1.5, 2, 3, 4] as const;

/** Kutu bu yüksekliği aşarsa kaydırılır — sayfa boyu ekranı taşırmasın. */
const PREVIEW_VIEWPORT_MAX_HEIGHT = 520;

interface TemplatePinEditorProps {
  width: number;
  height: number;
  backgroundColor: string;
  backgroundImageUrl: string | null;
  pins: TemplatePinDraft[];
  selectedIndex: number | null;
  onSelect: (index: number | null) => void;
  onChange: (pins: TemplatePinDraft[]) => void;
}

export function TemplatePinEditor({
  width,
  height,
  backgroundColor,
  backgroundImageUrl,
  pins,
  selectedIndex,
  onSelect,
  onChange
}: TemplatePinEditorProps) {
  const boxRef = useRef<HTMLDivElement>(null);
  const imageSrc = templateImageSrc(backgroundImageUrl);

  /** Süren sürükleme. `moved` bayrağı tıklama ile sürüklemeyi ayırır. */
  const dragRef = useRef<{ index: number; moved: boolean } | null>(null);
  /** Sürükleme sonrası oluşan tek `click`'i yutmak için — bkz. `addPinAt`. */
  const suppressClickRef = useRef(false);

  const [zoom, setZoom] = useState<number>(1);

  // Onizleme ORANI korur: 400x100'luk bir sablonu kare gostermek, pin
  // konumlarini gercekte olmadiklari yerde gosterirdi.
  //
  // `min(..., 1)` YOK: kucuk bir sablonun da onizlemede buyutulmesi gerekiyor,
  // yoksa 80x60'lik bir cizime pin koymak imkansiz olurdu. Sinir yalnizca
  // KUCULTME yonunde degil, iki yonde de PREVIEW_MAX'e oturtmak.
  const fit = PREVIEW_MAX / Math.max(width, height, 1);
  const previewWidth = Math.max(width * fit * zoom, 40);
  const previewHeight = Math.max(height * fit * zoom, 40);

  /**
   * İmleç konumunu pin yerleşimine çevirir. Ekleme ve sürükleme AYNI yoldan
   * geçiyor; iki ayrı hesap, eklerken bir yere düşen pinin sürüklerken başka
   * yere düşmesi demek olurdu.
   */
  function placementAt(clientX: number, clientY: number) {
    const rect = boxRef.current?.getBoundingClientRect();
    if (!rect) return null;

    const x = (clientX - rect.left) / rect.width;
    const y = (clientY - rect.top) / rect.height;

    // Gorselli sablonda pin TIKLANDIGI YERE konur; `side` yine de en yakin
    // kenardan turetiliyor cunku kablonun cikis yonu gerekiyor. Kullanici
    // formdan degistirebilir.
    return imageSrc ? { ...snapToEdge(x, y), relativeX: clamp01(x), relativeY: clamp01(y) } : snapToEdge(x, y);
  }

  function addPinAt(event: MouseEvent<HTMLDivElement>) {
    // Surukleme bittiginde tarayici bir `click` de uretir ve imlec o an pinin
    // uzerinde degilse click KUTUYA ulasir — yani her surukleme sonunda
    // istenmeyen yeni bir pin dogardi. Bayrak o tek click'i yutuyor.
    if (suppressClickRef.current) {
      suppressClickRef.current = false;
      return;
    }

    const placement = placementAt(event.clientX, event.clientY);
    if (!placement) return;

    onChange([
      ...pins,
      {
        ...placement,
        // Ad benzersiz olmak ZORUNDA (IX_ComponentTemplatePin_ComponentTemplateId_Name).
        // Otomatik ad cakismayi bastan onluyor; kullanici sonra degistirebilir.
        name: nextPinName(pins),
        channelNumber: null,
        function: PinFunction.General,
        direction: PinDirection.Bidirectional,
        voltageLevel: null
      }
    ]);
    onSelect(pins.length);
  }

  function patchPin(index: number, patch: Partial<TemplatePinDraft>) {
    onChange(pins.map((pin, i) => (i === index ? { ...pin, ...patch } : pin)));
  }

  function startDrag(event: ReactPointerEvent<HTMLButtonElement>, index: number) {
    // stopPropagation: basis kutuya ulasmasin.
    event.stopPropagation();
    // Isaretci yakalama, imlec pinin disina cikinca da olaylarin gelmesini
    // saglar — kucuk bir hedefi surukleyebilmenin sarti.
    event.currentTarget.setPointerCapture(event.pointerId);
    dragRef.current = { index, moved: false };
    onSelect(index);
  }

  function dragTo(event: ReactPointerEvent<HTMLButtonElement>, index: number) {
    const drag = dragRef.current;
    if (drag?.index !== index) return;

    const placement = placementAt(event.clientX, event.clientY);
    if (!placement) return;

    drag.moved = true;

    // Serbest kipte `side` KORUNUYOR: kullanicinin sectigi kablo yonu, pini
    // birkac piksel oynatinca degismemeli. Kenara yapisik kipte ise kenar
    // degistirmek zaten surukleyerek yapilir, dolayisiyla `side` takip eder.
    patchPin(index, imageSrc ? { relativeX: placement.relativeX, relativeY: placement.relativeY } : placement);
  }

  function endDrag(event: ReactPointerEvent<HTMLButtonElement>, index: number) {
    const drag = dragRef.current;
    if (drag?.index !== index) return;

    event.currentTarget.releasePointerCapture(event.pointerId);
    // Yalnizca GERCEKTEN suruklendiyse click yutuluyor. Kipirdamayan bir basista
    // click zaten pinin uzerinde olusur ve onun kendi `stopPropagation`'i
    // kutuya ulasmasini engeller.
    suppressClickRef.current = drag.moved;
    dragRef.current = null;
  }

  const selected = selectedIndex != null ? pins[selectedIndex] : undefined;

  return (
    <div className='flex flex-col gap-4 lg:flex-row'>
      <div className='flex flex-col gap-2'>
        <div className='flex flex-wrap items-center justify-between gap-2'>
          <Label className='text-xs'>Önizleme</Label>
          <div className='flex items-center gap-1'>
            {/* Yakinlastirma yerlesim matematigine DOKUNMAZ: konum
                `getBoundingClientRect()` uzerinden 0..1 kesrine cevriliyor,
                dolayisiyla kutu ne kadar buyurse buyusun ayni noktaya ayni pin
                duser. Kesirle saklamanin karsiligi tam olarak bu. */}
            {ZOOM_STEPS.map(step => (
              <Button key={step} size='xs' variant={zoom === step ? 'default' : 'outline'} onClick={() => setZoom(step)}>
                {step === 1 ? 'Sığdır' : `${step}×`}
              </Button>
            ))}
          </div>
        </div>

        <span className='text-muted-foreground text-[10px]'>Eklemek için tıklayın, taşımak için sürükleyin</span>

        {/* Yakinlastirilmis kutu tasabilir; kaydirma kabi onu sayfa boyunu
            bozmadan gezilebilir kiliyor. */}
        <div className='overflow-auto rounded-md border' style={{ maxHeight: PREVIEW_VIEWPORT_MAX_HEIGHT }}>
          <div
            ref={boxRef}
            onClick={addPinAt}
            className='relative cursor-crosshair border-2 border-slate-400/70'
            style={{
              width: previewWidth,
              height: previewHeight,
              backgroundColor: safeCssColor(backgroundColor),
              color: readableTextColor(backgroundColor)
            }}>
            {imageSrc ? (
              // `object-fill` + kilitli en-boy orani: sablon kutusunun orani
              // gorselinkiyle ayni tutuldugu icin germe olmuyor ve pinler cizimin
              // uzerinde durdugu yerde kaliyor.
              <img src={imageSrc} alt='' aria-hidden draggable={false} className='pointer-events-none absolute inset-0 size-full object-fill' />
            ) : (
              <span className='pointer-events-none absolute inset-0 grid place-items-center text-[10px] opacity-50'>
                {width} × {height}
              </span>
            )}

            {pins.map((pin, index) => (
              <button
                key={index}
                type='button'
                // stopPropagation SART: aksi halde bir pine tiklamak kutuya da
                // tiklamis sayilir ve ustune ikinci bir pin eklenir.
                onClick={event => {
                  event.stopPropagation();
                  onSelect(index);
                }}
                onPointerDown={event => startDrag(event, index)}
                onPointerMove={event => dragTo(event, index)}
                onPointerUp={event => endDrag(event, index)}
                // Iptal (ESC, isaretci kaybi) surukleme durumunu temizlemeli;
                // yoksa bayrak asili kalir ve sonraki tiklama yutulur.
                onPointerCancel={event => endDrag(event, index)}
                title={`${pin.name} — sürükleyerek taşıyın`}
                className={cn(
                  'absolute size-3 cursor-grab rounded-full border-2 border-white shadow active:cursor-grabbing',
                  index === selectedIndex ? 'bg-sky-500 ring-2 ring-sky-500/40' : 'bg-slate-600'
                )}
                // Canvas ile AYNI fonksiyon: iki eksen birden. Yerleşim iki yere
                // bölünmüşken sağ pinler sola, alt pinler üste düşüyordu.
                style={pinPlacementStyle(pin)}
              />
            ))}
          </div>
        </div>

        <p className='text-muted-foreground text-[10px]'>
          {pins.length === 0 ? 'Henüz pin yok.' : `${pins.length} pin — düzenlemek için tıklayın, taşımak için sürükleyin.`}
        </p>
      </div>

      <div className='min-w-0 flex-1'>
        {selected && selectedIndex != null ? (
          <PinForm
            key={selectedIndex}
            pin={selected}
            index={selectedIndex}
            freePlacement={imageSrc != null}
            onChange={patch => patchPin(selectedIndex, patch)}
            onDelete={() => {
              onChange(pins.filter((_, i) => i !== selectedIndex));
              onSelect(null);
            }}
          />
        ) : (
          <p className='text-muted-foreground rounded-md border border-dashed p-4 text-xs'>
            Bir pin seçin ya da kutuya tıklayarak yeni pin ekleyin.{' '}
            {imageSrc
              ? 'Görsel eklendiği için pin tıkladığınız yere konur — klemensin tam üstüne koyabilirsiniz.'
              : 'Pin, tıkladığınız noktaya en yakın kenara oturur.'}
          </p>
        )}
      </div>
    </div>
  );
}

function PinForm({
  pin,
  index,
  freePlacement,
  onChange,
  onDelete
}: {
  pin: TemplatePinDraft;
  index: number;
  freePlacement: boolean;
  onChange: (patch: Partial<TemplatePinDraft>) => void;
  onDelete: () => void;
}) {
  // Kenara yapisik kipte konum TEK eksendir: sol/sag kenarda yalnizca dikey,
  // ust/alt kenarda yalnizca yatay. Diger ekseni de gostermek, degistirdiginde
  // hicbir sey olmayan bir kontrol sunmak olurdu.
  const isVerticalEdge = pin.side === HandleSide.Left || pin.side === HandleSide.Right;

  return (
    <div className='flex flex-col gap-3 rounded-md border p-3'>
      <div className='flex items-center justify-between gap-2'>
        <p className='text-sm font-medium'>Pin {index + 1}</p>
        <Button
          size='xs'
          variant='outline'
          className='text-destructive hover:text-destructive border-destructive/40 hover:bg-destructive/10'
          onClick={onDelete}>
          <Trash2Icon />
          Sil
        </Button>
      </div>

      <Field label='Ad' htmlFor='pin-name' hint='Şablon içinde benzersiz olmalı.'>
        <Input id='pin-name' className='h-7' value={pin.name} onChange={e => onChange({ name: e.target.value })} />
      </Field>

      <Field label='Kablo yönü' hint='Kablonun pinden hangi yöne çıkacağı.'>
        <EnumSelect
          value={pin.side}
          options={SIDES}
          labels={HandleSideLabels}
          onChange={side => {
            // Serbest kipte `side` YALNIZCA kablo yonudur, pini KIPIRDATMAZ:
            // klemensin uzerine oturttugunuz pin, yonunu degistirdiginizde
            // kenara sicramamali.
            if (freePlacement) {
              onChange({ side });
              return;
            }
            // Kenara yapisik kipte eksen de degisir: sol kenarda `relativeX`
            // her zaman 0'dir. Eski degeri birakmak pini kenarin disinda
            // gosteren bir kayit uretirdi.
            onChange(snapToEdgeForSide(side, isVerticalEdge ? pin.relativeY : pin.relativeX));
          }}
        />
      </Field>

      {freePlacement ? (
        <div className='grid grid-cols-2 gap-2'>
          <Field label='X' htmlFor='pin-x' hint='0 = sol, 1 = sağ'>
            <Input
              id='pin-x'
              type='number'
              className='h-7'
              min={0}
              max={1}
              step={0.01}
              value={pin.relativeX}
              onChange={e => onChange({ relativeX: clamp01(Number(e.target.value)) })}
            />
          </Field>
          <Field label='Y' htmlFor='pin-y' hint='0 = üst, 1 = alt'>
            <Input
              id='pin-y'
              type='number'
              className='h-7'
              min={0}
              max={1}
              step={0.01}
              value={pin.relativeY}
              onChange={e => onChange({ relativeY: clamp01(Number(e.target.value)) })}
            />
          </Field>
        </div>
      ) : (
        <Field label={isVerticalEdge ? 'Dikey konum' : 'Yatay konum'} htmlFor='pin-along' hint='0 = başlangıç, 1 = son'>
          <Input
            id='pin-along'
            type='number'
            className='h-7'
            min={0}
            max={1}
            step={0.05}
            value={isVerticalEdge ? pin.relativeY : pin.relativeX}
            onChange={e => {
              const value = clamp01(Number(e.target.value));
              onChange(isVerticalEdge ? { relativeY: value } : { relativeX: value });
            }}
          />
        </Field>
      )}

      <div className='grid grid-cols-2 gap-2'>
        <Field label='Fonksiyon'>
          <EnumSelect value={pin.function} options={FUNCTIONS} labels={PinFunctionLabels} onChange={fn => onChange({ function: fn })} />
        </Field>
        <Field label='Yön'>
          <EnumSelect value={pin.direction} options={DIRECTIONS} labels={PinDirectionLabels} onChange={direction => onChange({ direction })} />
        </Field>
      </div>

      <div className='grid grid-cols-2 gap-2'>
        <Field label='Gerilim' hint='Boş = belirtilmemiş'>
          {/* `null` GECERLI bir deger: baglanti dogrulamasi yalnizca IKI UC da
              doluysa seviye karsilastiriyor (bkz. connection-rules.ts). */}
          <EnumSelect
            value={pin.voltageLevel ?? NO_VOLTAGE}
            options={[NO_VOLTAGE, ...VOLTAGES]}
            labels={{ [NO_VOLTAGE]: 'Belirtilmemiş', ...VoltageLevelLabels } as Record<number, string>}
            onChange={value => onChange({ voltageLevel: value === NO_VOLTAGE ? null : (value as VoltageLevel) })}
          />
        </Field>
      </div>

      <Field label='Kanal no' htmlFor='pin-channel' hint='Boş = telemetri kanalı yok.'>
        <Input
          id='pin-channel'
          type='number'
          className='h-7'
          min={0}
          value={pin.channelNumber ?? ''}
          onChange={e => onChange({ channelNumber: e.target.value === '' ? null : Number(e.target.value) })}
        />
      </Field>
    </div>
  );
}

/**
 * "Belirtilmemiş" için sentinel.
 *
 * `VoltageLevel.None` KULLANILAMAZ: o, sunucuda 0 değerine sahip GERÇEK bir enum
 * üyesi ("gerilimsiz") ve `null` ("bilinmiyor") ile aynı şey değil. Bağlantı
 * doğrulaması ikisini farklı ele alıyor.
 */
const NO_VOLTAGE = -1;

function snapToEdgeForSide(side: HandleSide, along: number): Partial<TemplatePinDraft> {
  const value = clamp01(along);
  switch (side) {
    case HandleSide.Left:
      return { side, relativeX: 0, relativeY: value };
    case HandleSide.Right:
      return { side, relativeX: 1, relativeY: value };
    case HandleSide.Top:
      return { side, relativeX: value, relativeY: 0 };
    case HandleSide.Bottom:
      return { side, relativeX: value, relativeY: 1 };
  }
}

function clamp01(value: number): number {
  if (!Number.isFinite(value)) return 0.5;
  return Math.min(Math.max(value, 0), 1);
}

/** "PIN-1", "PIN-2", … — ad benzersizliği DB kısıtı, çakışmayı baştan önlüyoruz. */
function nextPinName(pins: TemplatePinDraft[]): string {
  const taken = new Set(pins.map(p => p.name.trim().toLocaleLowerCase('tr')));
  for (let index = pins.length + 1; ; index++) {
    const candidate = `PIN-${index}`;
    if (!taken.has(candidate.toLocaleLowerCase('tr'))) return candidate;
  }
}

// ───────────────────────────────────────────────────────── ortak parçalar

function Field({ label, htmlFor, hint, children }: { label: string; htmlFor?: string; hint?: string; children: React.ReactNode }) {
  return (
    <div className='flex flex-col gap-1.5'>
      <Label htmlFor={htmlFor} className='text-xs'>
        {label}
      </Label>
      {children}
      {hint && <p className='text-muted-foreground text-[10px]'>{hint}</p>}
    </div>
  );
}

/** Sayısal enum seçici — enum'lar tel üzerinde SAYI olarak gider. */
function EnumSelect<T extends number>({
  value,
  options,
  labels,
  onChange
}: {
  value: T;
  options: T[];
  labels: Record<T, string>;
  onChange: (value: T) => void;
}) {
  return (
    <Select
      value={value}
      onValueChange={next => {
        if (next != null) onChange(next as T);
      }}>
      <SelectTrigger size='sm' className='w-full'>
        <SelectValue>{labels[value]}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {options.map(option => (
          <SelectItem key={option} value={option}>
            {labels[option]}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
