import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { handleFormApiError } from '@/lib/axios-helper';
import { useCameraCaptureSetting, useUpdateCameraCaptureSetting } from '@/hooks/use-settings';
import { cameraCaptureSettingFormSchema, type CameraCaptureSettingFormValues } from '@/models/cameraCaptureSetting';

/**
 * Sistem ayarları — `/admin/settings`.
 *
 * Buradaki değerler veritabanındadır ve kaydedildikleri an etkilidir: sunucu her yazmada
 * kendi önbellek anahtarını düşürüyor. Yeniden başlatma gerekmez.
 *
 * Medya geçidi (MediaMTX) ayarları bu ekranda DEĞİL: sunucunun `appsettings.json >
 * MediaGateway` bölümündedir ve `mediamtx.yml` ile elle senkron tutulur.
 *
 * Bu ekranda henüz izin kontrolü YOK; `permission` claim'i üretiliyor ama hiçbir yerde
 * okunmuyor (bilinçli boşluk, bkz. PROJECT_OVERVIEW.md § 7 (b)).
 */
export default function Settings() {
  return (
    <div className='flex max-w-3xl flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Sistem Ayarları</h1>
        <p className='text-sm text-muted-foreground'>
          Veritabanında tutulur ve kaydedildiği anda etkili olur — sunucuyu yeniden başlatmak gerekmez.
        </p>
      </div>

      <CameraCaptureForm />
    </div>
  );
}

function CameraCaptureForm() {
  const query = useCameraCaptureSetting();
  const mutation = useUpdateCameraCaptureSetting();

  const form = useForm<CameraCaptureSettingFormValues>({
    resolver: zodResolver(cameraCaptureSettingFormSchema),
    values: query.data,
    resetOptions: { keepDirtyValues: true }
  });

  const errors = form.formState.errors;

  const submit = form.handleSubmit(values =>
    mutation.mutate(values, {
      onSuccess: () => form.reset(values),
      onError: error => handleFormApiError(error, form.setError)
    })
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle>Kamera Çekimi</CardTitle>
        <CardDescription>
          Anlık görüntü ve klip davranışı, dosyaların nereye yazılacağı ve ne kadar saklanacağı. İkisi de sunucuda FFmpeg ile MediaMTX'in
          ana akım yolundan alınır; izleme açıksa kameraya ikinci bağlantı açılmaz.
        </CardDescription>
      </CardHeader>

      <CardContent>
        {query.isPending && <Skeleton className='h-64 w-full rounded-lg' />}
        {query.isError && <p className='text-sm text-destructive'>{query.error.message}</p>}

        {query.data && (
          <form onSubmit={submit} noValidate>
            <FieldGroup>
              <div className='grid gap-4 sm:grid-cols-2'>
                <Field>
                  <FieldLabel htmlFor='cc-snapshot-timeout'>Bağlantı / ilk kare zaman aşımı (ms)</FieldLabel>
                  <Input id='cc-snapshot-timeout' type='number' {...form.register('snapshotTimeoutMs', { valueAsNumber: true })} />
                  <FieldDescription>
                    İlk anahtar kareyi almak için azami süre (kimse izlemiyorsa MediaMTX'in kameraya bağlanması dahil); kameranın I-kare
                    aralığından uzun olmalı. Klipte kayıt süresine eklenir. 500 – 60.000 ms.
                  </FieldDescription>
                  {errors.snapshotTimeoutMs && <FieldError>{errors.snapshotTimeoutMs.message}</FieldError>}
                </Field>

                <Field>
                  <FieldLabel htmlFor='cc-snapshot-cache'>Anlık görüntü önbelleği (sn)</FieldLabel>
                  <Input id='cc-snapshot-cache' type='number' {...form.register('snapshotCacheSeconds', { valueAsNumber: true })} />
                  <FieldDescription>Aynı kameraya arka arkaya gelen isteklerin sürü koruması. `0` = önbellek yok.</FieldDescription>
                  {errors.snapshotCacheSeconds && <FieldError>{errors.snapshotCacheSeconds.message}</FieldError>}
                </Field>
              </div>

              <Field>
                <FieldLabel htmlFor='cc-capture-root'>Çekim kök dizini</FieldLabel>
                <Input id='cc-capture-root' placeholder='uploads/captures' {...form.register('captureRoot')} />
                <FieldDescription>
                  `wwwroot` ALTINDA göreli bir yol olmalı — mutlak yol veya `..` kabul edilmez. Baştaki/sondaki `/` kırpılarak saklanır.
                </FieldDescription>
                {errors.captureRoot && <FieldError>{errors.captureRoot.message}</FieldError>}
              </Field>

              <Field>
                <FieldLabel htmlFor='cc-retention'>Saklama süresi (gün)</FieldLabel>
                <Input id='cc-retention' type='number' {...form.register('captureRetentionDays', { valueAsNumber: true })} />
                <FieldDescription>
                  Süresi dolan çekimlerin yalnızca DOSYASI silinir (her gün, varsayılan 04:00); satır kalır ve çekimin yapıldığı bilgisi
                  geçmişte durur. `0` = süresiz sakla.
                </FieldDescription>
                {errors.captureRetentionDays && <FieldError>{errors.captureRetentionDays.message}</FieldError>}
              </Field>

              <div className='grid gap-4 sm:grid-cols-2'>
                <Field>
                  <FieldLabel htmlFor='cc-max-clip'>Klip süresi üst sınırı (sn)</FieldLabel>
                  <Input id='cc-max-clip' type='number' {...form.register('maxClipDurationSec', { valueAsNumber: true })} />
                  <FieldDescription>Daha uzun bir klip isteyen çağrı reddedilir. 1 – 3600 sn.</FieldDescription>
                  {errors.maxClipDurationSec && <FieldError>{errors.maxClipDurationSec.message}</FieldError>}
                </Field>

                <Field>
                  <FieldLabel htmlFor='cc-finalize-grace'>Sonlandırma payı (ms)</FieldLabel>
                  <Input id='cc-finalize-grace' type='number' {...form.register('clipFinalizeGraceMs', { valueAsNumber: true })} />
                  <FieldDescription>Kayıt süresi dolduktan sonra FFmpeg'in MP4 dosyasını kapatması için tanınan ek süre.</FieldDescription>
                  {errors.clipFinalizeGraceMs && <FieldError>{errors.clipFinalizeGraceMs.message}</FieldError>}
                </Field>
              </div>
            </FieldGroup>

            <div className='mt-4 flex justify-end'>
              <Button type='submit' disabled={mutation.isPending || !form.formState.isDirty}>
                {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
              </Button>
            </div>
          </form>
        )}
      </CardContent>
    </Card>
  );
}
