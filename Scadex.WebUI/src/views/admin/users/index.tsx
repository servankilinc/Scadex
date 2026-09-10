import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { PencilIcon, PlusIcon, ShieldIcon, UserIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { handleFormApiError } from '@/lib/axios-helper';
import { useCurrentUser } from '@/lib/auth-session';
import type { RoleDto } from '@/models/role';
import { userCreateRequestSchema, userUpdateRequestSchema, type UserCreateRequest, type UserDetailDto, type UserUpdateRequest } from '@/models/user';
import { useCompanies } from '@/hooks/use-companies';
import { useRoles } from '@/hooks/use-roles';
import { useCreateUser, useSyncUserRoles, useUpdateUser, useUserRoles, useUsers } from '@/hooks/use-users';

/**
 * Kullanıcı yönetimi.
 *
 * Silme YOK: `User` bir `IActivatableEntity`, fiziksel silme interceptor'da istisna
 * atar. Pasife alma tek yol — pasif kullanıcı giriş yapamaz, oturumunu yenileyemez.
 *
 * Parola yalnızca oluştururken verilir; yöneticinin parola sıfırlama ucu bilerek yok.
 */
export default function Users() {
  const { data, isPending, isError, error } = useUsers();
  const currentUser = useCurrentUser();
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<UserDetailDto | null>(null);
  const [assigning, setAssigning] = useState<UserDetailDto | null>(null);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Kullanıcılar</h1>
          <p className='text-sm text-muted-foreground'>Sisteme giriş yapabilen hesaplar ve rolleri.</p>
        </div>
        <Button size='sm' onClick={() => setIsCreating(true)}>
          <PlusIcon />
          Yeni kullanıcı
        </Button>
      </div>

      {isError && <p className='text-sm text-destructive'>{error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3'>
        {isPending && Array.from({ length: 3 }, (_, i) => <Skeleton key={i} className='h-40 w-full rounded-xl' />)}
        {data?.map(user => (
          <UserCard
            key={user.id}
            user={user}
            isSelf={user.id === currentUser?.id}
            onEdit={() => setEditing(user)}
            onRoles={() => setAssigning(user)}
          />
        ))}
      </div>

      {data?.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>Henüz kullanıcı yok.</CardContent>
        </Card>
      )}

      {/* Dialoglar koşullu mount edilir (ve `key` alır): her açılış TAZE bir form.
          `useEffect` + `form.reset` kalıbı bilerek kullanılmadı. */}
      {isCreating && <UserCreateDialog onClose={() => setIsCreating(false)} />}
      {editing && (
        <UserEditDialog key={editing.id} user={editing} isSelf={editing.id === currentUser?.id} onClose={() => setEditing(null)} />
      )}
      {assigning && <UserRolesDialog key={assigning.id} user={assigning} onClose={() => setAssigning(null)} />}
    </div>
  );
}

function UserCard({ user, isSelf, onEdit, onRoles }: { user: UserDetailDto; isSelf: boolean; onEdit: () => void; onRoles: () => void }) {
  return (
    <Card className={user.isActive ? undefined : 'opacity-60'}>
      <CardHeader>
        <CardTitle className='flex items-center gap-2'>
          <UserIcon className='size-4 shrink-0' />
          <span className='truncate'>{user.fullName}</span>
        </CardTitle>
        <CardDescription className='truncate'>
          {user.userName ?? '—'} · {user.companyName}
        </CardDescription>
      </CardHeader>

      <CardContent className='flex flex-col gap-3'>
        {user.email && <p className='truncate text-sm text-muted-foreground'>{user.email}</p>}

        {/* Pasif kayitlar listede GORUNUR — geri alinabilsin diye. */}
        {(isSelf || !user.isActive) && (
          <div className='flex flex-wrap gap-1.5'>
            {isSelf && <Badge variant='outline'>Siz</Badge>}
            {!user.isActive && <Badge variant='secondary'>Pasif</Badge>}
          </div>
        )}

        <div className='grid grid-cols-2 gap-2'>
          <Button size='sm' variant='outline' onClick={onEdit}>
            <PencilIcon />
            Düzenle
          </Button>
          <Button size='sm' variant='outline' onClick={onRoles}>
            <ShieldIcon />
            Roller
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── yeni kullanıcı

function UserCreateDialog({ onClose }: { onClose: () => void }) {
  const companies = useCompanies();
  // Yalnızca AKTİF firmalar — pasife alınmış firmaya yeni kullanıcı bağlanmamalı
  // (kabin formundaki gerekçenin aynısı). Firma sonradan değiştirilemez.
  const companyOptions = (companies.data ?? []).filter(company => company.isActive);

  const form = useForm<UserCreateRequest>({
    resolver: zodResolver(userCreateRequestSchema),
    defaultValues: { userName: '', fullName: '', companyId: '', email: '', phoneNumber: '', password: '' }
  });

  const mutation = useCreateUser();
  const errors = form.formState.errors;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='max-h-[90vh] gap-0 overflow-y-auto p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Yeni kullanıcı</DialogTitle>
          <DialogDescription>Kullanıcı aktif olarak oluşturulur. Rolleri kayıttan sonra “Roller” ile atayın.</DialogDescription>
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
              <FieldLabel htmlFor='user-name'>Kullanıcı adı</FieldLabel>
              <Input id='user-name' autoFocus autoComplete='off' {...form.register('userName')} />
              <FieldDescription>Giriş için kullanılır; sonradan değiştirilemez.</FieldDescription>
              {errors.userName && <FieldError>{errors.userName.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='user-full-name'>Ad soyad</FieldLabel>
              <Input id='user-full-name' {...form.register('fullName')} />
              {errors.fullName && <FieldError>{errors.fullName.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='user-company'>Firma</FieldLabel>
              <Controller
                control={form.control}
                name='companyId'
                render={({ field }) => (
                  <Select value={field.value} onValueChange={value => field.onChange(value ?? '')} disabled={companyOptions.length === 0}>
                    <SelectTrigger id='user-company' className='w-full'>
                      <SelectValue placeholder='Firma seçin'>{companyOptions.find(company => company.id === field.value)?.name}</SelectValue>
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
              {errors.companyId && <FieldError>{errors.companyId.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='user-email'>E-posta</FieldLabel>
              <Input id='user-email' type='email' autoComplete='off' {...form.register('email')} />
              {/* Duzenleme formunda YOK: UserUpdateDto bu alani tasimiyor. */}
              <FieldDescription>İsteğe bağlı; yalnızca oluştururken yazılabilir.</FieldDescription>
              {errors.email && <FieldError>{errors.email.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='user-phone'>Telefon</FieldLabel>
              <Input id='user-phone' type='tel' {...form.register('phoneNumber')} />
              {errors.phoneNumber && <FieldError>{errors.phoneNumber.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='user-password'>Parola</FieldLabel>
              <Input id='user-password' type='password' autoComplete='new-password' {...form.register('password')} />
              {errors.password && <FieldError>{errors.password.message}</FieldError>}
            </Field>
          </FieldGroup>

          <DialogFooter className='mx-0 mb-0'>
            <Button type='button' variant='outline' onClick={onClose} disabled={mutation.isPending}>
              Vazgeç
            </Button>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kaydediliyor…' : 'Kullanıcıyı oluştur'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────── kullanıcı düzenle

/**
 * `GET /{id}/update` ucuna gerek YOK: `UserUpdateDto`'nun dört alanı da listede var.
 */
function UserEditDialog({ user, isSelf, onClose }: { user: UserDetailDto; isSelf: boolean; onClose: () => void }) {
  const form = useForm<UserUpdateRequest>({
    resolver: zodResolver(userUpdateRequestSchema),
    defaultValues: { id: user.id, fullName: user.fullName, phoneNumber: user.phoneNumber ?? '', isActive: user.isActive }
  });

  const mutation = useUpdateUser();
  const errors = form.formState.errors;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='gap-0 p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Kullanıcıyı düzenle</DialogTitle>
          <DialogDescription>
            {user.userName ?? user.fullName} — kullanıcı adı, e-posta ve firma burada değiştirilemez.
          </DialogDescription>
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
              <FieldLabel htmlFor='user-edit-full-name'>Ad soyad</FieldLabel>
              <Input id='user-edit-full-name' autoFocus {...form.register('fullName')} />
              {errors.fullName && <FieldError>{errors.fullName.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='user-edit-phone'>Telefon</FieldLabel>
              <Input id='user-edit-phone' type='tel' {...form.register('phoneNumber')} />
              {errors.phoneNumber && <FieldError>{errors.phoneNumber.message}</FieldError>}
            </Field>

            <div className='rounded-lg border p-3'>
              <Field orientation='horizontal'>
                <FieldLabel htmlFor='user-edit-active'>Aktif</FieldLabel>
                {/* `form.watch()` DEĞİL: React Compiler onu memoize edemiyor. */}
                <Controller
                  control={form.control}
                  name='isActive'
                  render={({ field }) => (
                    <Switch
                      id='user-edit-active'
                      checked={field.value}
                      onCheckedChange={checked => field.onChange(checked)}
                      // Login `IsActive`'e baktığı için kendini pasife alan yönetici
                      // sistemden kilitlenirdi. Sunucu da bunu 400 ile reddeder.
                      disabled={isSelf}
                    />
                  )}
                />
              </Field>
              <FieldDescription className='mt-2'>
                {isSelf ? 'Kendi hesabınızı pasife alamazsınız.' : 'Pasif kullanıcı giriş yapamaz ve oturumunu yenileyemez.'}
              </FieldDescription>
              {errors.isActive && <FieldError>{errors.isActive.message}</FieldError>}
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

// ─────────────────────────────────────────────────────────── roller

function UserRolesDialog({ user, onClose }: { user: UserDetailDto; onClose: () => void }) {
  const roles = useRoles();
  const assigned = useUserRoles(user.id);
  const loadError = roles.error ?? assigned.error;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='max-h-[90vh] gap-0 overflow-y-auto p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Roller — {user.fullName}</DialogTitle>
          <DialogDescription>Değişiklik, kullanıcının bir sonraki girişinde ya da oturum yenilemesinde geçerli olur.</DialogDescription>
        </DialogHeader>

        {/* Form ancak iki kaynak da gelince mount edilir: varsayılan değerler veriden
            okunur, sonradan `reset` gerekmez. */}
        {roles.data && assigned.data ? (
          <UserRolesForm userId={user.id} roles={roles.data} assigned={assigned.data} onClose={onClose} />
        ) : loadError ? (
          <p className='p-4 text-sm text-destructive'>{loadError.message}</p>
        ) : (
          <div className='flex flex-col gap-2 p-4'>
            {Array.from({ length: 3 }, (_, i) => (
              <Skeleton key={i} className='h-11 w-full rounded-lg' />
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

function UserRolesForm({ userId, roles, assigned, onClose }: { userId: string; roles: RoleDto[]; assigned: string[]; onClose: () => void }) {
  // Identity rol adlarını büyük/küçük harfe duyarsız eşler; sunucu da öyle karşılaştırıyor.
  const assignedKeys = new Set(assigned.map(name => name.toUpperCase()));
  const isAssigned = (role: RoleDto) => assignedKeys.has(role.name.toUpperCase());

  // Pasif rol yalnızca ZATEN atanmışsa listelenir: çıkarılabilir, ama yeni bir
  // kullanıcıya atanamaz (sunucu 400 döner).
  const visibleRoles = roles.filter(role => role.isActive || isAssigned(role));

  const form = useForm<{ roleNames: string[] }>({
    defaultValues: { roleNames: roles.filter(isAssigned).map(role => role.name) }
  });

  const mutation = useSyncUserRoles();
  const errors = form.formState.errors;

  return (
    <form
      onSubmit={form.handleSubmit(values =>
        mutation.mutate(
          { userId, roleNames: values.roleNames },
          {
            onSuccess: onClose,
            onError: error => handleFormApiError(error, form.setError)
          }
        )
      )}
      noValidate>
      <div className='flex flex-col gap-2 p-4'>
        {visibleRoles.length === 0 && <p className='text-sm text-muted-foreground'>Tanımlı aktif rol yok.</p>}

        <Controller
          control={form.control}
          name='roleNames'
          render={({ field }) => (
            <>
              {visibleRoles.map(role => {
                const id = `user-role-${role.id}`;
                const checked = field.value.includes(role.name);
                return (
                  <Field key={role.id} orientation='horizontal' className='rounded-lg border p-3'>
                    <Checkbox
                      id={id}
                      checked={checked}
                      onCheckedChange={next =>
                        field.onChange(next ? [...field.value, role.name] : field.value.filter(name => name !== role.name))
                      }
                    />
                    <FieldLabel htmlFor={id} className='flex-1'>
                      {role.name}
                    </FieldLabel>
                    {role.isImmutable && <Badge variant='outline'>Sistem</Badge>}
                    {!role.isActive && <Badge variant='secondary'>Pasif</Badge>}
                  </Field>
                );
              })}
            </>
          )}
        />

        {errors.roleNames && <FieldError>{errors.roleNames.message}</FieldError>}
      </div>

      <DialogFooter className='mx-0 mb-0'>
        <Button type='button' variant='outline' onClick={onClose} disabled={mutation.isPending}>
          Vazgeç
        </Button>
        <Button type='submit' disabled={mutation.isPending}>
          {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
        </Button>
      </DialogFooter>
    </form>
  );
}
