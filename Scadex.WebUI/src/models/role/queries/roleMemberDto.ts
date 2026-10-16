/** Ayna: Scadex.Model/Dtos/UserRole/Queries/RoleMemberDto.cs — `GET /api/UserRole/role/{roleId}/users`. Pasif kullanıcılar dahil. */
export interface RoleMemberDto {
  userId: string;
  userName: string | null;
  fullName: string;
  isActive: boolean;
}
