import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Building2Icon, PencilIcon, PlusIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { handleFormApiError } from '@/lib/axios-helper';
import { companyCreateRequestSchema, companyUpdateRequestSchema, type CompanyCreateRequest, type CompanyDto } from '@/models/company';
import { useCompanies, useCreateCompany, useUpdateCompany } from '@/hooks/use-companies';

/**
 * Firma yönetimi.
 *
 * Kabin `CompanyId` olmadan açılamadığı için bu ekran kabin eklemenin ön koşulu:
 * hiç aktif firma yokken kabin formu kilitli kalır.
 */
export default function Companies() {
  const { data, isPending, isError, error } = useCompanies();
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<CompanyDto | null>(null);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Firmalar</h1>
          <p className='text-sm text-muted-foreground'>Kabinler bir firmaya bağlıdır. Kabin ekleyebilmek için en az bir aktif firma gerekir.</p>
        </div>
        <Button size='sm' onClick={() => setIsCreating(true)}>
          <PlusIcon />
          Yeni firma
        </Button>
      </div>

      {isError && <p className='text-sm text-destructive'>{error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3'>
        {isPending && Array.from({ length: 3 }, (_, i) => <Skeleton key={i} className='h-32 w-full rounded-xl' />)}
        {data?.map(company => <CompanyCard key={company.id} company={company} onEdit={() => setEditing(company)} />)}
      </div>

      {data?.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>Henüz firma yok.</CardContent>
        </Card>
      )}

      <CompanyCreateDialog open={isCreating} onOpenChange={setIsCreating} />
      <CompanyEditDialog company={editing} onClose={() => setEditing(null)} />
    </div>
  );
}

function CompanyCard({ company, onEdit }: { company: CompanyDto; onEdit: () => void }) {
  return (
    <Card className={company.isActive ? undefined : 'opacity-60'}>
      <CardHeader>
        <CardTitle className='flex items-center gap-2'>
          <Building2Icon className='size-4 shrink-0' />
          <span className='truncate'>{company.name}</span>
        </CardTitle>
        {company.description && <CardDescription className='truncate'>{company.description}</CardDescription>}
      </CardHeader>

      <CardContent className='flex flex-col gap-3'>
        {/* Pasif kayitlar listede GORUNUR — geri alinabilsin diye. */}
        {!company.isActive && (
          <div>
            <Badge variant='secondary'>Pasif</Badge>
          </div>
        )}

        <Button size='sm' variant='outline' onClick={onEdit}>
          <PencilIcon />
          Düzenle
        </Button>
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── yeni firma

function CompanyCreateDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const form = useForm<CompanyCreateRequest>({
    resolver: zodResolver(companyCreateRequestSchema),
    defaultValues: { name: '', description: '' }
  });

  const mutation = useCreateCompany();
  const errors = form.formState.errors;

  // Vazgeçilen bir kaydın alanları bir sonraki açılışta karşımıza çıkmasın.
  useEffect(() => {
    if (open) form.reset({ name: '', description: '' });
  }, [open, form]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className='gap-0 p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Yeni firma</DialogTitle>
          <DialogDescription>Kabinler bu firmaya bağlanabilir.</DialogDescription>
        </DialogHeader>

        <form
          onSubmit={form.handleSubmit(values =>
            mutation.mutate(values, {
              onSuccess: () => onOpenChange(false),
              onError: error => handleFormApiError(error, form.setError)
            })
          )}
          noValidate>
          <FieldGroup className='p-4'>
            <Field>
              <FieldLabel htmlFor='company-name'>Ad</FieldLabel>
              <Input id='company-name' autoFocus {...form.register('name')} />
              {errors.name && <FieldError>{errors.name.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='company-description'>Açıklama</FieldLabel>
              <Textarea id='company-description' rows={3} {...form.register('description')} />
              {/* Duzenleme formunda YOK: CompanyUpdateDto bu alani tasimiyor. */}
              <FieldDescription>Yalnızca oluştururken yazılabilir.</FieldDescription>
              {errors.description && <FieldError>{errors.description.message}</FieldError>}
            </Field>
          </FieldGroup>

          <DialogFooter className='mx-0 mb-0'>
            <Button type='button' variant='outline' onClick={() => onOpenChange(false)} disabled={mutation.isPending}>
              Vazgeç
            </Button>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kaydediliyor…' : 'Firmayı oluştur'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────── firma düzenle

/**
 * `GET /{id}/update` ucuna gerek YOK: `CompanyUpdateDto` yalnızca `id`, `name` ve
 * `isActive` taşıyor, üçü de listede zaten mevcut. Kabinin aksine (SCADA alanları
 * listede yok) burada ek bir istek atmak boşuna olurdu.
 */
function CompanyEditDialog({ company, onClose }: { company: CompanyDto | null; onClose: () => void }) {
  const form = useForm({
    resolver: zodResolver(companyUpdateRequestSchema),
    defaultValues: { id: '', name: '', isActive: true }
  });

  const mutation = useUpdateCompany();
  const errors = form.formState.errors;

  useEffect(() => {
    if (company) form.reset({ id: company.id, name: company.name, isActive: company.isActive });
  }, [company, form]);

  return (
    <Dialog open={company != null} onOpenChange={open => !open && onClose()}>
      <DialogContent className='gap-0 p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Firmayı düzenle</DialogTitle>
          <DialogDescription>Açıklama alanı burada değiştirilemez.</DialogDescription>
        </DialogHeader>

        <form
          onSubmit={form.handleSubmit(values =>
            mutation.mutate(values, {
              onSuccess: onClose,
              onError: error => handleFormApiError(error, form.setError)
            })
          )}
          noValidate>
          <FieldGroup className='p-4'>
            <Field>
              <FieldLabel htmlFor='company-edit-name'>Ad</FieldLabel>
              <Input id='company-edit-name' autoFocus {...form.register('name')} />
              {errors.name && <FieldError>{errors.name.message}</FieldError>}
            </Field>

            <div className='rounded-lg border p-3'>
              <Field orientation='horizontal'>
                <FieldLabel htmlFor='company-edit-active'>Aktif</FieldLabel>
                {/* `form.watch()` DEĞİL: React Compiler onu memoize edemiyor ve
                    bileşenin derlenmesini tümden atlıyor. */}
                <Controller
                  control={form.control}
                  name='isActive'
                  render={({ field }) => (
                    <Switch id='company-edit-active' checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
                  )}
                />
              </Field>
              {/* Firma silinemez (`IActivatableEntity`) — pasife alma tek yol. */}
              <FieldDescription className='mt-2'>Pasif firmaya yeni kabin bağlanamaz; mevcut kabinleri etkilenmez.</FieldDescription>
            </div>
          </FieldGroup>

          <DialogFooter className='mx-0 mb-0'>
            <Button type='button' variant='outline' onClick={onClose} disabled={mutation.isPending}>
              Vazgeç
            </Button>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
