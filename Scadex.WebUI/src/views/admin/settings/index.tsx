import { useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { handleFormApiError } from '@/lib/axios-helper';
import {
  useCameraCaptureSetting,
  useMediaGatewaySetting,
  useUpdateCameraCaptureSetting,
  useUpdateMediaGatewaySetting
} from '@/hooks/use-settings';
import {
  RTSP_TRANSPORTS,
  mediaGatewaySettingFormSchema,
  toRtspTransport,
  type MediaGatewaySettingFormValues
} from '@/models/mediaGatewaySetting';
import { cameraCaptureSettingFormSchema, type CameraCaptureSettingFormValues } from '@/models/cameraCaptureSetting';

/**
 * Sistem ayarları — `/admin/settings`.
 *
 * Buradaki değerler `appsettings.json`'da DEĞİL veritabanındadır ve kaydedildikleri an
 * etkilidir: sunucu her yazmada kendi önbellek anahtarını düşürüyor, `MediaMtxGateway`
 * de adresi ve zaman aşımını her çağrıda ayardan okuyor. Yeniden başlatma gerekmez.
 *
 * **İki ayrı form, tek ekran.** Sekme değil kart: iki grubun toplamı on üç alan, hepsi
 * tek ekrana sığıyor ve sekme, kullanıcıyı hangi grubun neyi kapsadığını tahmin etmeye
 * zorlardı. Formlar ayrı çünkü uçları, tabloları ve önbellek anahtarları da ayrı —
 * medya geçidini kaydetmek kamera ayarlarına dokunmaz.
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

      <MediaGatewayForm />
      <CameraCaptureForm />
    </div>
  );
}

function MediaGatewayForm() {
  const query = useMediaGatewaySetting();
  const mutation = useUpdateMediaGatewaySetting();

  // Kolon serbest metin olduğu için taşıma değeri okuma tarafında daraltılıyor.
  // `useMemo` şart değil (RHF `values`'ı derin karşılaştırır) ama niyeti görünür kılar.
  const values = useMemo<MediaGatewaySettingFormValues | undefined>(
    () => (query.data ? { ...query.data, rtspTransport: toRtspTransport(query.data.rtspTransport) } : undefined),
    [query.data]
  );

  const form = useForm<MediaGatewaySettingFormValues>({
    resolver: zodResolver(mediaGatewaySettingFormSchema),
    // `values` + `keepDirtyValues`: form sunucudan gelen veriyle KENDİLİĞİNDEN
    // eşitlenir, `useEffect` + `reset` gerekmez (kod tabanının efekt kuralı).
    // `keepDirtyValues`, arka planda bir refetch olursa kullanıcının dokunduğu
    // alanları korur; dokunulmayanlar sunucudaki doğruya güncellenir.
    values,
    resetOptions: { keepDirtyValues: true }
  });

  const errors = form.formState.errors;

  const submit = form.handleSubmit(values =>
    mutation.mutate(values, {
      // Kaydedilen değerler artık "temiz": aksi hâlde Kaydet düğmesi açık kalır
      // ve kullanıcı kaydın gittiğinden emin olamaz.
      onSuccess: () => form.reset(values),
      onError: error => handleFormApiError(error, form.setError)
    })
  );

  return (
    <Card>
      <CardHeader>
        <CardTitle>Medya Geçidi</CardTitle>
        <CardDescription>
          MediaMTX Control API adresi, izleme bileti ve RTSP oturum davranışı. MediaMTX uygulama tarafından başlatılmaz; ayrı bir süreçtir.
        </CardDescription>
      </CardHeader>

      <CardContent>
        {query.isPending && <Skeleton className='h-72 w-full rounded-lg' />}
        {query.isError && <p className='text-sm text-destructive'>{query.error.message}</p>}

        {query.data && (
          <form onSubmit={submit} noValidate>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor='mg-api-base-url'>Control API adresi</FieldLabel>
                <Input id='mg-api-base-url' placeholder='http://127.0.0.1:9997' {...form.register('apiBaseUrl')} />
                <FieldDescription>
                  Sunucudan MediaMTX'e giden adres. Kaydedilen değer sondaki `/` kırpılarak saklanır.
                </FieldDescription>
                {errors.apiBaseUrl && <FieldError>{errors.apiBaseUrl.message}</FieldError>}
              </Field>

              <Field>
                <FieldLabel htmlFor='mg-webrtc-url'>WebRTC genel adresi</FieldLabel>
                <Input id='mg-webrtc-url' placeholder='http://localhost:8889' {...form.register('webRtcPublicBaseUrl')} />
                <FieldDescription>
                  Tarayıcının WHEP isteğini attığı adres — Control API'den FARKLI bir porttur ve dışarıdan erişilebilir olmalıdır.
                </FieldDescription>
                {errors.webRtcPublicBaseUrl && <FieldError>{errors.webRtcPublicBaseUrl.message}</FieldError>}
              </Field>

              <div className='grid gap-4 sm:grid-cols-2'>
                <Field>
                  <FieldLabel htmlFor='mg-timeout'>Control API zaman aşımı (ms)</FieldLabel>
                  <Input id='mg-timeout' type='number' {...form.register('apiTimeoutMs', { valueAsNumber: true })} />
                  <FieldDescription>1.000 – 300.000 ms.</FieldDescription>
                  {errors.apiTimeoutMs && <FieldError>{errors.apiTimeoutMs.message}</FieldError>}
                </Field>

                <Field>
                  <FieldLabel htmlFor='mg-token-ttl'>Bilet ömrü (sn)</FieldLabel>
                  <Input id='mg-token-ttl' type='number' {...form.register('tokenTtlSeconds', { valueAsNumber: true })} />
                  <FieldDescription>İzleme bileti bu süre sonunda geçersizleşir. 10 – 3600 sn.</FieldDescription>
                  {errors.tokenTtlSeconds && <FieldError>{errors.tokenTtlSeconds.message}</FieldError>}
                </Field>
              </div>

              <div className='grid gap-4 sm:grid-cols-2'>
                <Field>
                  <FieldLabel htmlFor='mg-close-after'>Oturum kapanma süresi (sn)</FieldLabel>
                  <Input id='mg-close-after' type='number' {...form.register('sourceOnDemandCloseAfterSec', { valueAsNumber: true })} />
                  <FieldDescription>Son izleyici ayrıldıktan sonra kameraya giden RTSP oturumu kapanır. Yol silinmez.</FieldDescription>
                  {errors.sourceOnDemandCloseAfterSec && <FieldError>{errors.sourceOnDemandCloseAfterSec.message}</FieldError>}
                </Field>

                <Field>
                  <FieldLabel htmlFor='mg-rtsp-transport'>RTSP taşıma katmanı</FieldLabel>
                  <Controller
                    control={form.control}
                    name='rtspTransport'
                    render={({ field }) => (
                      // Serbest metin değil seçim: MediaMTX yalnızca bu dört değeri
                      // tanır, başkası gönderilirse yol hiç kurulmaz.
                      <Select value={field.value} onValueChange={value => field.onChange(value ?? field.value)}>
                        <SelectTrigger id='mg-rtsp-transport'>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {RTSP_TRANSPORTS.map(transport => (
                            <SelectItem key={transport} value={transport}>
                              {transport}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    )}
                  />
                  <FieldDescription>Kamera bağlantısı kopuyorsa `tcp` en güvenlisidir.</FieldDescription>
                  {errors.rtspTransport && <FieldError>{errors.rtspTransport.message}</FieldError>}
                </Field>
              </div>

              <Field>
                <FieldLabel htmlFor='mg-record-root'>Kayıt kök dizini</FieldLabel>
                <Input id='mg-record-root' {...form.register('recordRoot')} />
                <FieldDescription>MediaMTX'in klip dosyalarını yazdığı dizin. MediaMTX süreci bu yola yazabilmelidir.</FieldDescription>
                {errors.recordRoot && <FieldError>{errors.recordRoot.message}</FieldError>}
              </Field>
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
        <CardDescription>Anlık görüntü ve klip davranışı, dosyaların nereye yazılacağı ve ne kadar saklanacağı.</CardDescription>
      </CardHeader>

      <CardContent>
        {query.isPending && <Skeleton className='h-64 w-full rounded-lg' />}
        {query.isError && <p className='text-sm text-destructive'>{query.error.message}</p>}

        {query.data && (
          <form onSubmit={submit} noValidate>
            <FieldGroup>
              <div className='grid gap-4 sm:grid-cols-2'>
                <Field>
                  <FieldLabel htmlFor='cc-snapshot-timeout'>Anlık görüntü zaman aşımı (ms)</FieldLabel>
                  <Input id='cc-snapshot-timeout' type='number' {...form.register('snapshotTimeoutMs', { valueAsNumber: true })} />
                  <FieldDescription>500 – 60.000 ms.</FieldDescription>
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
                  <FieldDescription>MediaMTX'in dosyayı kapatması için klip bittikten sonra beklenen süre.</FieldDescription>
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
