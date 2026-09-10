import { useRef, useState, type ChangeEvent } from 'react';
import { ImageIcon, PlusIcon, XIcon } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { DeviceType, DeviceTypeLabels } from '@/models/enums';
import type { ComponentTemplateCreateRequest, ComponentTemplatePaletteDto, TemplatePinDraft } from '@/models/componentTemplate';
import { componentTemplateCreateSchema } from '@/models/componentTemplate';
import { useComponentTemplatePalette, useCreateTemplate, useUploadTemplateImage } from '@/hooks/use-component-templates';
import { readableTextColor, safeCssColor } from '@/lib/diagram/colors';
import { aspectRatio, fitToTemplate, lockedSide, readImageSize, templateImageSrc } from '@/lib/diagram/template-image';
import { TemplatePinEditor } from './template-pin-editor';

/**
 * Palet yazarlığı — şablon listesi + yeni şablon formu.
 *
 * Şablon ve pinleri TEK istekte gider (`POST /api/ComponentTemplate`). Generic
 * CRUD ile yazmak mümkün değildi: pin eklemek şablonun önce var olmasını
 * gerektiriyor ve ikinci adım yarıda kalırsa geriye pinsiz — dolayısıyla kablo
 * bağlanamayan — bir şablon kalırdı.
 *
 * Sözleşme: `docs/api-contract/10-component-template.md`
 */
export default function ComponentTemplates() {
  const { data, isPending, isError, error } = useComponentTemplatePalette();
  const [isCreating, setIsCreating] = useState(false);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Şablonlar</h1>
          <p className='text-muted-foreground text-sm'>Paletteki bileşenler. Yeni şablon yazıp diyagramlarda kullanabilirsiniz.</p>
        </div>
        {!isCreating && (
          <Button size='sm' onClick={() => setIsCreating(true)}>
            <PlusIcon />
            Yeni şablon
          </Button>
        )}
      </div>

      {isCreating && <TemplateForm onDone={() => setIsCreating(false)} />}

      {isError && <p className='text-destructive text-sm'>{error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3'>
        {isPending && Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className='h-24 w-full rounded-xl' />)}
        {data?.map(template => <TemplateCard key={template.id} template={template} />)}
      </div>

      {data?.length === 0 && !isCreating && (
        <Card>
          <CardContent className='text-muted-foreground py-8 text-center text-sm'>Aktif şablon yok.</CardContent>
        </Card>
      )}
    </div>
  );
}

function TemplateCard({ template }: { template: ComponentTemplatePaletteDto }) {
  return (
    <Card>
      <CardContent className='flex items-center gap-3 py-4'>
        <div
          // Kartta sablonun GERCEK en-boy orani gosteriliyor: palet ile canvas
          // arasindaki zihinsel eslesmeyi kuran sey bu.
          className='grid size-12 shrink-0 place-items-center rounded border text-[10px] font-semibold'
          style={{ backgroundColor: safeCssColor(template.backgroundColor), color: readableTextColor(template.backgroundColor) }}>
          {template.pins.length}
        </div>
        <div className='min-w-0'>
          <p className='truncate text-sm font-medium'>{template.name}</p>
          <p className='text-muted-foreground truncate text-xs'>
            {DeviceTypeLabels[template.deviceTypeId]} · {template.width} × {template.height}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── yeni şablon

const DEVICE_TYPES: DeviceType[] = [
  DeviceType.ControlModule,
  DeviceType.InputModule,
  DeviceType.OutputModule,
  DeviceType.LedModule,
  DeviceType.TerminalBlock,
  DeviceType.Sensor,
  DeviceType.Peripheral,
  DeviceType.PowerSupply,
  DeviceType.MeasurementDevice,
  DeviceType.CardReader,
  DeviceType.Mains,
  DeviceType.CircuitBreaker
];

const DEFAULT_DRAFT = {
  name: '',
  deviceTypeId: DeviceType.ControlModule as number,
  width: 200,
  height: 160,
  backgroundColor: '#f0f0f0',
  backgroundImageUrl: null as string | null,
  pins: [] as TemplatePinDraft[]
};

/** Dosya seçicinin kabul ettikleri — sunucudaki beyaz listeyle aynı. */
const ACCEPTED_IMAGE_TYPES = '.png,.jpg,.jpeg,.webp,.svg';

function TemplateForm({ onDone }: { onDone: () => void }) {
  const [draft, setDraft] = useState(DEFAULT_DRAFT);
  const [selectedPin, setSelectedPin] = useState<number | null>(null);
  const [issue, setIssue] = useState<string | null>(null);
  /**
   * Görselin en-boy oranı. `null` = görsel yok, iki kenar da serbest.
   *
   * Kilidin sebebi pinlerle ilgili: pinler `0..1` kesri olarak saklanıyor, yani
   * kutunun oranı değişirse görsel esner ama pinler esnemez ve her biri
   * klemensinden kayar. Kayma ancak şablon kullanılırken fark edilir.
   */
  const [ratio, setRatio] = useState<number | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const mutation = useCreateTemplate();
  const upload = useUploadTemplateImage();

  function setSize(axis: 'width' | 'height', raw: number) {
    const value = Number.isFinite(raw) ? Math.max(Math.round(raw), 1) : 1;
    if (ratio == null) {
      setDraft(current => ({ ...current, [axis]: value }));
      return;
    }
    // Kilitli: bir kenar yazılır, diğeri hesaplanır.
    setDraft(current =>
      axis === 'width'
        ? { ...current, width: value, height: lockedSide(value, ratio) }
        : { ...current, height: value, width: Math.max(Math.round(value * ratio), 1) }
    );
  }

  function pickImage(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Aynı dosyayı ikinci kez seçmek `change` üretmez; girdiyi sıfırlamak bunu
    // düzeltiyor (kullanıcı görseli kaldırıp aynısını geri koyabilmeli).
    event.target.value = '';
    if (!file) return;

    upload.mutate(file, {
      onSuccess: async result => {
        const src = templateImageSrc(result.url)!;
        const natural = await readImageSize(src);
        const fitted = fitToTemplate(natural);

        setRatio(aspectRatio(fitted));
        setDraft(current => ({ ...current, backgroundImageUrl: result.url, width: fitted.width, height: fitted.height }));

        // Pinler zaten konmuşsa oran değişmiş olabilir ve hepsi kayar. Sessizce
        // taşımak yerine söylüyoruz: hangi pinin nereye ait olduğunu yalnızca
        // kullanıcı bilir.
        if (draft.pins.length > 0) {
          toast.warning('Görsel değişti; kutu oranı da değişmiş olabilir. Pin konumlarını kontrol edin.');
        }
      }
    });
  }

  function clearImage() {
    setRatio(null);
    setDraft(current => ({ ...current, backgroundImageUrl: null }));
  }

  function submit() {
    // Zod sinirlari sunucu validator'iyla birebir. Burada yakalamak, sunucuya
    // gidip 400 ile donmekten hizli — ve bu ucta 400 demek gonderinin TAMAMININ
    // (pinler dahil) reddedilmesi demek.
    const parsed = componentTemplateCreateSchema.safeParse(draft);
    if (!parsed.success) {
      setIssue(parsed.error.issues[0]?.message ?? 'Geçersiz şablon');
      return;
    }

    setIssue(null);
    mutation.mutate(parsed.data as ComponentTemplateCreateRequest, {
      onSuccess: () => {
        setDraft(DEFAULT_DRAFT);
        setSelectedPin(null);
        setRatio(null);
        onDone();
      }
    });
  }

  return (
    <Card>
      <CardContent className='flex flex-col gap-4 py-4'>
        <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-4'>
          <Field label='Ad' htmlFor='template-name'>
            <Input id='template-name' className='h-8' value={draft.name} onChange={e => setDraft({ ...draft, name: e.target.value })} />
          </Field>

          <Field label='Cihaz tipi'>
            <Select value={draft.deviceTypeId} onValueChange={value => value != null && setDraft({ ...draft, deviceTypeId: value as number })}>
              <SelectTrigger size='sm' className='w-full'>
                <SelectValue>{DeviceTypeLabels[draft.deviceTypeId as DeviceType]}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                {DEVICE_TYPES.map(type => (
                  <SelectItem key={type} value={type}>
                    {DeviceTypeLabels[type]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>

          <div className='grid grid-cols-2 gap-2'>
            <Field label='Genişlik' htmlFor='template-width'>
              <Input
                id='template-width'
                type='number'
                className='h-8'
                min={1}
                value={draft.width}
                onChange={e => setSize('width', Number(e.target.value))}
              />
            </Field>
            <Field label='Yükseklik' htmlFor='template-height'>
              <Input
                id='template-height'
                type='number'
                className='h-8'
                min={1}
                value={draft.height}
                onChange={e => setSize('height', Number(e.target.value))}
              />
            </Field>
          </div>

          {draft.backgroundImageUrl ? (
            <Field label='Görsel'>
              <div className='flex items-center gap-2'>
                <span className='text-muted-foreground truncate text-xs'>Eklendi</span>
                <Button size='xs' variant='outline' onClick={clearImage}>
                  <XIcon />
                  Kaldır
                </Button>
              </div>
            </Field>
          ) : (
            <Field label='Renk' htmlFor='template-color'>
              <div className='flex items-center gap-2'>
                {/* Renk sunucuda da `#RRGGBB` dizesi; `<input type=color>` zaten
                    bu bicimi uretiyor, arada donusum yok. */}
                <Input
                  id='template-color'
                  type='color'
                  className='h-8 w-14 p-1'
                  value={draft.backgroundColor}
                  onChange={e => setDraft({ ...draft, backgroundColor: e.target.value })}
                />
                <span className='text-muted-foreground font-mono text-xs'>{draft.backgroundColor}</span>
              </div>
            </Field>
          )}
        </div>

        <div className='flex flex-wrap items-center gap-3 rounded-md border border-dashed p-3'>
          <Button size='sm' variant='outline' disabled={upload.isPending} onClick={() => fileRef.current?.click()}>
            <ImageIcon />
            {upload.isPending ? 'Yükleniyor…' : draft.backgroundImageUrl ? 'Görseli değiştir' : 'Görsel ekle'}
          </Button>
          <input ref={fileRef} type='file' accept={ACCEPTED_IMAGE_TYPES} className='hidden' onChange={pickImage} />

          <p className='text-muted-foreground text-xs'>
            {draft.backgroundImageUrl
              ? 'En-boy oranı görsele kilitli: bir kenarı değiştirince diğeri kendiliğinden gelir. Pinler 0..1 kesri olarak saklandığı için oran korunmazsa klemenslerinden kayarlar.'
              : 'PNG, SVG, JPG veya WEBP. Görsel eklendiğinde boyut ondan türetilir ve pinleri istediğiniz noktaya koyabilirsiniz.'}
          </p>
        </div>

        <div className='border-t pt-4'>
          <TemplatePinEditor
            width={draft.width}
            height={draft.height}
            backgroundColor={draft.backgroundColor}
            backgroundImageUrl={draft.backgroundImageUrl}
            pins={draft.pins}
            selectedIndex={selectedPin}
            onSelect={setSelectedPin}
            onChange={pins => setDraft({ ...draft, pins })}
          />
        </div>

        {issue && <p className='text-destructive text-xs'>{issue}</p>}

        <div className='flex justify-end gap-2 border-t pt-4'>
          <Button size='sm' variant='outline' onClick={onDone} disabled={mutation.isPending}>
            Vazgeç
          </Button>
          <Button size='sm' onClick={submit} disabled={mutation.isPending}>
            {mutation.isPending ? 'Kaydediliyor…' : 'Şablonu oluştur'}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

function Field({ label, htmlFor, children }: { label: string; htmlFor?: string; children: React.ReactNode }) {
  return (
    <div className='flex flex-col gap-1.5'>
      <Label htmlFor={htmlFor} className='text-xs'>
        {label}
      </Label>
      {children}
    </div>
  );
}
