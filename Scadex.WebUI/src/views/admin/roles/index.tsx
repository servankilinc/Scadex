import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { KeyRoundIcon, PencilIcon, PlusIcon, UserMinusIcon, UserPlusIcon, UsersIcon } from 'lucide-react';
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
import { handleFormApiError, toApiError } from '@/lib/axios-helper';
import { groupByCategory, permissionCategoryLabel } from '@/lib/permissions';
import type { PermissionDto } from '@/models/permission';
import { toast } from 'sonner';
import { roleCreateRequestSchema, roleUpdateRequestSchema, type RoleCreateRequest, type RoleDto, type RoleUpdateRequest } from '@/models/role';
import type { RolePermissionDto } from '@/models/rolePermission';
import {
  RELOGIN_NOTICE,
  useAddRoleMember,
  useCreateRole,
  usePermissions,
  useRemoveRoleMember,
  useRoleMembers,
  useRolePermissions,
  useRoles,
  useSyncRolePermissions,
  useUpdateRole
} from '@/hooks/use-roles';
import { useUsers } from '@/hooks/use-users';

/**
 * Rol ve izin yönetimi.
 *
 * Silme YOK: `Role` bir `IActivatableEntity`. Pasif rol kullanıcılarda atanmış
 * kalır ama izin türetmez (`AuthService.GetPermissionCodesAsync`).
 *
 * Sistem rolleri (`isImmutable`) yeniden adlandırılamaz ve pasife alınamaz; izinleri
 * yine düzenlenebilir — seed'deki dört rolün hepsi sistem rolü.
 *
 * İzinler sunucuda `[RequirePermission]` ile zorlanır ve token'a girişte yazılır: değişiklik, rolü taşıyan kullanıcıların
 * bir sonraki girişinde geçerli olur.
 */
export default function Roles() {
  const { data, isPending, isError, error } = useRoles();
  const [isCreating, setIsCreating] = useState(false);
  const [editing, setEditing] = useState<RoleDto | null>(null);
  const [granting, setGranting] = useState<RoleDto | null>(null);
  const [viewingMembers, setViewingMembers] = useState<RoleDto | null>(null);

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

      <DataTable
        columns={COLUMNS}
        rows={data}
        getRowKey={role => role.id}
        isLoading={isPending}
        isRowMuted={role => !role.isActive}
        emptyMessage='Henüz rol yok.'
        actions={role => (
          <>
            {/* Sistem rolünün adı ve aktifliği kilitli — sunucu 403 döner. */}
            <Button size='sm' variant='outline' onClick={() => setEditing(role)} disabled={role.isImmutable}>
              <PencilIcon />
              Düzenle
            </Button>
            <Button size='sm' variant='outline' onClick={() => setGranting(role)}>
              <KeyRoundIcon />
              İzinler
            </Button>
            <Button size='sm' variant='outline' onClick={() => setViewingMembers(role)}>
              <UsersIcon />
              Üyeler
            </Button>
          </>
        )}
      />

      {/* Dialoglar koşullu mount edilir (ve `key` alır): her açılış TAZE bir form. */}
      {isCreating && <RoleCreateDialog onClose={() => setIsCreating(false)} />}
      {editing && <RoleEditDialog key={editing.id} role={editing} onClose={() => setEditing(null)} />}
      {granting && <RolePermissionsDialog key={granting.id} role={granting} onClose={() => setGranting(null)} />}
      {viewingMembers && <RoleMembersDialog key={viewingMembers.id} role={viewingMembers} onClose={() => setViewingMembers(null)} />}
    </div>
  );
}

const COLUMNS: DataTableColumn<RoleDto>[] = [
  { id: 'name', header: 'Rol', cell: role => <span className='font-medium'>{role.name}</span> },
  {
    id: 'kind',
    header: 'Tür',
    cell: role => (role.isImmutable ? <Badge variant='outline'>Sistem rolü</Badge> : <span className='text-muted-foreground'>Özel</span>)
  },
  { id: 'users', header: 'Kullanıcı', cell: role => <span className='tabular-nums'>{role.userCount}</span> },
  { id: 'active', header: 'Durum', cell: role => <ActiveBadge isActive={role.isActive} /> }
];

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
          <DialogDescription>{RELOGIN_NOTICE}</DialogDescription>
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

// ─────────────────────────────────────────────────────────── izinler

function RolePermissionsDialog({ role, onClose }: { role: RoleDto; onClose: () => void }) {
  const permissions = usePermissions();
  const granted = useRolePermissions(role.id);
  const loadError = permissions.error ?? granted.error;

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='flex max-h-[90vh] flex-col gap-0 overflow-hidden p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>İzinler — {role.name}</DialogTitle>
          <DialogDescription>{RELOGIN_NOTICE}</DialogDescription>
        </DialogHeader>

        {/* Scroll İÇ katmanda ve başlığın ALTINDA: yuvarlatılmış kutu scrollbar'ı köşeden kırpar, X düğmesi scrollbar'la çakışmaz. */}
        <div className='min-h-0 scrollbar-thin overflow-y-auto'>
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
        </div>
      </DialogContent>
    </Dialog>
  );
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
  const errors = form.formState.errors;
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
                <p className='text-xs font-medium tracking-wide text-muted-foreground uppercase'>{permissionCategoryLabel(category)}</p>
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
            {/* Bilinmeyen izin (400, errors.PermissionIds). */}
            {errors.permissionIds && <FieldError>{errors.permissionIds.message}</FieldError>}
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

// ─────────────────────────────────────────────────────────── üyeler

/**
 * Rolün kullanıcıları: listeden çıkar, aktif kullanıcılar arasından ekle. Kullanıcının bütün rollerini düzenlemek Kullanıcılar
 * ekranındadır; burası rol yönünden aynı ara tabloyu yazar (`UserRole`).
 */
function RoleMembersDialog({ role, onClose }: { role: RoleDto; onClose: () => void }) {
  const members = useRoleMembers(role.id);
  const users = useUsers();
  const addMutation = useAddRoleMember();
  const removeMutation = useRemoveRoleMember();
  const [candidateId, setCandidateId] = useState<string>('');

  const memberIds = new Set((members.data ?? []).map(member => member.userId));
  // Pasif kullanıcı giriş yapamaz; ona rol eklemek anlamsız. Zaten üye olanlar da listede yok.
  const candidates = (users.data ?? []).filter(user => user.isActive && !memberIds.has(user.id));
  const isBusy = addMutation.isPending || removeMutation.isPending;

  const showError = (error: unknown) => toast.error(toApiError(error).message);

  return (
    <Dialog open onOpenChange={open => !open && onClose()}>
      <DialogContent className='flex max-h-[90vh] flex-col gap-0 overflow-hidden p-0'>
        <DialogHeader className='p-4 pb-3'>
          <DialogTitle>Üyeler — {role.name}</DialogTitle>
          <DialogDescription>{RELOGIN_NOTICE}</DialogDescription>
        </DialogHeader>

        <div className='flex flex-col gap-3 border-b p-4 pt-0'>
          {role.isActive ? (
            <div className='flex gap-2'>
              <Select value={candidateId || null} onValueChange={value => setCandidateId(value ?? '')} disabled={candidates.length === 0 || isBusy}>
                <SelectTrigger className='min-w-0 flex-1' aria-label='Eklenecek kullanıcı'>
                  {/* Çocuksuz `SelectValue` ham Guid'i basar; ad açıkça verilir. */}
                  <SelectValue placeholder={candidates.length === 0 ? 'Eklenecek aktif kullanıcı yok' : 'Kullanıcı seçin'}>
                    {candidateId ? (candidates.find(user => user.id === candidateId)?.fullName ?? 'Kullanıcı bulunamadı') : undefined}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {candidates.map(user => (
                    <SelectItem key={user.id} value={user.id}>
                      {user.fullName}
                      {user.userName && <span className='text-muted-foreground'> · {user.userName}</span>}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button
                type='button'
                disabled={!candidateId || isBusy}
                onClick={() =>
                  addMutation.mutate({ roleId: role.id, userId: candidateId }, { onSuccess: () => setCandidateId(''), onError: showError })
                }>
                <UserPlusIcon />
                Ekle
              </Button>
            </div>
          ) : (
            <p className='text-sm text-muted-foreground'>Pasif role yeni üye eklenemez; mevcut üyeler çıkarılabilir.</p>
          )}
        </div>

        {/* Scroll İÇ katmanda ve başlığın ALTINDA: yuvarlatılmış kutu scrollbar'ı köşeden kırpar, X düğmesi scrollbar'la çakışmaz. */}
        <div className='min-h-0 scrollbar-thin overflow-y-auto'>
          {members.isPending ? (
            <div className='flex flex-col gap-2 p-4'>
              {Array.from({ length: 3 }, (_, i) => (
                <Skeleton key={i} className='h-11 w-full rounded-lg' />
              ))}
            </div>
          ) : members.isError ? (
            <p className='p-4 text-sm text-destructive'>{members.error.message}</p>
          ) : members.data.length === 0 ? (
            <p className='p-4 text-sm text-muted-foreground'>Bu rolde kullanıcı yok.</p>
          ) : (
            <ul className='flex flex-col gap-2 p-4'>
              {members.data.map(member => (
                <li key={member.userId} className='flex items-center gap-3 rounded-lg border p-3'>
                  <div className='flex min-w-0 flex-1 flex-col'>
                    <span className='truncate text-sm font-medium'>{member.fullName}</span>
                    <span className='truncate text-xs text-muted-foreground'>{member.userName ?? '—'}</span>
                  </div>
                  {!member.isActive && <Badge variant='secondary'>Pasif</Badge>}
                  <Button
                    size='sm'
                    variant='outline'
                    disabled={isBusy}
                    onClick={() => removeMutation.mutate({ roleId: role.id, userId: member.userId }, { onError: showError })}>
                    <UserMinusIcon />
                    Çıkar
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <DialogFooter className='mx-0 mb-0'>
          <Button type='button' variant='outline' onClick={onClose}>
            Kapat
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
