import { Fragment, useState } from 'react';
import { RotateCcwIcon, SaveIcon } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { toApiError } from '@/lib/axios-helper';
import { groupByCategory, permissionCategoryLabel } from '@/lib/permissions';
import { cn } from '@/lib/utils';
import type { PermissionDto } from '@/models/permission';
import type { RoleDto } from '@/models/role';
import type { RolePermissionMatrixItem } from '@/models/rolePermission';
import { toast } from 'sonner';
import { RELOGIN_NOTICE, usePermissionMatrix, usePermissions, useRoles, useSavePermissionMatrix } from '@/hooks/use-roles';

/**
 * Yetki Matrisi — `/admin/permissions` (`ManageUsers`).
 *
 * Satırlar kategoriye göre gruplu izinler, sütunlar roller. Hücreler taslakta değişir; "Kaydet" yalnızca DEĞİŞEN rolleri
 * tek istekte gönderir (`PUT /api/RolePermission/matrix`) ve sunucu hepsini tek transaction'da eşitler:
 * matris ya tamamen yazılır ya hiç.
 *
 * Pasif roller soluk gösterilir: atanmış kalırlar ama izin vermezler. İzinleri yine düzenlenebilir.
 */
export default function PermissionMatrix() {
  const roles = useRoles();
  const permissions = usePermissions();
  const matrix = usePermissionMatrix();
  const loadError = roles.error ?? permissions.error ?? matrix.error;

  return (
    <div className='flex flex-col gap-4 p-4'>
      <div>
        <h1 className='text-lg font-semibold'>Yetki Matrisi</h1>
        <p className='text-sm text-muted-foreground'>Hangi rolün hangi izne sahip olduğu. {RELOGIN_NOTICE}</p>
      </div>

      {loadError ? (
        <p className='text-sm text-destructive'>{loadError.message}</p>
      ) : roles.data && permissions.data && matrix.data ? (
        // Kayıttan sonra matris yeniden çekilince editör TAZE taslakla yeniden kurulur (`key`); `useEffect` ile senkron yok.
        <MatrixEditor key={matrix.dataUpdatedAt} roles={roles.data} permissions={permissions.data} matrix={matrix.data} />
      ) : (
        <Skeleton className='h-96 w-full rounded-lg' />
      )}
    </div>
  );
}

type Draft = Record<string, number[]>;

function toDraft(matrix: RolePermissionMatrixItem[]): Draft {
  return Object.fromEntries(matrix.map(item => [item.roleId, [...item.permissionIds].sort((a, b) => a - b)]));
}

function sameSet(a: number[], b: number[]): boolean {
  if (a.length !== b.length) return false;
  const set = new Set(a);
  return b.every(id => set.has(id));
}

function MatrixEditor({ roles, permissions, matrix }: { roles: RoleDto[]; permissions: PermissionDto[]; matrix: RolePermissionMatrixItem[] }) {
  const original = toDraft(matrix);
  const [draft, setDraft] = useState<Draft>(() => toDraft(matrix));
  const mutation = useSavePermissionMatrix();

  // Aktif roller önce, sonra ada göre — pasif roller sağda soluk durur.
  const columns = [...roles].sort((a, b) => Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name, 'tr'));
  const groups = groupByCategory(permissions);

  const changedRoleIds = columns.map(role => role.id).filter(roleId => !sameSet(draft[roleId] ?? [], original[roleId] ?? []));

  const isGranted = (roleId: string, permissionId: number) => (draft[roleId] ?? []).includes(permissionId);
  const wasGranted = (roleId: string, permissionId: number) => (original[roleId] ?? []).includes(permissionId);

  const toggle = (roleId: string, permissionId: number, next: boolean) =>
    setDraft(current => {
      const ids = current[roleId] ?? [];
      return { ...current, [roleId]: next ? [...ids, permissionId] : ids.filter(id => id !== permissionId) };
    });

  const save = () =>
    mutation.mutate(
      changedRoleIds.map(roleId => ({ roleId, permissionIds: draft[roleId] ?? [] })),
      { onError: error => toast.error(toApiError(error).message) }
    );

  return (
    <div className='flex flex-col gap-3'>
      <div className='rounded-lg border'>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className='sticky left-0 z-10 min-w-64 bg-background'>İzin</TableHead>
              {columns.map(role => (
                <TableHead key={role.id} className={cn('min-w-28 text-center align-bottom', !role.isActive && 'opacity-60')}>
                  <div className='flex flex-col items-center gap-1 py-1'>
                    <span className='font-medium'>{role.name}</span>
                    <div className='flex gap-1'>
                      {role.isImmutable && <Badge variant='outline'>Sistem</Badge>}
                      {!role.isActive && <Badge variant='secondary'>Pasif</Badge>}
                    </div>
                    <span className='text-xs font-normal text-muted-foreground tabular-nums'>
                      {(draft[role.id] ?? []).length} izin · {role.userCount} kullanıcı
                    </span>
                  </div>
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {groups.map(([category, items]) => (
              <Fragment key={category}>
                <TableRow className='bg-muted/40 hover:bg-muted/40'>
                  <TableCell
                    colSpan={columns.length + 1}
                    className='sticky left-0 py-1.5 text-xs font-medium tracking-wide text-muted-foreground uppercase'>
                    {permissionCategoryLabel(category)}
                  </TableCell>
                </TableRow>
                {items.map(permission => (
                  <TableRow key={permission.id}>
                    <TableCell className='sticky left-0 z-10 bg-background'>
                      <div className='flex flex-col'>
                        <span>{permission.displayName}</span>
                        <span className='font-mono text-xs text-muted-foreground'>{permission.code}</span>
                      </div>
                    </TableCell>
                    {columns.map(role => {
                      const checked = isGranted(role.id, permission.id);
                      const isChanged = checked !== wasGranted(role.id, permission.id);
                      return (
                        <TableCell key={role.id} className={cn('text-center', isChanged && 'bg-amber-500/15', !role.isActive && 'opacity-60')}>
                          <Checkbox
                            aria-label={`${role.name}: ${permission.displayName}`}
                            checked={checked}
                            disabled={mutation.isPending}
                            onCheckedChange={next => toggle(role.id, permission.id, next === true)}
                          />
                        </TableCell>
                      );
                    })}
                  </TableRow>
                ))}
              </Fragment>
            ))}
          </TableBody>
        </Table>
      </div>

      <div className='flex flex-wrap items-center justify-end gap-3'>
        <span className='text-sm text-muted-foreground'>
          {changedRoleIds.length === 0 ? 'Kaydedilmemiş değişiklik yok.' : `${changedRoleIds.length} rolde kaydedilmemiş değişiklik var.`}
        </span>
        <Button variant='outline' disabled={changedRoleIds.length === 0 || mutation.isPending} onClick={() => setDraft(toDraft(matrix))}>
          <RotateCcwIcon />
          Geri al
        </Button>
        <Button disabled={changedRoleIds.length === 0 || mutation.isPending} onClick={save}>
          <SaveIcon />
          {mutation.isPending ? 'Kaydediliyor…' : 'Kaydet'}
        </Button>
      </div>
    </div>
  );
}
