import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';

// Özellikler panelinin ortak girdileri (cihaz / kablo / not formları).

/**
 * Konum alanlarının sınırı. Sunucuda koordinat için bir kısıt YOK; bu yalnızca yazım hatasına karşı bir arayüz
 * korkuluğu — kaydırılmış bir tuşla cihazı canvas'ın milyonlarca piksel ötesine göndermek, onu geri bulmayı imkânsız kılardı.
 */
export const COORDINATE_LIMIT = 100_000;

// ─────────────────────────────────────────────────────────────── katman

/**
 * Katman, kilit ve görünürlük — cihazlar ve notlar için AYNI kontrol seti.
 *
 * Üç alan da her iki DTO'da var ve anlamları birebir aynı; ikisine ayrı arayüz
 * yazmak, birinde düzeltilen bir davranışın diğerinde eski kalması demek olurdu.
 */
export function LayerControls({
  idPrefix,
  zIndex,
  isLocked,
  isVisible,
  onChange
}: {
  idPrefix: string;
  zIndex: number;
  isLocked: boolean;
  isVisible: boolean;
  onChange: (patch: { zIndex?: number; isLocked?: boolean; isVisible?: boolean }) => void;
}) {
  return (
    <div className='flex flex-col gap-3 border-t pt-3'>
      <Field label='Katman' htmlFor={`${idPrefix}-z`} hint='Büyük sayı üstte durur.'>
        <NumberInput id={`${idPrefix}-z`} value={zIndex} min={-999} max={999} onCommit={next => onChange({ zIndex: next })} />
      </Field>

      <div className='flex items-center justify-between gap-2'>
        <Label htmlFor={`${idPrefix}-locked`} className='text-xs'>
          Kilitli
        </Label>
        <Switch id={`${idPrefix}-locked`} checked={isLocked} onCheckedChange={locked => onChange({ isLocked: locked })} />
      </div>

      <div className='flex items-center justify-between gap-2'>
        <Label htmlFor={`${idPrefix}-visible`} className='text-xs'>
          Görünür
        </Label>
        <Switch id={`${idPrefix}-visible`} checked={isVisible} onCheckedChange={visible => onChange({ isVisible: visible })} />
      </div>

      {/* Gizlenen (ve kilitli notlarda: kilitlenen) öğe canvas'ta seçilemez,
          dolayısıyla bu panele bir daha getirilemez. Kullanıcının kendini
          kilitlediği bir duruma düşmemesi için çıkış yolu seçim boşken
          listeleniyor — bkz. `UnreachableItems`. */}
      {!isVisible && <p className='text-muted-foreground text-[10px]'>Gizli öğeler canvas'ta seçilemez; seçimi boşaltıp panelden geri getirebilirsiniz.</p>}
    </div>
  );
}

export function ColorField({ label, id, value, onCommit }: { label: string; id: string; value: string; onCommit: (value: string) => void }) {
  return (
    <div className='flex items-center justify-between gap-2'>
      <Label htmlFor={id} className='text-xs'>
        {label}
      </Label>
      <div className='flex items-center gap-2'>
        <ColorInput id={id} value={value} onCommit={onCommit} />
        <span className='text-muted-foreground font-mono text-xs'>{value}</span>
      </div>
    </div>
  );
}

// ───────────────────────────────────────────────────────── ortak parçalar

export function Field({ label, htmlFor, hint, children }: { label: string; htmlFor?: string; hint?: string; children: React.ReactNode }) {
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

export function ReadOnly({ term, value }: { term: string; value: string }) {
  return (
    <>
      <dt>{term}</dt>
      <dd className='truncate text-right' title={value}>
        {value}
      </dd>
    </>
  );
}

/**
 * Metin girdisi — commit `blur` ve `Enter`'da.
 *
 * Her tuş vuruşunda `onChange` çağırmak, her harfi ayrı bir düzenleme (kablo ve
 * notta ayrı bir istek) yapardı; ara hâller ("Pan", "Pano", "Pano-") kullanıcı için
 * anlamsızdır.
 *
 * Çağıran taraf `key={id}` verir: seçim değişince bileşen remount olur ve taslak
 * taze değerle doğar. Bunu bir effect'le yapmak fazladan bir render turu üretirdi.
 */
export function TextInput({ id, value, maxLength, onCommit }: { id: string; value: string; maxLength?: number; onCommit: (value: string) => void }) {
  const [draft, setDraft] = useState(value);

  const commit = () => {
    const next = draft.trim();
    if (next !== value) onCommit(next);
  };

  return (
    <Input
      id={id}
      className='h-7'
      value={draft}
      maxLength={maxLength}
      onChange={e => setDraft(e.target.value)}
      onBlur={commit}
      onKeyDown={e => {
        if (e.key === 'Enter') e.currentTarget.blur();
        // Escape yazılanı atar: yanlış girilen bir adresi geri almanın en hızlı
        // yolu. Blur commit'i tetiklemesin diye taslak önce geri alınır.
        if (e.key === 'Escape') {
          setDraft(value);
          e.currentTarget.blur();
        }
      }}
    />
  );
}

/**
 * Parola girdisi — her zaman BOŞ başlar (sunucu parolayı göndermez). Boş bırakılan alan "dokunma" demektir; yazılan değer
 * commit'te (blur / Enter) gönderilir ve girdi yeniden boşalır. Temizlemek ayrı bir düğmedir (bkz. `CredentialFields`).
 */
export function PasswordInput({ id, onCommit }: { id: string; onCommit: (value: string) => void }) {
  const [draft, setDraft] = useState('');

  const commit = () => {
    if (draft === '') return;
    onCommit(draft);
    setDraft('');
  };

  return (
    <Input
      id={id}
      type='password'
      autoComplete='new-password'
      className='h-7'
      maxLength={128}
      value={draft}
      placeholder='••••••'
      onChange={e => setDraft(e.target.value)}
      onBlur={commit}
      onKeyDown={e => {
        if (e.key === 'Enter') e.currentTarget.blur();
        if (e.key === 'Escape') {
          setDraft('');
          e.currentTarget.blur();
        }
      }}
    />
  );
}

export function NumberInput({
  id,
  value,
  min,
  max,
  step,
  onCommit
}: {
  id: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  onCommit: (value: number) => void;
}) {
  const [draft, setDraft] = useState(String(value));

  const commit = () => {
    const parsed = Number(draft);
    // Boş ya da sayı olmayan girdi sessizce eski değere döner: 400 yiyecek bir
    // gövde göndermektense hiç göndermemek doğru.
    if (!Number.isFinite(parsed) || draft.trim() === '') {
      setDraft(String(value));
      return;
    }
    const clamped = Math.min(max, Math.max(min, parsed));
    setDraft(String(clamped));
    if (clamped !== value) onCommit(clamped);
  };

  return (
    <Input
      id={id}
      type='number'
      className='h-7'
      min={min}
      max={max}
      step={step}
      value={draft}
      onChange={e => setDraft(e.target.value)}
      onBlur={commit}
      onKeyDown={e => e.key === 'Enter' && e.currentTarget.blur()}
    />
  );
}

/**
 * `NumberInput`'un boş bırakılabilen hâli: boş girdi `null` demektir (örn. "izleme portu yok"). Sayı olmayan ya da
 * aralık dışı girdi `NumberInput`'taki gibi sessizce eski değere döner.
 */
export function OptionalNumberInput({
  id,
  value,
  min,
  max,
  onCommit
}: {
  id: string;
  value: number | null;
  min: number;
  max: number;
  onCommit: (value: number | null) => void;
}) {
  const [draft, setDraft] = useState(value == null ? '' : String(value));

  const commit = () => {
    if (draft.trim() === '') {
      if (value != null) onCommit(null);
      return;
    }
    const parsed = Number(draft);
    if (!Number.isInteger(parsed) || parsed < min || parsed > max) {
      setDraft(value == null ? '' : String(value));
      return;
    }
    if (parsed !== value) onCommit(parsed);
  };

  return (
    <Input
      id={id}
      type='number'
      className='h-7'
      min={min}
      max={max}
      value={draft}
      onChange={e => setDraft(e.target.value)}
      onBlur={commit}
      onKeyDown={e => e.key === 'Enter' && e.currentTarget.blur()}
    />
  );
}

export function ColorInput({ id, value, onCommit }: { id: string; value: string; onCommit: (value: string) => void }) {
  const [draft, setDraft] = useState(value);

  return (
    <Input
      id={id}
      type='color'
      className='h-7 w-14 p-1'
      value={draft}
      onChange={e => setDraft(e.target.value)}
      onBlur={() => draft !== value && onCommit(draft)}
    />
  );
}

/**
 * Sayısal enum seçici.
 *
 * Enum'lar tel üzerinde SAYI olarak gidiyor (bkz. `entityEnums.ts`); Base UI
 * `Select` değeri olduğu gibi taşıdığı için dönüştürme gerekmiyor. `SelectValue`
 * etiketi kendisi bulamaz — seçili değerin Türkçe karşılığı çocuk olarak verilir.
 */
export function EnumSelect<T extends number>({
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
