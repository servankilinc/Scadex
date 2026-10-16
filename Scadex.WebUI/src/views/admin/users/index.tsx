import { useState } from 'react';
import { Controller, useForm, useWatch, type Control, type UseFormRegisterReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { KeyRoundIcon, PencilIcon, PlusIcon, ShieldIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { ActiveBadge, DataTable, type DataTableColumn } from '@/components/custom/data-table';
import { handleFormApiError } from '@/lib/axios-helper';
import { useCurrentUser } from '@/lib/auth-session';
import { effectivePermissionIds, groupByCategory, permissionCategoryLabel } from '@/lib/permissions';
import type { PermissionDto } from '@/models/permission';
import type { RoleDto } from '@/models/role';
import type { RolePermissionMatrixItem } from '@/models/rolePermission';
import {
  userCreateRequestSchema,
  userPasswordChangeSchema,
  userUpdateRequestSchema,
  type UserCreateRequest,
  type UserDetailDto,
  type UserPasswordChangeForm,
  type UserUpdateRequest
} from '@/models/user';
import { useCompanies } from '@/hooks/use-companies';
import { RELOGIN_NOTICE, usePermissionMatrix, usePermissions, useRoles } from '@/hooks/use-roles';
import { useChangeUserPassword, useCreateUser, useSyncUserRoles, useUpdateUser, useUserRoles, useUsers } from '@/hooks/use-users';

/**
 * Kullanıcı yönetimi — okuma da yazma da `ManageUsers` ister (rota ve sunucu). Anonim kayıt yoktur: hesapları burada
 * yönetici açar.
 *
 * Silme YOK: `User` bir `IActivatableEntity`, fiziksel silme interceptor'da istisna
 * atar. Pasife alma tek yol — pasif kullanıcı giriş yapamaz, oturumunu yenileyemez.
 *
 * Parolayı yönetici değiştirir ("Parola değiştir", eski parola sorulmaz); kullanıcının açık oturumları yeniden giriş ister.
 */
export default function Users() {
  const { data, isPending, isError, error } = useUsers();
  const roles = useRoles();
  const currentUser = useCurrentUser();
  // Pasif rol kullanıcıda atanmış kalır ama izin vermez — sütunda soluk gösterilir.
  const passiveRoleKeys = new Set((roles.data ?? []).filter(role => !role.isActive).map(role => role.name.toUpperCase()));
  const columns = buildColumns(passiveRoleKeys);
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<UserDetailDto | null>(null);
  const [assigning, setAssigning] = useState<UserDetailDto | null>(null);
  const [changingPassword, setChangingPassword] = useState<UserDetailDto | null>(null);

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

      <DataTable
        columns={columns}
        rows={data}
        getRowKey={user => user.id}
        isLoading={isPending}
        isRowMuted={user => !user.isActive}
        emptyMessage='Henüz kullanıcı yok.'
        actions={user => (
          <>
            <Button size='sm' variant='outline' onClick={() => setEditing(user)}>
              <PencilIcon />
              Düzenle
            </Button>
            <Button size='sm' variant='outline' onClick={() => setChangingPassword(user)}>
              <KeyRoundIcon />
              Parola değiştir
            </Button>
            <Button size='sm' variant='outline' onClick={() => setAssigning(user)}>
              <ShieldIcon />
              Roller
            </Button>
          </>
        )}
      />

      {/* Dialoglar koşullu mount edilir (ve `key` alır): her açılış TAZE bir form.
          `useEffect` + `form.reset` kalıbı bilerek kullanılmadı. */}
      {isCreating && <UserCreateDialog onClose={() => setIsCreating(false)} />}
      {editing && (
        <UserEditDialog key={editing.id} user={editing} isSelf={editing.id === currentUser?.id} onClose={() => setEditing(null)} />
      )}
      {assigning && <UserRolesDialog key={assigning.id} user={assigning} onClose={() => setAssigning(null)} />}
      {changingPassword && (
        <UserPasswordDialog key={changingPassword.id} user={changingPassword} onClose={() => setChangingPassword(null)} />
      )}
    </div>
  );
}

function buildColumns(passiveRoleKeys: Set<string>): DataTableColumn<UserDetailDto>[] {
  return [
    { id: 'fullName', header: 'Ad soyad', cell: user => <span className='font-medium'>{user.fullName}</span> },
    { id: 'userName', header: 'Kullanıcı adı', cell: user => user.userName ?? '—' },
    { id: 'company', header: 'Firma', cell: user => user.companyName },
    {
      id: 'roles',
      header: 'Roller',
      cell: user =>
        user.roles.length === 0 ? (
          <span className='text-muted-foreground'>—</span>
        ) : (
          <div className='flex max-w-xs flex-wrap gap-1'>
            {user.roles.map(name => {
              const isPassive = passiveRoleKeys.has(name.toUpperCase());
              return (
                <Badge key={name} variant={isPassive ? 'secondary' : 'outline'} className={isPassive ? 'opacity-60' : undefined} title={isPassive ? 'Pasif rol — izin vermez' : undefined}>
                  {name}
                </Badge>
              );
            })}
          </div>
        )
    },
    { id: 'email', header: 'E-posta', className: 'max-w-xs truncate', cell: user => user.email ?? '—' },
    { id: 'card', header: 'Kart', cell: user => (user.identityCardId ? <span className='font-mono text-xs'>{user.identityCardId}</span> : '—') },
    { id: 'active', header: 'Durum', cell: user => <ActiveBadge isActive={user.isActive} /> }
  ];
}

/**
 * Kart okuyucudan gelen HAM kart kimliği. Sunucu ham string karşılaştırması yapar
 * (normalizasyon yok) — SCADA'nın gönderdiği yazımla girilmeli. Aktif kullanıcılar
 * arasında tekildir; pasif kullanıcının kartı başkasına verilebilir.
 */
function IdentityCardField({ id, registration, error }: { id: string; registration: UseFormRegisterReturn; error?: string }) {
  return (
    <Field>
      <FieldLabel htmlFor={id}>Kart numarası</FieldLabel>
      <Input id={id} autoComplete='off' {...registration} />
      <FieldDescription>İsteğe bağlı; kart okuyucunun gönderdiği biçimde girin. Boş bırakmak kartı kaldırır.</FieldDescription>
      {error && <FieldError>{error}</FieldError>}
    </Field>
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
    defaultValues: { userName: '', fullName: '', companyId: '', email: '', phoneNumber: '', identityCardId: '', password: '' }
  });

  const mutation = useCreateUser();
  const errors = form.formState.errors;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='flex max-h-[90vh] flex-col gap-0 overflow-hidden p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Yeni kullanıcı</DialogTitle>
          <DialogDescription>Kullanıcı aktif olarak oluşturulur. Rolleri kayıttan sonra “Roller” ile atayın.</DialogDescription>
        </DialogHeader>

        {/* Scroll İÇ katmanda ve başlığın ALTINDA: yuvarlatılmış kutu scrollbar'ı köşeden kırpar, X düğmesi scrollbar'la çakışmaz. */}
        <div className='min-h-0 scrollbar-thin overflow-y-auto'>
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
                    <Select value={field.value || null} onValueChange={value => field.onChange(value ?? '')} disabled={companyOptions.length === 0}>
                      <SelectTrigger id='user-company' className='w-full'>
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

              <IdentityCardField id='user-card' registration={form.register('identityCardId')} error={errors.identityCardId?.message} />

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
        </div>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────── kullanıcı düzenle

/**
 * `GET /{id}/update` ucuna gerek YOK: `UserUpdateDto`'nun tüm alanları listede var.
 */
function UserEditDialog({ user, isSelf, onClose }: { user: UserDetailDto; isSelf: boolean; onClose: () => void }) {
  const form = useForm<UserUpdateRequest>({
    resolver: zodResolver(userUpdateRequestSchema),
    defaultValues: {
      id: user.id,
      fullName: user.fullName,
      phoneNumber: user.phoneNumber ?? '',
      identityCardId: user.identityCardId ?? '',
      isActive: user.isActive
    }
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

            <IdentityCardField id='user-edit-card' registration={form.register('identityCardId')} error={errors.identityCardId?.message} />

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

// ─────────────────────────────────────────────────────────── parola değiştir

/** Eski parola sorulmaz (yönetici işlemi). Değişince kullanıcının refresh token'ları iptal edilir, kilidi sıfırlanır. */
function UserPasswordDialog({ user, onClose }: { user: UserDetailDto; onClose: () => void }) {
  const form = useForm<UserPasswordChangeForm>({
    resolver: zodResolver(userPasswordChangeSchema),
    defaultValues: { newPassword: '', confirmPassword: '' }
  });

  const mutation = useChangeUserPassword();
  const errors = form.formState.errors;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='gap-0 p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Parola değiştir</DialogTitle>
          <DialogDescription>
            {user.fullName} ({user.userName ?? '—'}) — yeni parola hemen geçerli olur; kullanıcının açık oturumları yeniden giriş
            ister.
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={form.handleSubmit(values =>
            mutation.mutate(
              { userId: user.id, request: { newPassword: values.newPassword } },
              { onSuccess: onClose, onError: error => handleFormApiError(error, form.setError) }
            )
          )}
          noValidate>
          <FieldGroup className='p-4'>
            <Field>
              <FieldLabel htmlFor='user-new-password'>Yeni parola</FieldLabel>
              <Input id='user-new-password' type='password' autoComplete='new-password' autoFocus {...form.register('newPassword')} />
              {errors.newPassword && <FieldError>{errors.newPassword.message}</FieldError>}
            </Field>

            <Field>
              <FieldLabel htmlFor='user-confirm-password'>Yeni parola (tekrar)</FieldLabel>
              <Input id='user-confirm-password' type='password' autoComplete='new-password' {...form.register('confirmPassword')} />
              {errors.confirmPassword && <FieldError>{errors.confirmPassword.message}</FieldError>}
            </Field>
          </FieldGroup>

          <DialogFooter className='mx-0 mb-0'>
            <Button type='button' variant='outline' onClick={onClose} disabled={mutation.isPending}>
              Vazgeç
            </Button>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kaydediliyor…' : 'Parolayı değiştir'}
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
  // Önizleme kaynakları — yüklenemezlerse atama yine yapılabilir, yalnızca önizleme görünmez.
  const matrix = usePermissionMatrix();
  const permissions = usePermissions();
  const loadError = roles.error ?? assigned.error;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='flex max-h-[90vh] flex-col gap-0 overflow-hidden p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Roller — {user.fullName}</DialogTitle>
          <DialogDescription>{RELOGIN_NOTICE}</DialogDescription>
        </DialogHeader>

        {/* Scroll İÇ katmanda ve başlığın ALTINDA: yuvarlatılmış kutu scrollbar'ı köşeden kırpar, X düğmesi scrollbar'la çakışmaz. */}
        <div className='min-h-0 scrollbar-thin overflow-y-auto'>
          {/* Form ancak iki kaynak da gelince mount edilir: varsayılan değerler veriden
              okunur, sonradan `reset` gerekmez. */}
          {roles.data && assigned.data ? (
            <UserRolesForm
              userId={user.id}
              roles={roles.data}
              assigned={assigned.data}
              matrix={matrix.data}
              permissions={permissions.data}
              onClose={onClose}
            />
          ) : loadError ? (
            <p className='p-4 text-sm text-destructive'>{loadError.message}</p>
          ) : (
            <div className='flex flex-col gap-2 p-4'>
              {Array.from({ length: 3 }, (_, i) => (
                <Skeleton key={i} className='h-11 w-full rounded-lg' />
              ))}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

function UserRolesForm({
  userId,
  roles,
  assigned,
  matrix,
  permissions,
  onClose
}: {
  userId: string;
  roles: RoleDto[];
  assigned: string[];
  matrix: RolePermissionMatrixItem[] | undefined;
  permissions: PermissionDto[] | undefined;
  onClose: () => void;
}) {
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

        {matrix && permissions && <EffectivePermissionsPreview control={form.control} roles={roles} matrix={matrix} permissions={permissions} />}
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

/**
 * Seçili rollerin verdiği izinlerin canlı önizlemesi. Pasif rol izin vermez (sunucudaki kuralın aynası, `effectivePermissionIds`).
 * `useWatch` — `form.watch()` DEĞİL: React Compiler onu memoize edemiyor.
 */
function EffectivePermissionsPreview({
  control,
  roles,
  matrix,
  permissions
}: {
  control: Control<{ roleNames: string[] }>;
  roles: RoleDto[];
  matrix: RolePermissionMatrixItem[];
  permissions: PermissionDto[];
}) {
  const roleNames = useWatch({ control, name: 'roleNames' });
  const granted = effectivePermissionIds(roleNames, roles, matrix);
  const groups = groupByCategory(permissions.filter(permission => granted.has(permission.id)));

  return (
    <div className='mt-2 rounded-lg border bg-muted/40 p-3'>
      <p className='text-sm font-medium'>Etkin izinler ({granted.size})</p>
      {groups.length === 0 ? (
        <p className='mt-1 text-sm text-muted-foreground'>Seçili rollerle bu kullanıcı hiçbir izne sahip olmaz; yalnızca giriş isteyen ekranları görür.</p>
      ) : (
        <div className='mt-2 flex flex-col gap-2'>
          {groups.map(([category, items]) => (
            <div key={category} className='flex flex-col gap-1'>
              <p className='text-xs font-medium tracking-wide text-muted-foreground uppercase'>{permissionCategoryLabel(category)}</p>
              <div className='flex flex-wrap gap-1'>
                {items.map(permission => (
                  <Badge key={permission.id} variant='outline' title={permission.code}>
                    {permission.displayName}
                  </Badge>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
