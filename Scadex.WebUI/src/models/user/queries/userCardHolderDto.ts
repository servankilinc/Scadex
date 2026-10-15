/**
 * Ayna: Scadex.Model/Dtos/User/Queries/UserCardHolderDto.cs — kart tanımlama penceresinin listesi (`ManageAccessCards`).
 * Yalnızca AKTİF kullanıcılar; e-posta/telefon gibi alanlar bilerek yok (tam liste `ManageUsers` ister).
 */
export interface UserCardHolderDto {
  id: string;
  userName: string | null;
  fullName: string;
  /** Kart tanımlı değilse `null`. */
  identityCardId: string | null;
}
