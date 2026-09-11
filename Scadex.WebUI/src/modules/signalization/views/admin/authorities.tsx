import { Controller, useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link } from 'react-router';
import { PlusIcon, RotateCcwIcon, Trash2Icon, TriangleAlertIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { FieldError } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { useRoles } from '@/hooks/use-roles';
import { newId } from '@/lib/sequential-id';
import type { RoleDto } from '@/models/role';
import { useSaveSignalAuthorities, useSignalAuthorities } from '../../hooks/use-signal-config';
import { handleTreeFormApiError } from '../../lib';
import { signalAuthorityFormSchema, toAuthoritySaveRequest, type SignalAuthorityDto, type SignalAuthorityFormValues } from '../../models/authority';

/**
 * Kurumlar — `/signalization/authorities`.
 *
 * Kurum = iç kapı yetkisi taşıyan ROL (Belediye, Emniyet, Sinyalizasyon…). Ayrı bir yetki tablosu yoktur:
 * kullanıcıya kurumun rolü verilince (Operatörler ekranı) o kurumun iç kapısını kartıyla açar.
 *
 * Liste TAM olarak kaydedilir: listeden çıkarılan kurum PASİFE alınır (silinmez — geçmiş oturumlar kurum adını
 * taşır). Aktif bir iç kapıda kullanılan kurum çıkarılamaz; sunucu 400 döner.
 */
export default function SignalAuthorities() {
  const authorities = useSignalAuthorities();
  const roles = useRoles();
  const loadError = authorities.error ?? roles.error;

  return (
    <div className='flex max-w-4xl flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Kurumlar</h1>
        <p className='text-sm text-muted-foreground'>
          Her kurum bir role bağlıdır ve kabinlerde en fazla bir iç kapıyı yönetir. Rolleri{' '}
          <Link to='/admin/roles' className='underline underline-offset-4'>
            Roller
          </Link>{' '}
          ekranında oluşturun.
        </p>
      </div>

      {loadError && <p className='text-sm text-destructive'>{loadError.message}</p>}

      {/* Form ancak iki kaynak da gelince mount edilir: varsayılanlar veriden okunur, `reset` efekti gerekmez. */}
      {authorities.data && roles.data ? (
        <AuthorityForm authorities={authorities.data} roles={roles.data} />
      ) : (
        !loadError && <Skeleton className='h-64 w-full rounded-xl' />
      )}
    </div>
  );
}

function AuthorityForm({ authorities, roles }: { authorities: SignalAuthorityDto[]; roles: RoleDto[] }) {
  const form = useForm<SignalAuthorityFormValues>({
    resolver: zodResolver(signalAuthorityFormSchema),
    defaultValues: {
      authorities: authorities.filter(a => a.isActive).map(a => ({ authorityId: a.id, name: a.name, roleId: a.roleId }))
    }
  });

  // `field.id` RHF'nin ürettiği render anahtarıdır; kurum kimliği `authorityId` alanında durur.
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'authorities' });
  const mutation = useSaveSignalAuthorities();
  const errors = form.formState.errors;

  const byId = new Map(authorities.map(a => [a.id, a]));
  const listedIds = new Set(fields.map(field => field.authorityId));
  const restorable = authorities.filter(a => !a.isActive && !listedIds.has(a.id));

  const submit = form.handleSubmit(values =>
    mutation.mutate(toAuthoritySaveRequest(values), {
      onSuccess: () => form.reset(values),
      onError: error => handleTreeFormApiError(error, form.setError)
    })
  );

  return (
    <form onSubmit={submit} noValidate className='flex flex-col gap-4'>
      <Card>
        <CardHeader>
          <CardTitle>Aktif kurumlar</CardTitle>
          <CardDescription>Listeden çıkarılan kurum kaydedince pasife alınır; geçmiş işlemlerdeki adı değişmez.</CardDescription>
        </CardHeader>
        <CardContent className='flex flex-col gap-2'>
          {fields.length === 0 && <p className='text-sm text-muted-foreground'>Henüz kurum yok.</p>}

          {fields.map((field, index) => {
            const saved = byId.get(field.authorityId);
            const rowErrors = errors.authorities?.[index];

            return (
              <div key={field.id} className='flex flex-col gap-1.5 rounded-lg border p-3'>
                <div className='flex flex-wrap items-start gap-2'>
                  <div className='min-w-[12rem] flex-1'>
                    <Input aria-label='Kurum adı' placeholder='Kurum adı (örn. Belediye)' {...form.register(`authorities.${index}.name`)} />
                    {rowErrors?.name && <FieldError>{rowErrors.name.message}</FieldError>}
                  </div>

                  <div className='min-w-[12rem] flex-1'>
                    <Controller
                      control={form.control}
                      name={`authorities.${index}.roleId`}
                      render={({ field: roleField }) => (
                        <RoleSelect roles={roles} value={roleField.value} onChange={roleField.onChange} />
                      )}
                    />
                    {rowErrors?.roleId && <FieldError>{rowErrors.roleId.message}</FieldError>}
                  </div>

                  <Button type='button' size='icon' variant='ghost' aria-label='Kurumu çıkar' onClick={() => remove(index)}>
                    <Trash2Icon />
                  </Button>
                </div>

                {saved && !saved.roleIsActive && (
                  <p className='flex items-center gap-1.5 text-xs text-amber-600 dark:text-amber-400'>
                    <TriangleAlertIcon className='size-3.5' />
                    Bağlı rol {saved.roleName ? 'pasif' : 'silinmiş'} — bu kurumla hiçbir kart kapı açamaz.
                  </p>
                )}
              </div>
            );
          })}

          {(errors.authorities?.message ?? errors.authorities?.root?.message) && (
            <FieldError>{errors.authorities?.message ?? errors.authorities?.root?.message}</FieldError>
          )}

          <div>
            <Button type='button' size='sm' variant='outline' onClick={() => append({ authorityId: newId(), name: '', roleId: '' })}>
              <PlusIcon />
              Kurum ekle
            </Button>
          </div>
        </CardContent>
      </Card>

      {restorable.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Pasif kurumlar</CardTitle>
            <CardDescription>Geri alınan kurum kaydedince yeniden aktif olur.</CardDescription>
          </CardHeader>
          <CardContent className='flex flex-col gap-2'>
            {restorable.map(authority => (
              <div key={authority.id} className='flex items-center justify-between gap-2 rounded-lg border p-2.5 text-sm'>
                <div className='flex min-w-0 items-center gap-2'>
                  <span className='truncate font-medium'>{authority.name}</span>
                  <Badge variant='secondary'>{authority.roleName ?? 'rol silinmiş'}</Badge>
                </div>
                <Button
                  type='button'
                  size='sm'
                  variant='ghost'
                  onClick={() => append({ authorityId: authority.id, name: authority.name, roleId: authority.roleId })}>
                  <RotateCcwIcon />
                  Geri al
                </Button>
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      <div className='flex justify-end gap-2'>
        <Button type='button' variant='outline' disabled={mutation.isPending || !form.formState.isDirty} onClick={() => form.reset()}>
          Vazgeç
        </Button>
        <Button type='submit' disabled={mutation.isPending || !form.formState.isDirty}>
          {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
        </Button>
      </div>
    </form>
  );
}

/** Pasif rol yalnızca ZATEN seçiliyse listelenir — yeni bir kuruma bağlanmamalı (yetki türetmez). */
function RoleSelect({ roles, value, onChange }: { roles: RoleDto[]; value: string; onChange: (value: string) => void }) {
  const options = roles.filter(role => role.isActive || role.id === value);
  const selected = roles.find(role => role.id === value);

  return (
    <Select value={value || null} onValueChange={next => onChange(next ?? '')}>
      <SelectTrigger aria-label='Rol' className='w-full'>
        <SelectValue placeholder='Rol seçin'>{selected ? `${selected.name}${selected.isActive ? '' : ' (pasif)'}` : undefined}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {options.map(role => (
          <SelectItem key={role.id} value={role.id}>
            {role.name}
            {!role.isActive && ' (pasif)'}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
