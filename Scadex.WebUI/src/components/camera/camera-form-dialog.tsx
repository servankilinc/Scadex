import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle
} from '@/components/ui/dialog';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { handleFormApiError } from '@/lib/axios-helper';
import { useCreateCamera, useUpdateCamera } from '@/hooks/use-cameras';

import {
  cameraFormSchema,
  emptyCameraForm,
  toCameraForm,
  toCreateRequest,
  toUpdateRequest,
  type CameraDto,
  type CameraFormValues
} from '@/models/camera';

/**
 * Tek bir form bileşeni tutuluyor çünkü iki mod arasındaki fark yalnızca iki
 * alan (`cabinetId` / `id`) ve `isActive`'in görünürlüğü; iki ayrı bileşen,
 * 20 alanlık bir formu iki yerde bakmak demek olurdu ve ikisi sessizce
 * ayrışırdı. Sunucuda da ortak kurallar tek `CameraRules` sınıfında.
 */
type Props =
  | { mode: 'create'; cabinetId: string; open: boolean; onOpenChange: (open: boolean) => void; camera?: undefined }
  | { mode: 'edit'; camera: CameraDto | null; open: boolean; onOpenChange: (open: boolean) => void; cabinetId?: undefined };

/**
 * Boş bırakılabilen sayısal alanlar için (`httpsPort`, `monitoringPort`).
 *
 * `valueAsNumber` KULLANILAMAZ: boş bir `<input type='number'>` ondan `NaN`
 * döner ve kullanıcı alanı bilerek boş bıraktığında "sayı olmalı" diye anlamsız
 * bir hata görürdü — oysa boş bırakmak geçerli ve sunucuda `null` demek.
 */
const toNullableNumber = (value: unknown): number | null => {
  if (value === '' || value === null || value === undefined) return null;
  const parsed = Number(value);
  return Number.isNaN(parsed) ? null : parsed;
};

export function CameraFormDialog(props: Props) {
  const { mode, open, onOpenChange } = props;
  const isEdit = mode === 'edit';

  const form = useForm<CameraFormValues>({
    resolver: zodResolver(cameraFormSchema),
    defaultValues: emptyCameraForm
  });

  const createMutation = useCreateCamera();
  const updateMutation = useUpdateCamera();
  const mutation = isEdit ? updateMutation : createMutation;
  const errors = form.formState.errors;

  // Vazgeçilen bir kaydın alanları bir sonraki açılışta karşımıza çıkmasın.
  // Parola alanı DÜZENLEMEDE DE boş gelir — `toCameraForm` onu bilerek boş
  // bırakıyor (gerekçe `cameraForm.ts`'te), böylece boş bırakmak "dokunma"
  // anlamını korur.
  const editing = isEdit ? props.camera : null;
  useEffect(() => {
    if (!open) return;
    form.reset(editing ? toCameraForm(editing) : emptyCameraForm);
  }, [open, editing, form]);

  const submit = form.handleSubmit(values => {
    if (isEdit) {
      if (!editing) return;
      updateMutation.mutate(toUpdateRequest(values, editing.id), {
        onSuccess: () => onOpenChange(false),
        onError: error => handleFormApiError(error, form.setError)
      });
      return;
    }

    createMutation.mutate(toCreateRequest(values, props.cabinetId), {
      onSuccess: () => onOpenChange(false),
      onError: error => handleFormApiError(error, form.setError)
    });
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='max-h-[85vh] gap-0 overflow-y-auto p-0 sm:max-w-2xl'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>{isEdit ? 'Kamerayı düzenle' : 'Yeni kamera'}</DialogTitle>
          <DialogDescription>
            Adres ve kanal bilgileri kameradan okunmaz, buradan tanımlanır. Canlı görüntü bu turda henüz yok.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} noValidate>
          <FieldGroup className='p-4'>
            <Field>
              <FieldLabel htmlFor='camera-name'>Ad</FieldLabel>
              <Input id='camera-name' autoFocus {...form.register('name')} />
              <FieldDescription>Kabin içinde benzersiz olmalı.</FieldDescription>
              {errors.name && <FieldError>{errors.name.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='camera-description'>Açıklama</FieldLabel>
              <Textarea id='camera-description' rows={2} {...form.register('description')} />
              {errors.description && <FieldError>{errors.description.message}</FieldError>}
            </Field>

            <div className='grid gap-4 sm:grid-cols-2'>
              <Field>
                <FieldLabel htmlFor='camera-manufacturer'>Üretici</FieldLabel>
                <Input id='camera-manufacturer' {...form.register('manufacturer')} />
                {errors.manufacturer && <FieldError>{errors.manufacturer.message}</FieldError>}
              </Field>
              <Field>
                <FieldLabel htmlFor='camera-model'>Model</FieldLabel>
                <Input id='camera-model' placeholder='DS-2CD1123G0-IUF' {...form.register('model')} />
                {errors.model && <FieldError>{errors.model.message}</FieldError>}
              </Field>
            </div>

            <SectionTitle>Ağ</SectionTitle>

            <Field>
              <FieldLabel htmlFor='camera-ip'>IP adresi</FieldLabel>
              <Input id='camera-ip' placeholder='192.168.1.50' {...form.register('ipAddress')} />
              <FieldDescription>Kabin içi LAN adresi — kabinin dış erişim adresi değil.</FieldDescription>
              {errors.ipAddress && <FieldError>{errors.ipAddress.message}</FieldError>}
            </Field>

            <div className='grid gap-4 sm:grid-cols-3'>
              <Field>
                <FieldLabel htmlFor='camera-rtsp-port'>RTSP portu</FieldLabel>
                <Input id='camera-rtsp-port' type='number' {...form.register('rtspPort', { valueAsNumber: true })} />
                {errors.rtspPort && <FieldError>{errors.rtspPort.message}</FieldError>}
              </Field>
              <Field>
                <FieldLabel htmlFor='camera-http-port'>HTTP portu</FieldLabel>
                <Input id='camera-http-port' type='number' {...form.register('httpPort', { valueAsNumber: true })} />
                {errors.httpPort && <FieldError>{errors.httpPort.message}</FieldError>}
              </Field>
              <Field>
                <FieldLabel htmlFor='camera-https-port'>HTTPS portu</FieldLabel>
                <Input id='camera-https-port' type='number' placeholder='—' {...form.register('httpsPort', { setValueAs: toNullableNumber })} />
                <FieldDescription>TLS kapalıysa boş.</FieldDescription>
                {errors.httpsPort && <FieldError>{errors.httpsPort.message}</FieldError>}
              </Field>
            </div>

            <SectionTitle>Erişim</SectionTitle>

            <div className='grid gap-4 sm:grid-cols-2'>
              <Field>
                <FieldLabel htmlFor='camera-username'>Kullanıcı adı</FieldLabel>
                <Input id='camera-username' autoComplete='off' {...form.register('username')} />
                {errors.username && <FieldError>{errors.username.message}</FieldError>}
              </Field>
              <Field>
                <FieldLabel htmlFor='camera-password'>Parola</FieldLabel>
                <Input id='camera-password' type='password' autoComplete='new-password' {...form.register('password')} />
                <FieldDescription>
                  {isEdit ? 'Boş bırakılırsa mevcut parola korunur.' : 'Kamera web arayüzü parolası.'}
                </FieldDescription>
                {errors.password && <FieldError>{errors.password.message}</FieldError>}
              </Field>
            </div>

            <SectionTitle>Akış</SectionTitle>

            <div className='grid gap-4 sm:grid-cols-3'>
              <Field>
                <FieldLabel htmlFor='camera-main-channel'>Ana akım kanalı</FieldLabel>
                <Input id='camera-main-channel' type='number' {...form.register('mainStreamChannel', { valueAsNumber: true })} />
                {errors.mainStreamChannel && <FieldError>{errors.mainStreamChannel.message}</FieldError>}
              </Field>
              <Field>
                <FieldLabel htmlFor='camera-sub-channel'>Tali akım kanalı</FieldLabel>
                <Input id='camera-sub-channel' type='number' {...form.register('subStreamChannel', { valueAsNumber: true })} />
                {errors.subStreamChannel && <FieldError>{errors.subStreamChannel.message}</FieldError>}
              </Field>
              <Field>
                <FieldLabel htmlFor='camera-snapshot-channel'>Anlık görüntü kanalı</FieldLabel>
                <Input id='camera-snapshot-channel' type='number' {...form.register('snapshotChannel', { valueAsNumber: true })} />
                {errors.snapshotChannel && <FieldError>{errors.snapshotChannel.message}</FieldError>}
              </Field>
            </div>

            <div className='rounded-lg border p-3'>
              <Field orientation='horizontal'>
                <FieldLabel htmlFor='camera-main-enabled'>Ana akım açık</FieldLabel>
                <Controller
                  control={form.control}
                  name='mainStreamEnabled'
                  render={({ field }) => <Switch id='camera-main-enabled' checked={field.value} onCheckedChange={field.onChange} />}
                />
              </Field>
              <Field orientation='horizontal' className='mt-2'>
                <FieldLabel htmlFor='camera-sub-enabled'>Tali akım açık</FieldLabel>
                <Controller
                  control={form.control}
                  name='subStreamEnabled'
                  render={({ field }) => <Switch id='camera-sub-enabled' checked={field.value} onCheckedChange={field.onChange} />}
                />
              </Field>
              {errors.mainStreamEnabled && <FieldError className='mt-2'>{errors.mainStreamEnabled.message}</FieldError>}
            </div>

            <SectionTitle>İzleme</SectionTitle>

            <div className='grid gap-4 sm:grid-cols-2'>
              <Field>
                <FieldLabel htmlFor='camera-monitoring-port'>İzleme portu</FieldLabel>
                <Input id='camera-monitoring-port' type='number' placeholder='RTSP portu' {...form.register('monitoringPort', { setValueAs: toNullableNumber })} />
                <FieldDescription>Boşsa RTSP portu kullanılır. Ping değil TCP bağlantısı denenir.</FieldDescription>
                {errors.monitoringPort && <FieldError>{errors.monitoringPort.message}</FieldError>}
              </Field>
              <Field>
                <FieldLabel htmlFor='camera-ping-interval'>Yoklama aralığı (sn)</FieldLabel>
                <Input id='camera-ping-interval' type='number' {...form.register('pingIntervalSec', { valueAsNumber: true })} />
                <FieldDescription>Varsayılan 300 sn (5 dk).</FieldDescription>
                {errors.pingIntervalSec && <FieldError>{errors.pingIntervalSec.message}</FieldError>}
              </Field>
            </div>

            <div className='rounded-lg border p-3'>
              <Field orientation='horizontal'>
                <FieldLabel htmlFor='camera-monitoring-enabled'>İzleme açık</FieldLabel>
                <Controller
                  control={form.control}
                  name='isMonitoringEnabled'
                  render={({ field }) => <Switch id='camera-monitoring-enabled' checked={field.value} onCheckedChange={field.onChange} />}
                />
              </Field>
              <FieldDescription className='mt-2'>
                Yoklamayı yapan servis henüz yazılmadı; bu ayar o servis geldiğinde etkili olacak.
              </FieldDescription>

              {isEdit && (
                <Field orientation='horizontal' className='mt-3'>
                  <FieldLabel htmlFor='camera-active'>Aktif</FieldLabel>
                  <Controller
                    control={form.control}
                    name='isActive'
                    render={({ field }) => <Switch id='camera-active' checked={field.value} onCheckedChange={field.onChange} />}
                  />
                </Field>
              )}
              {isEdit && (
                // Kamera silinemez (`IActivatableEntity`) — pasife alma tek yol.
                <FieldDescription className='mt-2'>Pasif kamera listede gizlenir ama kaydı ve çekim geçmişi korunur.</FieldDescription>
              )}
            </div>
          </FieldGroup>

          <DialogFooter className='mx-0 mb-0'>
            <Button type='button' variant='outline' onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
              Vazgeç
            </Button>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kaydediliyor…' : isEdit ? 'Kaydet' : 'Kamerayı ekle'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function SectionTitle({ children }: { children: React.ReactNode }) {
  return <h2 className='mt-2 border-b pb-1 text-sm font-medium text-muted-foreground'>{children}</h2>;
}
