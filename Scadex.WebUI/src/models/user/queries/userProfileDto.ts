/**
 * Ayna: Scadex.Model/Dtos/User/Queries/UserProfileDto.cs — `GET /api/Account/profile`.
 *
 * Kullanıcı adı, firma ve kart salt okunurdur: onları yönetici yazar.
 */
export interface UserProfileDto {
  id: string;
  userName: string | null;
  email: string | null;
  fullName: string;
  phoneNumber: string | null;
  companyName: string;
  identityCardId: string | null;
}
