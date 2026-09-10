import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { KeyRoundIcon, PencilIcon, PlusIcon, ShieldIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { handleFormApiError } from '@/lib/axios-helper';
import type { PermissionDto } from '@/models/permission';
import { roleCreateRequestSchema, roleUpdateRequestSchema, type RoleCreateRequest, type RoleDto, type RoleUpdateRequest } from '@/models/role';
import type { RolePermissionDto } from '@/models/rolePermission';
import { useCreateRole, usePermissions, useRolePermissions, useRoles, useSyncRolePermissions, useUpdateRole } from '@/hooks/use-roles';

/**
 * Rol ve izin yönetimi.
 *
 * Silme YOK: `Role` bir `IActivatableEntity`. Pasif rol kullanıcılarda atanmış
 * kalır ama izin türetmez (`AuthService.GetPermissionCodesAsync`).
 *
 * Sistem rolleri (`isImmutable`) yeniden adlandırılamaz ve pasife alınamaz; izinleri
 * yine düzenlenebilir — seed'deki dört rolün hepsi sistem rolü.
 *
 * İzinler bugün YALNIZCA token'a yazılır, hiçbir uç onları zorlamaz (§7).
 */
export default function Roles() {
  const { data, isPending, isError, error } = useRoles();
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<RoleDto | null>(null);
  const [granting, setGranting] = useState<RoleDto | null>(null);

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div className='flex flex-wrap items-start justify-between gap-3'>
        <div>
          <h1 className='text-lg font-semibold'>Roller</h1>
          <p className='text-sm text-muted-foreground'>Kullanıcılara atanan roller ve her rolün verdiği izinler.</p>
        </div>
        <Button size='sm' onClick={() => setIsCreating(true)}>
          <PlusIcon />
          Yeni rol
        </Button>
      </div>

      {isError && <p className='text-sm text-destructive'>{error.message}</p>}

      <div className='grid gap-3 sm:grid-cols-2 lg:grid-cols-3'>
        {isPending && Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className='h-32 w-full rounded-xl' />)}
        {data?.map(role => (
          <RoleCard key={role.id} role={role} onEdit={() => setEditing(role)} onPermissions={() => setGranting(role)} />
        ))}
      </div>

      {data?.length === 0 && (
        <Card>
          <CardContent className='py-8 text-center text-sm text-muted-foreground'>Henüz rol yok.</CardContent>
        </Card>
      )}

      {/* Dialoglar koşullu mount edilir (ve `key` alır): her açılış TAZE bir form. */}
      {isCreating && <RoleCreateDialog onClose={() => setIsCreating(false)} />}
      {editing && <RoleEditDialog key={editing.id} role={editing} onClose={() => setEditing(null)} />}
      {granting && <RolePermissionsDialog key={granting.id} role={granting} onClose={() => setGranting(null)} />}
    </div>
  );
}

function RoleCard({ role, onEdit, onPermissions }: { role: RoleDto; onEdit: () => void; onPermissions: () => void }) {
  return (
    <Card className={role.isActive ? undefined : 'opacity-60'}>
      <CardHeader>
        <CardTitle className='flex items-center gap-2'>
          <ShieldIcon className='size-4 shrink-0' />
          <span className='truncate'>{role.name}</span>
        </CardTitle>
      </CardHeader>

      <CardContent className='flex flex-col gap-3'>
        {(role.isImmutable || !role.isActive) && (
          <div className='flex flex-wrap gap-1.5'>
            {role.isImmutable && <Badge variant='outline'>Sistem rolü</Badge>}
            {/* Pasif kayitlar listede GORUNUR — geri alinabilsin diye. */}
            {!role.isActive && <Badge variant='secondary'>Pasif</Badge>}
          </div>
        )}

        <div className='grid grid-cols-2 gap-2'>
          {/* Sistem rolünün adı ve aktifliği kilitli — sunucu 403 döner. */}
          <Button size='sm' variant='outline' onClick={onEdit} disabled={role.isImmutable}>
            <PencilIcon />
            Düzenle
          </Button>
          <Button size='sm' variant='outline' onClick={onPermissions}>
            <KeyRoundIcon />
            İzinler
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

// ─────────────────────────────────────────────────────────── yeni rol

function RoleCreateDialog({ onClose }: { onClose: () => void }) {
  const form = useForm<RoleCreateRequest>({
    resolver: zodResolver(roleCreateRequestSchema),
    defaultValues: { name: '' }
  });

  const mutation = useCreateRole();
  const errors = form.formState.errors;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='gap-0 p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Yeni rol</DialogTitle>
          <DialogDescription>Rol izinsiz ve aktif olarak oluşturulur. İzinleri kayıttan sonra “İzinler” ile verin.</DialogDescription>
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
              <FieldLabel htmlFor='role-name'>Ad</FieldLabel>
              <Input id='role-name' autoFocus {...form.register('name')} />
              {errors.name && <FieldError>{errors.name.message}</FieldError>}
            </Field>
          </FieldGroup>

          <DialogFooter className='mx-0 mb-0'>
            <Button type='button' variant='outline' onClick={onClose} disabled={mutation.isPending}>
              Vazgeç
            </Button>
            <Button type='submit' disabled={mutation.isPending}>
              {mutation.isPending ? 'Kaydediliyor…' : 'Rolü oluştur'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ─────────────────────────────────────────────────────────── rol düzenle

function RoleEditDialog({ role, onClose }: { role: RoleDto; onClose: () => void }) {
  const form = useForm<RoleUpdateRequest>({
    resolver: zodResolver(roleUpdateRequestSchema),
    defaultValues: { id: role.id, name: role.name, isActive: role.isActive }
  });

  const mutation = useUpdateRole();
  const errors = form.formState.errors;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='gap-0 p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Rolü düzenle</DialogTitle>
          <DialogDescription>Ad değişikliği, rolü taşıyan kullanıcıların bir sonraki girişinde token'a yansır.</DialogDescription>
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
              <FieldLabel htmlFor='role-edit-name'>Ad</FieldLabel>
              <Input id='role-edit-name' autoFocus {...form.register('name')} />
              {errors.name && <FieldError>{errors.name.message}</FieldError>}
            </Field>

            <div className='rounded-lg border p-3'>
              <Field orientation='horizontal'>
                <FieldLabel htmlFor='role-edit-active'>Aktif</FieldLabel>
                <Controller
                  control={form.control}
                  name='isActive'
                  render={({ field }) => (
                    <Switch id='role-edit-active' checked={field.value} onCheckedChange={checked => field.onChange(checked)} />
                  )}
                />
              </Field>
              <FieldDescription className='mt-2'>Pasif rol izin vermez; kullanıcılarda atanmış kalır ve tekrar aktifleştirilebilir.</FieldDescription>
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

// ─────────────────────────────────────────────────────────── izinler

function RolePermissionsDialog({ role, onClose }: { role: RoleDto; onClose: () => void }) {
  const permissions = usePermissions();
  const granted = useRolePermissions(role.id);
  const loadError = permissions.error ?? granted.error;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='max-h-[90vh] gap-0 overflow-y-auto p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>İzinler — {role.name}</DialogTitle>
          <DialogDescription>
            Değişiklik, bu role sahip kullanıcıların bir sonraki girişinde ya da oturum yenilemesinde geçerli olur.
          </DialogDescription>
        </DialogHeader>

        {/* Form ancak iki kaynak da gelince mount edilir: varsayılan değerler veriden okunur. */}
        {permissions.data && granted.data ? (
          <RolePermissionsForm roleId={role.id} permissions={permissions.data} granted={granted.data} onClose={onClose} />
        ) : loadError ? (
          <p className='p-4 text-sm text-destructive'>{loadError.message}</p>
        ) : (
          <div className='flex flex-col gap-2 p-4'>
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className='h-14 w-full rounded-lg' />
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** Katalogdaki sırayı koruyarak kategoriye göre gruplar (`Object.groupBy` hedef lib'e bağlı, kullanılmadı). */
function groupByCategory(permissions: PermissionDto[]): [string, PermissionDto[]][] {
  const groups = new Map<string, PermissionDto[]>();
  for (const permission of permissions) {
    const group = groups.get(permission.category);
    if (group) group.push(permission);
    else groups.set(permission.category, [permission]);
  }
  return [...groups.entries()];
}

function RolePermissionsForm({
  roleId,
  permissions,
  granted,
  onClose
}: {
  roleId: string;
  permissions: PermissionDto[];
  granted: RolePermissionDto[];
  onClose: () => void;
}) {
  const form = useForm<{ permissionIds: number[] }>({
    defaultValues: { permissionIds: granted.map(item => item.permissionId) }
  });

  const mutation = useSyncRolePermissions();
  const groups = groupByCategory(permissions);

  return (
    <form
      onSubmit={form.handleSubmit(values =>
        mutation.mutate(
          { roleId, permissionIds: values.permissionIds },
          {
            onSuccess: onClose,
            onError: error => handleFormApiError(error, form.setError)
          }
        )
      )}
      noValidate>
      <Controller
        control={form.control}
        name='permissionIds'
        render={({ field }) => (
          <div className='flex flex-col gap-4 p-4'>
            {groups.map(([category, items]) => (
              <div key={category} className='flex flex-col gap-2'>
                <p className='text-xs font-medium tracking-wide text-muted-foreground uppercase'>{category}</p>
                {items.map(permission => {
                  const id = `role-permission-${permission.id}`;
                  const checked = field.value.includes(permission.id);
                  return (
                    <Field key={permission.id} orientation='horizontal' className='rounded-lg border p-3'>
                      <Checkbox
                        id={id}
                        checked={checked}
                        onCheckedChange={next =>
                          field.onChange(next ? [...field.value, permission.id] : field.value.filter(value => value !== permission.id))
                        }
                      />
                      <FieldLabel htmlFor={id} className='flex flex-1 flex-col items-start gap-0.5'>
                        <span>{permission.displayName}</span>
                        <span className='font-mono text-xs font-normal text-muted-foreground'>{permission.code}</span>
                      </FieldLabel>
                    </Field>
                  );
                })}
              </div>
            ))}
          </div>
        )}
      />

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
