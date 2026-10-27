import type { PermissionDto } from '@/models/permission';
import type { RoleDto } from '@/models/role';
import type { RolePermissionMatrixItem } from '@/models/rolePermission';

/**
 * İzin kataloğunun ekran yardımcıları — Roller dialogu ve kullanıcı Roller dialogu aynı gruplamayı ve etiketleri kullanır.
 */

/** Seed'deki `Permission.Category` değerleri (İngilizce anahtar) → ekranda görünen ad. Bilinmeyen kategori olduğu gibi görünür. */
const CATEGORY_LABELS: Record<string, string> = {
  Diagram: 'Diyagram',
  Alarm: 'Alarm',
  Admin: 'Yönetim',
  Data: 'Veri',
  Access: 'Geçiş',
  RemoteDesk: 'Uzak Masaüstü',
  Signalization: 'Sinyalizasyon'
};

export function permissionCategoryLabel(category: string): string {
  return CATEGORY_LABELS[category] ?? category;
}

/** Katalogdaki sırayı koruyarak kategoriye göre gruplar (`Object.groupBy` hedef lib'e bağlı, kullanılmadı). */
export function groupByCategory(permissions: PermissionDto[]): [string, PermissionDto[]][] {
  const groups = new Map<string, PermissionDto[]>();
  for (const permission of permissions) {
    const group = groups.get(permission.category);
    if (group) group.push(permission);
    else groups.set(permission.category, [permission]);
  }
  return [...groups.entries()];
}

/**
 * Seçili rol ADLARININ verdiği izin kimlikleri. Sunucudaki kuralın aynası (`AuthService.GetPermissionCodesAsync`): PASİF rol
 * izin türetmez. Ad karşılaştırması büyük/küçük harfe duyarsızdır (Identity `NormalizedName`).
 */
export function effectivePermissionIds(roleNames: string[], roles: RoleDto[], matrix: RolePermissionMatrixItem[]): Set<number> {
  const selected = new Set(roleNames.map(name => name.toUpperCase()));
  const activeRoleIds = new Set(roles.filter(role => role.isActive && selected.has(role.name.toUpperCase())).map(role => role.id));

  const result = new Set<number>();
  for (const item of matrix) {
    if (!activeRoleIds.has(item.roleId)) continue;
    for (const id of item.permissionIds) result.add(id);
  }
  return result;
}
