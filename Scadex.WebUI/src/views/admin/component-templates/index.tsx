import { useMemo, useRef, useState, type ChangeEvent } from 'react';
import { useSearchParams } from 'react-router';
import { ImageIcon, PlusIcon, XIcon } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { DataTable, type DataTableColumn } from '@/components/custom/data-table';
import { DeviceType, DeviceTypeLabels } from '@/models/enums';
import type { ComponentTemplateCreateRequest, ComponentTemplateListItemDto, TemplatePinDraft } from '@/models/componentTemplate';
import { componentTemplateCreateSchema } from '@/models/componentTemplate';
import { useCreateTemplate, useTemplatesByType, useTemplateTypeCounts, useUploadTemplateImage } from '@/hooks/use-component-templates';
import { safeCssColor } from '@/lib/diagram/colors';
import { DEVICE_TYPE_ICON, DEVICE_TYPE_ORDER } from '@/lib/diagram/device-type-icon';
import { cn } from '@/lib/utils';
import { aspectRatio, fitToTemplate, lockedSide, readImageSize, templateImageSrc } from '@/lib/diagram/template-image';
import { TemplatePinEditor } from './template-pin-editor';

/** `?type=` değerini doğrular; bilinmeyen/boş değer = seçim yok. */
function parseDeviceType(raw: string | null): DeviceType | null {
  const value = Number(raw);
  return raw != null && DEVICE_TYPE_ORDER.includes(value as DeviceType) ? (value as DeviceType) : null;
}

/**
 * Palet yazarlığı — tip kartları + seçili tipin şablonları + yeni şablon formu.
 *
 * Tüm şablonlar tek listede GÖSTERİLMEZ: önce tip seçilir, tablo yalnızca o tipin şablonlarını
 * listeler. Seçim `?type=` sorgu parametresindedir — yenilemede ve paylaşılan bağlantıda korunur.
 * Tip kartlarının ikonları diyagram paletinin ağacıyla aynı haritadan gelir (`DEVICE_TYPE_ICON`).
 *
 * Şablon ve pinleri TEK istekte gider (`POST /api/ComponentTemplate`). Generic
 * CRUD ile yazmak mümkün değildi: pin eklemek şablonun önce var olmasını
 * gerektiriyor ve ikinci adım yarıda kalırsa geriye pinsiz — dolayısıyla kablo
 * bağlanamayan — bir şablon kalırdı.
 *
 * Sözleşme: `docs/api-contract/10-component-template.md`
 */
export default function ComponentTemplates() {
  const [isCreating, setIsCreating] = useState(false);
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedType = parseDeviceType(searchParams.get('type'));

  // İki ayrı, hafif istek: kartlar yalnızca sayıyı, tablo yalnızca seçili tipi çeker. Diyagram
  // paletinin ucu (`/palette`) burada KULLANILMAZ — tüm şablonları pin şemalarıyla döndürür.
  const typeCounts = useTemplateTypeCounts();
  const templates = useTemplatesByType(selectedType);

  const countByType = useMemo(() => new Map(typeCounts.data?.map(item => [item.deviceTypeId, item.count])), [typeCounts.data]);
  const loadError = typeCounts.error ?? templates.error;

  // `replace`: tip kartları arasında gezinmek geçmişi doldurmasın, "geri" önceki SAYFAYA dönsün.
  const selectType = (type: DeviceType | null) =>
    setSearchParams(type == null ? {} : { type: String(type) }, { replace: true });

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Şablonlar</h1>
          <p className='text-muted-foreground text-sm'>Paletteki bileşenler. Şablonlarını görmek için bir cihaz tipi seçin.</p>
        </div>
        {!isCreating && (
          <Button size='sm' onClick={() => setIsCreating(true)}>
            <PlusIcon />
            Yeni şablon
          </Button>
        )}
      </div>

      {/* Form seçili tiple açılır; oluşturulan şablonun tipi seçilir ki yeni kayıt tabloda hemen görünsün. */}
      {isCreating && (
        <TemplateForm
          initialDeviceType={selectedType}
          onDone={createdType => {
            setIsCreating(false);
            if (createdType != null) selectType(createdType);
          }}
        />
      )}

      {loadError && <p className='text-destructive text-sm'>{loadError.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5'>
        {DEVICE_TYPE_ORDER.map(type => (
          <DeviceTypeCard
            key={type}
            deviceType={type}
            // Sunucu şablonu olmayan tipi göndermez → 0.
            count={typeCounts.isPending ? null : (countByType.get(type) ?? 0)}
            selected={type === selectedType}
            // Seçili karta tekrar basmak seçimi kaldırır.
            onSelect={() => selectType(type === selectedType ? null : type)}
          />
        ))}
      </div>

      {selectedType == null ? (
        <p className='text-muted-foreground rounded-xl border border-dashed py-8 text-center text-sm'>Şablonlarını görmek için yukarıdan bir cihaz tipi seçin.</p>
      ) : (
        <section className='flex flex-col gap-2'>
          <h2 className='text-sm font-semibold'>{DeviceTypeLabels[selectedType]} şablonları</h2>
          {/* Satır eylemi yok: şablonların güncelleme/silme ucu bulunmuyor, bu yüzden "İşlemler" kolonu da yok.
              "Cihaz tipi" kolonu da yok — tablo zaten tek tipe süzülü. */}
          <DataTable
            columns={COLUMNS}
            rows={templates.data}
            getRowKey={template => template.id}
            isLoading={templates.isPending}
            emptyMessage='Bu tipte aktif şablon yok.'
          />
        </section>
      )}
    </div>
  );
}

function DeviceTypeCard({
  deviceType,
  count,
  selected,
  onSelect
}: {
  deviceType: DeviceType;
  /** `null` = sayılar henüz yükleniyor. */
  count: number | null;
  selected: boolean;
  onSelect: () => void;
}) {
  const Icon = DEVICE_TYPE_ICON[deviceType];

  return (
    <button
      type='button'
      aria-pressed={selected}
      onClick={onSelect}
      className={cn(
        'bg-card hover:bg-accent focus-visible:ring-ring flex items-center gap-3 rounded-xl p-3 text-left ring-1 ring-foreground/10 transition-colors outline-none focus-visible:ring-2',
        selected && 'bg-accent ring-primary ring-2'
      )}>
      <span
        className={cn(
          'grid size-10 shrink-0 place-items-center rounded-lg transition-colors',
          selected ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'
        )}>
        <Icon className='size-5' />
      </span>
      <span className='min-w-0'>
        <span className='block truncate text-sm font-medium'>{DeviceTypeLabels[deviceType]}</span>
        <span className='text-muted-foreground block text-xs'>{count == null ? '…' : count === 0 ? 'Şablon yok' : `${count} şablon`}</span>
      </span>
    </button>
  );
}

const COLUMNS: DataTableColumn<ComponentTemplateListItemDto>[] = [
  {
    id: 'name',
    header: 'Şablon',
    cell: template => (
      <div className='flex items-center gap-3'>
        <TemplatePreview template={template} />
        <span className='font-medium'>{template.name}</span>
        {template.isSystemTemplate && <Badge variant='outline'>Sistem</Badge>}
      </div>
    )
  },
  { id: 'size', header: 'Boyut', cell: template => `${template.width} × ${template.height}` },
  { id: 'pins', header: 'Pin', className: 'text-right tabular-nums', cell: template => template.pinCount },
  {
    id: 'monitorable',
    header: 'Ağ izlemesi',
    cell: template => (template.isMonitorable ? <Badge variant='outline'>Açılabilir</Badge> : <span className='text-muted-foreground'>—</span>)
  }
];

/** Ön izleme kutusunun sınırı (px). Şablon bu kutuya ORANI korunarak sığdırılır. */
const PREVIEW_MAX_WIDTH = 64;
const PREVIEW_MAX_HEIGHT = 40;

/**
 * Şablonun tuvaldeki görünümünün küçültülmüş hâli — yalnızca görsel: pin çizilmez, etkileşim yok.
 *
 * Kutu şablonun `width × height` oranındadır; görsel `object-fill` ile basılır (tuvaldeki
 * `TemplateNode` ile aynı gerekçe: oran görsele kilitli, germe olmaz). Görseli olmayan şablonda
 * yalnızca arka plan rengi görünür.
 */
function TemplatePreview({ template }: { template: ComponentTemplateListItemDto }) {
  const imageSrc = templateImageSrc(template.backgroundImageUrl);
  const ratio = aspectRatio(template);
  const fitsByWidth = ratio >= PREVIEW_MAX_WIDTH / PREVIEW_MAX_HEIGHT;
  const width = fitsByWidth ? PREVIEW_MAX_WIDTH : Math.max(Math.round(PREVIEW_MAX_HEIGHT * ratio), 4);
  const height = fitsByWidth ? Math.max(Math.round(PREVIEW_MAX_WIDTH / ratio), 4) : PREVIEW_MAX_HEIGHT;

  return (
    // Sabit genişlikli yuva: oranı farklı şablonlarda adlar aynı hizadan başlasın.
    <span className='grid shrink-0 place-items-center' style={{ width: PREVIEW_MAX_WIDTH, height: PREVIEW_MAX_HEIGHT }}>
      <span
        className='relative overflow-hidden rounded-sm border shadow-xs'
        style={{ width, height, backgroundColor: safeCssColor(template.backgroundColor) }}>
        {imageSrc && (
          <img
            src={imageSrc}
            alt=''
            aria-hidden
            loading='lazy'
            decoding='async'
            draggable={false}
            className='pointer-events-none absolute inset-0 size-full object-fill select-none'
          />
        )}
      </span>
    </span>
  );
}

// ─────────────────────────────────────────────────────────── yeni şablon

const DEFAULT_DRAFT = {
  name: '',
  deviceTypeId: DeviceType.ControlModule as number,
  width: 200,
  height: 160,
  backgroundColor: '#f0f0f0',
  backgroundImageUrl: null as string | null,
  isMonitorable: false,
  pins: [] as TemplatePinDraft[]
};

/** Dosya seçicinin kabul ettikleri — sunucudaki beyaz listeyle aynı. */
const ACCEPTED_IMAGE_TYPES = '.png,.jpg,.jpeg,.webp,.svg';

function TemplateForm({
  initialDeviceType,
  onDone
}: {
  initialDeviceType: DeviceType | null;
  /** Vazgeçilince argümansız, oluşturulunca şablonun tipiyle çağrılır. */
  onDone: (createdType?: DeviceType) => void;
}) {
  const [draft, setDraft] = useState(() => ({ ...DEFAULT_DRAFT, deviceTypeId: initialDeviceType ?? DEFAULT_DRAFT.deviceTypeId }));
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
        onDone(parsed.data.deviceTypeId as DeviceType);
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
                {DEVICE_TYPE_ORDER.map(type => (
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

        {/* Şablon yalnızca ağ izlemesine İZİN verir; izlemeyi açmak cihaz başına diyagramda yapılır. Şablonların güncelleme
            ucu olmadığı için bu seçim oluşturmada kalıcıdır. */}
        <div className='flex items-start justify-between gap-3 rounded-md border p-3'>
          <div className='flex flex-col gap-1'>
            <Label htmlFor='template-monitorable' className='text-xs'>
              Ağ izlemesi
            </Label>
            <p className='text-muted-foreground text-xs'>
              Açıksa bu şablondan eklenen cihazda IP ve port girilip TCP ile yoklama açılabilir (SCADA kartı, POS, bilgisayar gibi ağ cihazları).
              Klemens, sigorta gibi ağı olmayan parçalarda kapalı bırakın.
            </p>
          </div>
          <Switch id='template-monitorable' checked={draft.isMonitorable} onCheckedChange={isMonitorable => setDraft({ ...draft, isMonitorable })} />
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
          <Button size='sm' variant='outline' onClick={() => onDone()} disabled={mutation.isPending}>
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
