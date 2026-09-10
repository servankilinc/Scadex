import http from '@/lib/axios-helper';
import type { PermissionDto } from '@/models/permission';

/** İzin kataloğu — seed'den gelir, salt okunur (`IImmutableEntity`). */
export async function getPermissionList(): Promise<PermissionDto[]> {
  return http.post<PermissionDto[]>('/api/Permission/list', {});
}
