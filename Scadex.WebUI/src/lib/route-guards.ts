import { redirect } from 'react-router';
import { hasPermission } from '@/lib/auth-session';
import type { PermissionCode } from '@/models/enums/entityEnums';

export type { PermissionCode };

/**
 * Rota `loader`'ı: izni olmayan kullanıcı adresi elle yazsa da ana sayfaya döner (menü maddesi zaten gizli). Asıl denetim
 * sunucudadır (403); bu yalnızca boş/hatalı bir ekranı göstermemek içindir. İzinler girişte token'la gelir, reaktif değildir.
 */
export const requirePermission = (permission: PermissionCode) => () => (hasPermission(permission) ? null : redirect('/'));
