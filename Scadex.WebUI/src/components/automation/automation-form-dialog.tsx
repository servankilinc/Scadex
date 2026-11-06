import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
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
  automationFormSchema,
  emptyAutomationForm,
  toAutomationForm,
  toCreateRequest,
  toUpdateRequest,
  type AutomationDetailDto,
  type AutomationFormValues
} from '@/models/automation';
import { useAutomationUpdateModel, useCreateAutomation, useUpdateAutomation } from '@/hooks/use-automations';
import { useCompanies } from '@/hooks/use-companies';

interface AutomationFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Dolu ise düzenleme, boş ise ekleme modu. */
  automation?: AutomationDetailDto;
}

/**
 * Otomasyon ekleme ve düzenleme — tek dialog, iki mod.
 *
 * Düzenleme verisi listeden DEĞİL `GET /api/Automation/{id}/update` ucundan gelir (`AutomationUpdateDto`'nun şekli).
 * SCADA adresi / zaman aşımı 2026-11-04'ten beri burada değil, diyagramdaki kontrol kartının formundadır.
 */
export function AutomationFormDialog({ open, onOpenChange, automation }: AutomationFormDialogProps) {
  const isEdit = automation != null;

  const form = useForm<AutomationFormValues>({
    resolver: zodResolver(automationFormSchema),
    defaultValues: emptyAutomationForm
  });

  const companies = useCompanies();
  // Yalnızca AKTİF firmalar seçilebilir — pasife alınmış bir firmaya yeni otomasyon
  // bağlanmamalı. Düzenlemede firma değiştirilemediği için bu eleme zararsız.
  const companyOptions = (companies.data ?? []).filter(company => company.isActive);

  // Dialog kapalıyken sorgu atılmaz; `enabled` bunu `id == null` ile yönetiyor.
  const updateModel = useAutomationUpdateModel(open && isEdit ? automation.id : null);

  const createMutation = useCreateAutomation();
  const updateMutation = useUpdateAutomation();
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
      form.reset(emptyAutomationForm);
      return;
    }

    if (updateModel.data) {
      form.reset(toAutomationForm(updateModel.data, automation.companyId));
    }
  }, [open, isEdit, updateModel.data, automation?.companyId, form]);

  const errors = form.formState.errors;
  const isLoadingModel = isEdit && updateModel.isPending;
  const hasNoCompany = !isEdit && !companies.isPending && companyOptions.length === 0;

  function submit(values: AutomationFormValues) {
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
      <DialogContent className='flex max-h-[90vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-2xl'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>{isEdit ? 'Otomasyonu düzenle' : 'Yeni otomasyon'}</DialogTitle>
          <DialogDescription>
            {isEdit ? 'Otomasyonun künyesi ve konumu. SCADA ayarları diyagramdaki kontrol kartındadır.' : 'Otomasyon oluşturulduktan sonra diyagramı çizilebilir.'}
          </DialogDescription>
        </DialogHeader>

        {/* Scroll İÇ katmanda ve başlığın ALTINDA: yuvarlatılmış kutu scrollbar'ı köşeden kırpar, X düğmesi scrollbar'la çakışmaz. */}
        <div className='min-h-0 scrollbar-thin overflow-y-auto'>
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
                    <FieldLabel htmlFor='automation-name'>Ad</FieldLabel>
                    <Input id='automation-name' autoFocus {...form.register('name')} />
                    {errors.name && <FieldError>{errors.name.message}</FieldError>}
                  </Field>

                  {isEdit ? (
                    <Field>
                      <FieldLabel>Firma</FieldLabel>
                      <Input value={automation.companyName} disabled readOnly />
                      {/* AutomationUpdateDto `CompanyId` taşımıyor: otomasyon firma değiştiremez. */}
                      <FieldDescription>Otomasyonun firması sonradan değiştirilemez.</FieldDescription>
                    </Field>
                  ) : (
                    <Field>
                      <FieldLabel htmlFor='automation-company'>Firma</FieldLabel>
                      <Controller
                        control={form.control}
                        name='companyId'
                        render={({ field }) => (
                          <Select value={field.value || null} onValueChange={value => field.onChange(value ?? '')} disabled={companyOptions.length === 0}>
                            <SelectTrigger id='automation-company' className='w-full'>
                              {/* Eşleşme yoksa `undefined` BIRAKILMAZ: çocuksuz `SelectValue` ham Guid'i basar. */}
                              <SelectValue placeholder='Firma seçin'>
                                {field.value ? (companyOptions.find(company => company.id === field.value)?.name ?? 'Firma bulunamadı') : undefined}
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
                          Aktif firma yok. Otomasyon bir firmaya bağlı olmak zorunda —{' '}
                          <Link to='/admin/companies' className='underline underline-offset-4'>
                            önce firma ekleyin
                          </Link>
                          .
                        </FieldDescription>
                      )}
                      {errors.companyId && <FieldError>{errors.companyId.message}</FieldError>}
                    </Field>
                  )}
                </div>

                <Field>
                  <FieldLabel htmlFor='automation-location-description'>Konum açıklaması</FieldLabel>
                  <Input id='automation-location-description' placeholder='Örn. Kuzey kapısı, 2. bodrum' {...form.register('locationDescription')} />
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

                {isEdit && (
                  <div className='rounded-lg border p-3'>
                    <Field orientation='horizontal'>
                      <FieldLabel htmlFor='automation-active'>Aktif</FieldLabel>
                      <Controller
                        control={form.control}
                        name='isActive'
                        render={({ field }) => (
                          <Switch id='automation-active' checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
                        )}
                      />
                    </Field>
                    {/* Otomasyon silinemez (`IActivatableEntity`); pasife alma tek yol
                        ve geri alınabilir olduğu için kayıt listede kalır. */}
                    <FieldDescription className='mt-2'>Pasif otomasyonlar listede kalır ve tekrar aktifleştirilebilir.</FieldDescription>
                  </div>
                )}

                {isEdit && (
                  <div className='rounded-lg border p-3'>
                    <Field orientation='horizontal'>
                      <FieldLabel htmlFor='automation-maintenance'>Bakımda</FieldLabel>
                      <Controller
                        control={form.control}
                        name='isUnderMaintenance'
                        render={({ field }) => (
                          <Switch id='automation-maintenance' checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
                        )}
                      />
                    </Field>
                    {/* Durum sunucuda yeniden hesaplanır (`Automation.IsUnderMaintenance`): bakım her şeyi ezer. */}
                    <FieldDescription className='mt-2'>
                      Açıkken otomasyon haritada ve listede "Bakımda" görünür; cihaz arızaları otomasyon durumuna yansımaz.
                    </FieldDescription>
                  </div>
                )}
              </FieldGroup>

              <DialogFooter className='mx-0 mb-0'>
                <Button type='button' variant='outline' onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
                  Vazgeç
                </Button>
                <Button type='submit' disabled={mutation.isPending || hasNoCompany}>
                  {mutation.isPending ? 'Kaydediliyor…' : isEdit ? 'Kaydet' : 'Otomasyonu oluştur'}
                </Button>
              </DialogFooter>
            </form>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
