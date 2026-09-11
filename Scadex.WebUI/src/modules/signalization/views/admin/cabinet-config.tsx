import { useEffect, useMemo } from 'react';
import {
  Controller,
  useFieldArray,
  useForm,
  useWatch,
  type Control,
  type DeepPartialSkipArrayKey,
  type FieldErrors,
  type UseFormRegister
} from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useBlocker, useSearchParams } from 'react-router';
import { DoorClosedIcon, DoorOpenIcon, LockIcon, LockOpenIcon, PlusIcon, SirenIcon, Trash2Icon } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { useCabinets } from '@/hooks/use-cabinets';
import { newId } from '@/lib/sequential-id';
import { AuthoritySelect, CameraSelect, ChannelSelect, type ChannelUsage } from '../../components/config-selects';
import { useSaveSignalCabinet, useSignalCabinet, useSignalCabinetOptions } from '../../hooks/use-signal-config';
import { handleTreeFormApiError } from '../../lib';
import {
  signalCabinetFormSchema,
  toCabinetFormValues,
  toCabinetSaveRequest,
  type SignalCabinetDto,
  type SignalCabinetFormValues,
  type SignalCabinetOptionsDto,
  type SignalInnerDoorFormValues,
  type SignalOuterDoorFormValues
} from '../../models/cabinet';

/**
 * Kapı yapılandırması — `/signalization/cabinets?cabinetId=…`.
 *
 * Kullanıcının hiyerarşi JSON'unun görsel karşılığı: kabin (ortak siren, süreler) → dış kapılar (anahtar,
 * kamera) → iç kapılar (kurum, anahtar, kilit). Kapılar SANALDIR; seçilen her şey diyagramdaki kartların
 * KANALLARIDIR. Kanalı olmayan bir kapı tanımlanamaz — önce diyagramda kartı ekleyin.
 *
 * Kayıt TAM ağaçtır (`PUT /api/SignalCabinet/{cabinetId}`): formdan çıkarılan kapı kaydedince PASİFE alınır.
 * Kilit / siren durumu burada yalnızca GÖSTERİLİR; onları motor yazar.
 */
export default function SignalCabinetConfig() {
  const cabinets = useCabinets();
  const [searchParams, setSearchParams] = useSearchParams();

  const activeCabinets = useMemo(() => cabinets.data?.filter(c => c.isActive) ?? [], [cabinets.data]);
  // Türetme, efekt DEĞİL: adres çubuğunda kabin yoksa ilk aktif kabin.
  const cabinetId = searchParams.get('cabinetId') || activeCabinets[0]?.id || '';

  const config = useSignalCabinet(cabinetId);
  const options = useSignalCabinetOptions(cabinetId);
  const loadError = config.error ?? options.error;

  // Kabin seçimi adres çubuğunda: bağlantı paylaşılabilir ve kaydedilmemiş değişiklik varken seçim değişimi
  // de bir gezinme olduğu için engelleyiciye takılır.
  const selectCabinet = (id: string) => setSearchParams({ cabinetId: id }, { replace: true });

  const picker = <CabinetPicker cabinets={activeCabinets} value={cabinetId} onChange={selectCabinet} isPending={cabinets.isPending} />;

  return (
    <div className='flex max-w-5xl flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Kapı Yapılandırması</h1>
        <p className='text-sm text-muted-foreground'>
          Kabinin dış ve iç kapılarını diyagramdaki kanallara bağlayın. Kanal listesi{' '}
          {cabinetId ? (
            <Link to={`/cabinets/${cabinetId}/diagram`} className='underline underline-offset-4'>
              diyagramdan
            </Link>
          ) : (
            'diyagramdan'
          )}{' '}
          gelir.
        </p>
      </div>

      {activeCabinets.length === 0 && !cabinets.isPending && <p className='text-sm text-muted-foreground'>Önce bir kabin oluşturun.</p>}
      {loadError && <p className='text-sm text-destructive'>{loadError.message}</p>}

      {/* Form, kabin başına YENİDEN mount edilir (`key`): varsayılanlar veriden okunur, `reset` efekti yok. */}
      {config.data && options.data ? (
        <CabinetConfigForm key={cabinetId} cabinetId={cabinetId} config={config.data} options={options.data} picker={picker} />
      ) : (
        <>
          {picker}
          {cabinetId && !loadError && <Skeleton className='h-96 w-full rounded-xl' />}
        </>
      )}
    </div>
  );
}

function CabinetPicker({
  cabinets,
  value,
  onChange,
  isPending
}: {
  cabinets: { id: string; name: string }[];
  value: string;
  onChange: (id: string) => void;
  isPending: boolean;
}) {
  return (
    <Field className='w-full max-w-xs'>
      <FieldLabel htmlFor='signal-cabinet'>Kabin</FieldLabel>
      <Select value={value || null} onValueChange={next => next && onChange(next)}>
        <SelectTrigger id='signal-cabinet' className='w-full'>
          <SelectValue placeholder={isPending ? 'Yükleniyor…' : 'Kabin seçin'}>{cabinets.find(c => c.id === value)?.name}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          {cabinets.map(cabinet => (
            <SelectItem key={cabinet.id} value={cabinet.id}>
              {cabinet.name}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}

// ─────────────────────────────────────────────────────────── form

const newInnerDoor = (): SignalInnerDoorFormValues => ({
  doorId: newId(),
  name: '',
  authorityId: '',
  switchIoChannelId: '',
  switchOpenValue: '1',
  lockIoChannelId: '',
  unlockTurnsOn: true
});

// Sunucu her dış kapıda en az bir iç kapı ister; yeni dış kapı boş bir iç kapıyla doğar.
const newOuterDoor = (): SignalOuterDoorFormValues => ({
  doorId: newId(),
  name: '',
  switchIoChannelId: '',
  switchOpenValue: '1',
  cameraId: '',
  innerDoors: [newInnerDoor()]
});

interface DoorContext {
  control: Control<SignalCabinetFormValues>;
  register: UseFormRegister<SignalCabinetFormValues>;
  errors: FieldErrors<SignalCabinetFormValues>;
  options: SignalCabinetOptionsDto;
  usage: ChannelUsage;
  /** Kayıtlı iç kapıların son bilinen kilit durumu (salt okunur). */
  unlockedById: Map<string, boolean>;
}

function CabinetConfigForm({
  cabinetId,
  config,
  options,
  picker
}: {
  cabinetId: string;
  config: SignalCabinetDto;
  options: SignalCabinetOptionsDto;
  picker: React.ReactNode;
}) {
  const form = useForm<SignalCabinetFormValues>({
    resolver: zodResolver(signalCabinetFormSchema),
    defaultValues: toCabinetFormValues(config)
  });

  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'outerDoors' });
  const mutation = useSaveSignalCabinet();
  const errors = form.formState.errors;
  const isDirty = form.formState.isDirty;

  // `form.watch()` DEĞİL: React Compiler onu memoize edemiyor (çekirdek formlarıyla aynı kural).
  const watched = useWatch({ control: form.control });
  const usage = buildChannelUsage(watched);

  const unlockedById = useMemo(
    () => new Map(config.outerDoors.flatMap(outer => outer.innerDoors.map(inner => [inner.id, inner.isUnlocked] as const))),
    [config.outerDoors]
  );

  const ctx: DoorContext = { control: form.control, register: form.register, errors, options, usage, unlockedById };

  const submit = form.handleSubmit(values =>
    mutation.mutate(
      { cabinetId, request: toCabinetSaveRequest(values) },
      {
        // Kaydedilen değerler artık "temiz"; engelleyici ve Kaydet düğmesi buna bakar.
        onSuccess: () => form.reset(values),
        onError: error => handleTreeFormApiError(error, form.setError)
      }
    )
  );

  const rootDoorsError = errors.outerDoors?.message ?? errors.outerDoors?.root?.message;

  return (
    <form onSubmit={submit} noValidate className='flex flex-col gap-4'>
      <UnsavedChangesGuard isDirty={isDirty} />

      <div className='flex flex-wrap items-end justify-between gap-3'>
        {picker}
        <div className='flex flex-wrap items-center gap-1.5'>
          {!config.isConfigured && <Badge variant='outline'>Henüz yapılandırılmadı</Badge>}
          {config.isConfigured && <Badge variant={config.isEnabled ? 'default' : 'secondary'}>{config.isEnabled ? 'Etkin' : 'Kapalı'}</Badge>}
          {config.sirenIsOn && (
            <Badge variant='destructive'>
              <SirenIcon className='animate-pulse' />
              Siren çalıyor
            </Badge>
          )}
        </div>
      </div>

      {!config.isConfigured && (
        <p className='rounded-xl border border-dashed p-3 text-sm text-muted-foreground'>
          Bu kabin modüle bağlı değil; olayları yok sayılıyor. Kapıları tanımlayıp kaydettiğinizde bağlanır.
        </p>
      )}

      <CabinetSettingsCard ctx={ctx} />

      <div className='flex items-center justify-between gap-2'>
        <h2 className='font-medium'>Dış kapılar</h2>
        <Button type='button' size='sm' variant='outline' onClick={() => append(newOuterDoor())}>
          <PlusIcon />
          Dış kapı ekle
        </Button>
      </div>

      {fields.length === 0 && (
        <p className='rounded-xl border border-dashed p-6 text-center text-sm text-muted-foreground'>
          Dış kapı yok. Her dış kapının bir anahtar (giriş) kanalı ve ardında en az bir iç kapısı olmalı.
        </p>
      )}

      {fields.map((field, index) => (
        <OuterDoorCard key={field.id} index={index} ctx={ctx} onRemove={() => remove(index)} />
      ))}

      {rootDoorsError && <FieldError>{rootDoorsError}</FieldError>}

      {/* Uzun ağaçta Kaydet görünür kalsın: kaydırılan `main` içinde altta yapışık. */}
      <div className='sticky bottom-0 -mx-4 flex justify-end gap-2 border-t bg-background/95 px-4 py-3 backdrop-blur'>
        {isDirty && <span className='mr-auto self-center text-xs text-muted-foreground'>Kaydedilmemiş değişiklikler var.</span>}
        <Button type='button' variant='outline' disabled={mutation.isPending || !isDirty} onClick={() => form.reset()}>
          Vazgeç
        </Button>
        <Button type='submit' disabled={mutation.isPending || !isDirty}>
          {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
        </Button>
      </div>
    </form>
  );
}

function CabinetSettingsCard({ ctx }: { ctx: DoorContext }) {
  const { control, register, errors, options, usage } = ctx;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Kabin</CardTitle>
        <CardDescription>Ortak siren ve tüm dış kapılar için geçerli süreler.</CardDescription>
        <CardAction>
          <Controller
            control={control}
            name='isEnabled'
            render={({ field }) => (
              <Field orientation='horizontal'>
                <FieldLabel htmlFor='cab-enabled'>Etkin</FieldLabel>
                <Switch id='cab-enabled' checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
              </Field>
            )}
          />
        </CardAction>
      </CardHeader>
      <CardContent className='flex flex-col gap-4'>
        <p className='text-xs text-muted-foreground'>Kapalıyken bu kabinin olayları yok sayılır; yapılandırma saklanır. Açık bir siren talebi yine de süresinde kapanır.</p>

        <Field>
          <FieldLabel htmlFor='cab-siren'>Siren kanalı</FieldLabel>
          <Controller
            control={control}
            name='sirenIoChannelId'
            render={({ field }) => (
              <ChannelSelect
                id='cab-siren'
                path='sirenIoChannelId'
                channels={options.outputChannels}
                usage={usage}
                value={field.value}
                onChange={field.onChange}
                placeholder='Siren çıkışı seçin'
                allowNone
                invalid={Boolean(errors.sirenIoChannelId)}
              />
            )}
          />
          <FieldDescription>
            Kabindeki TEK, ortak siren çıkışı. Bir dış kapının ardındaki tüm iç kapılar kilitlenince çalar; o dış kapı kapanınca, süre dolunca
            ya da bir iç kapı yeniden açılınca susar.
          </FieldDescription>
          {errors.sirenIoChannelId && <FieldError>{errors.sirenIoChannelId.message}</FieldError>}
        </Field>

        <div className='grid gap-4 sm:grid-cols-2 lg:grid-cols-3'>
          <NumberField id='cab-siren-duration' label='Siren süresi (sn)' description='1 – 3600 sn.' error={errors.sirenDurationSec?.message} registration={register('sirenDurationSec', { valueAsNumber: true })} />
          <NumberField
            id='cab-snapshot-count'
            label='Giriş karesi sayısı'
            description='Dış kapı açılınca çekilen kare. 0 = çekme.'
            error={errors.entrySnapshotCount?.message}
            registration={register('entrySnapshotCount', { valueAsNumber: true })}
          />
          <NumberField
            id='cab-snapshot-interval'
            label='Kare aralığı (ms)'
            description='Kareler arası süre, 200 – 10.000 ms.'
            error={errors.entrySnapshotIntervalMs?.message}
            registration={register('entrySnapshotIntervalMs', { valueAsNumber: true })}
          />
          <NumberField
            id='cab-awaiting-card'
            label='Kart bekleme süresi (sn)'
            description='Bu sürede yetkili kart okutulmazsa "kartsız giriş" uyarısı. 0 = kapalı.'
            error={errors.awaitingCardTimeoutSec?.message}
            registration={register('awaitingCardTimeoutSec', { valueAsNumber: true })}
          />
          <NumberField
            id='cab-max-duration'
            label='Azami işlem süresi (dk)'
            description='Dış kapı kapanmazsa işlem bu süre sonunda zaman aşımıyla kapatılır.'
            error={errors.sessionMaxDurationMin?.message}
            registration={register('sessionMaxDurationMin', { valueAsNumber: true })}
          />
        </div>
      </CardContent>
    </Card>
  );
}

function OuterDoorCard({ index, ctx, onRemove }: { index: number; ctx: DoorContext; onRemove: () => void }) {
  const { control, register, errors, options, usage } = ctx;
  const { fields, append, remove } = useFieldArray({ control, name: `outerDoors.${index}.innerDoors` });
  const doorErrors = errors.outerDoors?.[index];
  const innerRootError = doorErrors?.innerDoors?.message ?? doorErrors?.innerDoors?.root?.message;
  const prefix = `outer-${index}`;

  return (
    <Card>
      <CardHeader>
        <CardTitle className='flex items-center gap-2'>
          <DoorOpenIcon className='size-4 shrink-0' />
          <OuterDoorTitle control={control} index={index} />
        </CardTitle>
        <CardDescription>Kayıttan çıkarılan dış kapı ve iç kapıları pasife alınır; geçmiş işlemler kalır.</CardDescription>
        <CardAction>
          <Button type='button' size='sm' variant='ghost' onClick={onRemove}>
            <Trash2Icon />
            Dış kapıyı çıkar
          </Button>
        </CardAction>
      </CardHeader>

      <CardContent className='flex flex-col gap-4'>
        <div className='grid gap-4 sm:grid-cols-2'>
          <Field>
            <FieldLabel htmlFor={`${prefix}-name`}>Ad</FieldLabel>
            <Input id={`${prefix}-name`} placeholder='örn. Ön dış kapı' {...register(`outerDoors.${index}.name`)} />
            {doorErrors?.name && <FieldError>{doorErrors.name.message}</FieldError>}
          </Field>

          <Field>
            <FieldLabel htmlFor={`${prefix}-camera`}>Kamera</FieldLabel>
            <Controller
              control={control}
              name={`outerDoors.${index}.cameraId`}
              render={({ field }) => (
                <CameraSelect id={`${prefix}-camera`} cameras={options.cameras} value={field.value} onChange={field.onChange} invalid={Boolean(doorErrors?.cameraId)} />
              )}
            />
            <FieldDescription>Dış kapı açılınca giriş kareleri bu kameradan çekilir.</FieldDescription>
            {doorErrors?.cameraId && <FieldError>{doorErrors.cameraId.message}</FieldError>}
          </Field>

          <Field>
            <FieldLabel htmlFor={`${prefix}-switch`}>Anahtar kanalı (giriş)</FieldLabel>
            <Controller
              control={control}
              name={`outerDoors.${index}.switchIoChannelId`}
              render={({ field }) => (
                <ChannelSelect
                  id={`${prefix}-switch`}
                  path={`outerDoors.${index}.switchIoChannelId`}
                  channels={options.inputChannels}
                  usage={usage}
                  value={field.value}
                  onChange={field.onChange}
                  placeholder='Giriş kanalı seçin'
                  invalid={Boolean(doorErrors?.switchIoChannelId)}
                />
              )}
            />
            {doorErrors?.switchIoChannelId && <FieldError>{doorErrors.switchIoChannelId.message}</FieldError>}
          </Field>

          <Field>
            <FieldLabel htmlFor={`${prefix}-open-value`}>Anahtarın “açık” değeri</FieldLabel>
            <Input id={`${prefix}-open-value`} className='font-mono' {...register(`outerDoors.${index}.switchOpenValue`)} />
            <FieldDescription>Kapı açıkken kanalın değeri — genelde 1.</FieldDescription>
            {doorErrors?.switchOpenValue && <FieldError>{doorErrors.switchOpenValue.message}</FieldError>}
          </Field>
        </div>

        <div className='flex items-center justify-between gap-2'>
          <h3 className='text-sm font-medium'>İç kapılar</h3>
          <Button type='button' size='sm' variant='outline' onClick={() => append(newInnerDoor())}>
            <PlusIcon />
            İç kapı ekle
          </Button>
        </div>

        {fields.map((field, innerIndex) => (
          <InnerDoorRow key={field.id} outerIndex={index} index={innerIndex} ctx={ctx} onRemove={() => remove(innerIndex)} />
        ))}

        {innerRootError && <FieldError>{innerRootError}</FieldError>}
      </CardContent>
    </Card>
  );
}

/** Başlık, yazılan adı canlı gösterir (useWatch yalnızca bu alanı dinler). */
function OuterDoorTitle({ control, index }: { control: Control<SignalCabinetFormValues>; index: number }) {
  const name = useWatch({ control, name: `outerDoors.${index}.name` });
  return <span className='truncate'>{name?.trim() || 'Yeni dış kapı'}</span>;
}

function InnerDoorRow({ outerIndex, index, ctx, onRemove }: { outerIndex: number; index: number; ctx: DoorContext; onRemove: () => void }) {
  const { control, register, errors, options, usage, unlockedById } = ctx;
  const base = `outerDoors.${outerIndex}.innerDoors.${index}` as const;
  const doorErrors = errors.outerDoors?.[outerIndex]?.innerDoors?.[index];
  const prefix = `inner-${outerIndex}-${index}`;

  const doorId = useWatch({ control, name: `${base}.doorId` });
  const unlocked = unlockedById.get(doorId);

  return (
    <div className='flex flex-col gap-3 rounded-lg border bg-muted/20 p-3'>
      <div className='flex items-center justify-between gap-2'>
        <div className='flex items-center gap-2 text-sm font-medium'>
          <DoorClosedIcon className='size-4 text-muted-foreground' />
          İç kapı {index + 1}
          {/* Kayıtlı kapının son bilinen kilit durumu; yeni kapıda yok. */}
          {unlocked === true && (
            <Badge variant='outline'>
              <LockOpenIcon />
              Kilitsiz
            </Badge>
          )}
          {unlocked === false && (
            <Badge variant='secondary'>
              <LockIcon />
              Kilitli
            </Badge>
          )}
        </div>
        <Button type='button' size='icon-sm' variant='ghost' aria-label='İç kapıyı çıkar' onClick={onRemove}>
          <Trash2Icon />
        </Button>
      </div>

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3'>
        <Field>
          <FieldLabel htmlFor={`${prefix}-name`}>Ad</FieldLabel>
          <Input id={`${prefix}-name`} placeholder='örn. Belediye iç kapı' {...register(`${base}.name`)} />
          {doorErrors?.name && <FieldError>{doorErrors.name.message}</FieldError>}
        </Field>

        <Field>
          <FieldLabel htmlFor={`${prefix}-authority`}>Kurum</FieldLabel>
          <Controller
            control={control}
            name={`${base}.authorityId`}
            render={({ field }) => (
              <AuthoritySelect id={`${prefix}-authority`} authorities={options.authorities} value={field.value} onChange={field.onChange} invalid={Boolean(doorErrors?.authorityId)} />
            )}
          />
          {doorErrors?.authorityId && <FieldError>{doorErrors.authorityId.message}</FieldError>}
        </Field>

        <Field>
          <FieldLabel htmlFor={`${prefix}-open-value`}>Anahtarın “açık” değeri</FieldLabel>
          <Input id={`${prefix}-open-value`} className='font-mono' {...register(`${base}.switchOpenValue`)} />
          {doorErrors?.switchOpenValue && <FieldError>{doorErrors.switchOpenValue.message}</FieldError>}
        </Field>

        <Field>
          <FieldLabel htmlFor={`${prefix}-switch`}>Anahtar kanalı (giriş)</FieldLabel>
          <Controller
            control={control}
            name={`${base}.switchIoChannelId`}
            render={({ field }) => (
              <ChannelSelect
                id={`${prefix}-switch`}
                path={`${base}.switchIoChannelId`}
                channels={options.inputChannels}
                usage={usage}
                value={field.value}
                onChange={field.onChange}
                placeholder='Giriş kanalı seçin'
                invalid={Boolean(doorErrors?.switchIoChannelId)}
              />
            )}
          />
          {doorErrors?.switchIoChannelId && <FieldError>{doorErrors.switchIoChannelId.message}</FieldError>}
        </Field>

        <Field>
          <FieldLabel htmlFor={`${prefix}-lock`}>Kilit kanalı (çıkış)</FieldLabel>
          <Controller
            control={control}
            name={`${base}.lockIoChannelId`}
            render={({ field }) => (
              <ChannelSelect
                id={`${prefix}-lock`}
                path={`${base}.lockIoChannelId`}
                channels={options.outputChannels}
                usage={usage}
                value={field.value}
                onChange={field.onChange}
                placeholder='Çıkış kanalı seçin'
                invalid={Boolean(doorErrors?.lockIoChannelId)}
              />
            )}
          />
          {doorErrors?.lockIoChannelId && <FieldError>{doorErrors.lockIoChannelId.message}</FieldError>}
        </Field>

        <Controller
          control={control}
          name={`${base}.unlockTurnsOn`}
          render={({ field }) => (
            <Field>
              <FieldLabel htmlFor={`${prefix}-polarity`}>Kilit polaritesi</FieldLabel>
              <div className='flex h-8 items-center gap-2'>
                <Switch id={`${prefix}-polarity`} checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
                <span className='text-sm'>{field.value ? 'Açmak için çıkış 1' : 'Açmak için çıkış 0'}</span>
              </div>
              <FieldDescription>Kilit türüne göre: çıkış verilince açılan kilitte açık bırakın.</FieldDescription>
            </Field>
          )}
        />
      </div>
    </div>
  );
}

function NumberField({
  id,
  label,
  description,
  error,
  registration
}: {
  id: string;
  label: string;
  description: string;
  error?: string;
  registration: ReturnType<UseFormRegister<SignalCabinetFormValues>>;
}) {
  return (
    <Field>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <Input id={id} type='number' {...registration} />
      <FieldDescription>{description}</FieldDescription>
      {error && <FieldError>{error}</FieldError>}
    </Field>
  );
}

// ─────────────────────────────────────────────────────────── kanal kullanımı

/**
 * Formun ANLIK değerlerinden kanal → kullanan alanlar haritası. Seçeneklerdeki `usedBy` KAYITLI yapılandırmayı
 * gösterir; düzenleme sırasında doğru olan formun kendisidir.
 */
function buildChannelUsage(values: DeepPartialSkipArrayKey<SignalCabinetFormValues>): ChannelUsage {
  const usage: ChannelUsage = new Map();
  const add = (channelId: string | undefined, path: string, label: string) => {
    if (!channelId) return;
    const list = usage.get(channelId) ?? [];
    list.push({ path, label });
    usage.set(channelId, list);
  };

  add(values.sirenIoChannelId, 'sirenIoChannelId', 'Kabin sireni');

  values.outerDoors?.forEach((outer, i) => {
    const outerName = outer?.name?.trim() || `Dış kapı ${i + 1}`;
    add(outer?.switchIoChannelId, `outerDoors.${i}.switchIoChannelId`, `${outerName} (anahtar)`);

    outer?.innerDoors?.forEach((inner, j) => {
      const innerName = inner?.name?.trim() || `İç kapı ${j + 1}`;
      add(inner?.switchIoChannelId, `outerDoors.${i}.innerDoors.${j}.switchIoChannelId`, `${innerName} (anahtar)`);
      add(inner?.lockIoChannelId, `outerDoors.${i}.innerDoors.${j}.lockIoChannelId`, `${innerName} (kilit)`);
    });
  });

  return usage;
}

// ─────────────────────────────────────────────────────────── kaydedilmemiş değişiklik

/**
 * Kaydedilmemiş değişiklikle çıkışı engeller: uygulama içi gezinme (kabin değiştirmek dahil — seçim adres
 * çubuğunda) `useBlocker` ile sorulur, sekme kapatma/yenileme `beforeunload` ile tarayıcıya bırakılır.
 */
function UnsavedChangesGuard({ isDirty }: { isDirty: boolean }) {
  const blocker = useBlocker(isDirty);

  useEffect(() => {
    if (!isDirty) return;

    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [isDirty]);

  return (
    <AlertDialog open={blocker.state === 'blocked'} onOpenChange={open => !open && blocker.reset?.()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Kaydedilmemiş değişiklikler var</AlertDialogTitle>
          <AlertDialogDescription>Kapı yapılandırmasında kaydedilmemiş değişiklikler var. Çıkarsanız bu değişiklikler kaybolur.</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={() => blocker.reset?.()}>Sayfada kal</AlertDialogCancel>
          <Button variant='destructive' onClick={() => blocker.proceed?.()}>
            Kaydetmeden çık
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
