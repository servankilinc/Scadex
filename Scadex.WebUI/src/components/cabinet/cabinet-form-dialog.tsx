import { useEffect } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { LocationPicker } from '@/components/custom/location-picker';
import { handleFormApiError } from '@/lib/axios-helper';
import {
  cabinetFormSchema,
  emptyCabinetForm,
  toCabinetForm,
  toCreateRequest,
  toUpdateRequest,
  type CabinetDetailDto,
  type CabinetFormValues
} from '@/models/cabinet';
import { useCabinetUpdateModel, useCreateCabinet, useUpdateCabinet } from '@/hooks/use-cabinets';
import { useCompanies } from '@/hooks/use-companies';

interface CabinetFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Dolu ise düzenleme, boş ise ekleme modu. */
  cabinet?: CabinetDetailDto;
}

/**
 * Kabin ekleme ve düzenleme — tek dialog, iki mod.
 *
 * Düzenleme verisi listeden DEĞİL `GET /api/Cabinet/{id}/update` ucundan gelir:
 * `CabinetDetailDto` SCADA alanlarını taşımıyor ve listeden doldurmak o üç alanı
 * kullanıcı fark etmeden sıfırlardı.
 */
export function CabinetFormDialog({ open, onOpenChange, cabinet }: CabinetFormDialogProps) {
  const isEdit = cabinet != null;

  const form = useForm<CabinetFormValues>({
    resolver: zodResolver(cabinetFormSchema),
    defaultValues: emptyCabinetForm
  });

  const companies = useCompanies();
  // Yalnızca AKTİF firmalar seçilebilir — pasife alınmış bir firmaya yeni kabin
  // bağlanmamalı. Düzenlemede firma değiştirilemediği için bu eleme zararsız.
  const companyOptions = (companies.data ?? []).filter(company => company.isActive);

  // Dialog kapalıyken sorgu atılmaz; `enabled` bunu `id == null` ile yönetiyor.
  const updateModel = useCabinetUpdateModel(open && isEdit ? cabinet.id : null);

  const createMutation = useCreateCabinet();
  const updateMutation = useUpdateCabinet();
  const mutation = isEdit ? updateMutation : createMutation;

  /**
   * Formu doldur.
   *
   * Ekleme modunda dialog her açılışta SIFIRLANIR; yoksa vazgeçilen bir kaydın
   * alanları bir sonraki açılışta karşımıza çıkar.
   */
  useEffect(() => {
    if (!open) return;

    if (!isEdit) {
      form.reset(emptyCabinetForm);
      return;
    }

    if (updateModel.data) {
      form.reset(toCabinetForm(updateModel.data, cabinet.companyId));
    }
  }, [open, isEdit, updateModel.data, cabinet?.companyId, form]);

  const errors = form.formState.errors;
  // `form.watch()` DEĞİL: React Compiler onu memoize edemiyor ve bileşenin
  // tamamının derlenmesini atlıyor. `useWatch` abonelik tabanlı ve uyumlu.
  const scadaIsEnabled = useWatch({ control: form.control, name: 'scadaIsEnabled' });
  const isLoadingModel = isEdit && updateModel.isPending;
  const hasNoCompany = !isEdit && !companies.isPending && companyOptions.length === 0;

  function submit(values: CabinetFormValues) {
    if (isEdit) {
      updateMutation.mutate(toUpdateRequest(values), {
        onSuccess: () => onOpenChange(false),
        onError: error => handleFormApiError(error, form.setError)
      });
      return;
    }

    createMutation.mutate(toCreateRequest(values), {
      onSuccess: () => onOpenChange(false),
      onError: error => handleFormApiError(error, form.setError)
    });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='max-h-[90vh] gap-0 overflow-y-auto p-0 sm:max-w-2xl'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>{isEdit ? 'Kabini düzenle' : 'Yeni kabin'}</DialogTitle>
          <DialogDescription>
            {isEdit ? 'Kabinin künyesi ve SCADA bağlantı ayarları.' : 'Kabin oluşturulduktan sonra diyagramı çizilebilir.'}
          </DialogDescription>
        </DialogHeader>

        {isLoadingModel ? (
          <div className='flex flex-col gap-3 p-4'>
            <Skeleton className='h-8 w-full' />
            <Skeleton className='h-8 w-full' />
            <Skeleton className='h-64 w-full' />
          </div>
        ) : (
          <form onSubmit={form.handleSubmit(submit)} noValidate>
            <FieldGroup className='p-4'>
              <div className='grid gap-5 sm:grid-cols-2'>
                <Field>
                  <FieldLabel htmlFor='cabinet-name'>Ad</FieldLabel>
                  <Input id='cabinet-name' autoFocus {...form.register('name')} />
                  {errors.name && <FieldError>{errors.name.message}</FieldError>}
                </Field>

                {isEdit ? (
                  <Field>
                    <FieldLabel>Firma</FieldLabel>
                    <Input value={cabinet.companyName} disabled readOnly />
                    {/* CabinetUpdateDto `CompanyId` taşımıyor: kabin firma değiştiremez. */}
                    <FieldDescription>Kabinin firması sonradan değiştirilemez.</FieldDescription>
                  </Field>
                ) : (
                  <Field>
                    <FieldLabel htmlFor='cabinet-company'>Firma</FieldLabel>
                    <Controller
                      control={form.control}
                      name='companyId'
                      render={({ field }) => (
                        <Select value={field.value} onValueChange={value => field.onChange(value ?? '')} disabled={companyOptions.length === 0}>
                          <SelectTrigger id='cabinet-company' className='w-full'>
                            <SelectValue placeholder='Firma seçin'>
                              {companyOptions.find(company => company.id === field.value)?.name}
                            </SelectValue>
                          </SelectTrigger>
                          <SelectContent>
                            {companyOptions.map(company => (
                              <SelectItem key={company.id} value={company.id}>
                                {company.name}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      )}
                    />
                    {hasNoCompany && (
                      <FieldDescription>
                        Aktif firma yok. Kabin bir firmaya bağlı olmak zorunda —{' '}
                        <Link to='/admin/companies' className='underline underline-offset-4'>
                          önce firma ekleyin
                        </Link>
                        .
                      </FieldDescription>
                    )}
                    {errors.companyId && <FieldError>{errors.companyId.message}</FieldError>}
                  </Field>
                )}

                <Field>
                  <FieldLabel htmlFor='cabinet-network-ip'>Ağ IP</FieldLabel>
                  <Input id='cabinet-network-ip' placeholder='192.168.1.10' {...form.register('networkIp')} />
                  {errors.networkIp && <FieldError>{errors.networkIp.message}</FieldError>}
                </Field>

                <Field>
                  <FieldLabel htmlFor='cabinet-gsm-ip'>GSM IP</FieldLabel>
                  <Input id='cabinet-gsm-ip' {...form.register('gsmIp')} />
                  {errors.gsmIp && <FieldError>{errors.gsmIp.message}</FieldError>}
                </Field>
              </div>

              <Field>
                <FieldLabel htmlFor='cabinet-location-description'>Konum açıklaması</FieldLabel>
                <Input id='cabinet-location-description' placeholder='Örn. Kuzey kapısı, 2. bodrum' {...form.register('locationDescription')} />
                {errors.locationDescription && <FieldError>{errors.locationDescription.message}</FieldError>}
              </Field>

              <Field>
                <FieldLabel>Harita konumu</FieldLabel>
                {/* Enlem ve boylam TEK bir kontrolden yönetilir: ikisi ayrı ayrı
                    doldurulabilseydi yarısı girilmiş bir konum mümkün olurdu. */}
                <Controller
                  control={form.control}
                  name='latitude'
                  render={({ field: latField }) => (
                    <Controller
                      control={form.control}
                      name='longitude'
                      render={({ field: lngField }) => (
                        <LocationPicker
                          value={latField.value != null && lngField.value != null ? { lat: latField.value, lng: lngField.value } : null}
                          onChange={next => {
                            latField.onChange(next?.lat ?? null);
                            lngField.onChange(next?.lng ?? null);
                          }}
                        />
                      )}
                    />
                  )}
                />
              </Field>

              <div className='rounded-lg border p-3'>
                <Field orientation='horizontal'>
                  <FieldLabel htmlFor='cabinet-scada-enabled'>SCADA bağlantısı</FieldLabel>
                  <Controller
                    control={form.control}
                    name='scadaIsEnabled'
                    render={({ field }) => (
                      <Switch id='cabinet-scada-enabled' checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
                    )}
                  />
                </Field>

                {scadaIsEnabled && (
                  <div className='mt-4 grid gap-5 sm:grid-cols-2'>
                    <Field>
                      <FieldLabel htmlFor='cabinet-scada-url'>SCADA adresi</FieldLabel>
                      <Input id='cabinet-scada-url' placeholder='http://10.0.0.5:8080' {...form.register('scadaBaseUrl')} />
                      {errors.scadaBaseUrl && <FieldError>{errors.scadaBaseUrl.message}</FieldError>}
                    </Field>

                    <Field>
                      <FieldLabel htmlFor='cabinet-scada-timeout'>Komut zaman aşımı (ms)</FieldLabel>
                      <Input
                        id='cabinet-scada-timeout'
                        type='number'
                        min={10000}
                        step={1000}
                        {...form.register('scadaCommandTimeoutMs', { valueAsNumber: true })}
                      />
                      {errors.scadaCommandTimeoutMs ? (
                        <FieldError>{errors.scadaCommandTimeoutMs.message}</FieldError>
                      ) : (
                        // Yeniden deneme YOK: süre dolarsa komut `NoResponse` olur,
                        // tekrarlanmaz. Kısa bir zaman aşımı bu yüzden pahalı.
                        <FieldDescription>Süre dolarsa komut tekrarlanmaz, `NoResponse` olarak kaydedilir.</FieldDescription>
                      )}
                    </Field>
                  </div>
                )}
              </div>

              {isEdit && (
                <div className='rounded-lg border p-3'>
                  <Field orientation='horizontal'>
                    <FieldLabel htmlFor='cabinet-active'>Aktif</FieldLabel>
                    <Controller
                      control={form.control}
                      name='isActive'
                      render={({ field }) => (
                        <Switch id='cabinet-active' checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
                      )}
                    />
                  </Field>
                  {/* Kabin silinemez (`IActivatableEntity`); pasife alma tek yol
                      ve geri alınabilir olduğu için kayıt listede kalır. */}
                  <FieldDescription className='mt-2'>Pasif kabinler listede kalır ve tekrar aktifleştirilebilir.</FieldDescription>
                </div>
              )}
            </FieldGroup>

            <DialogFooter className='mx-0 mb-0'>
              <Button type='button' variant='outline' onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
                Vazgeç
              </Button>
              <Button type='submit' disabled={mutation.isPending || hasNoCompany}>
                {mutation.isPending ? 'Kaydediliyor…' : isEdit ? 'Kaydet' : 'Kabini oluştur'}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
