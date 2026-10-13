/**
 * Ayna: Scadex.Model/Dtos/User/Commands/UserIdentityCardAssignDto.cs — sahada okutulan kartı kullanıcıya atar
 * (Kabinler → "Kart tanımlama"). Form değil, okunan karttan doldurulur; bu yüzden zod şeması yok.
 *
 * Kart başka bir aktif kullanıcıdaysa 400 (`errors.identityCardId`). Kullanıcının FARKLI bir kartı varsa `replaceExisting: false`
 * ile 400 (`errors.replaceExisting`) — ekran önce onay sorar, sonra `true` gönderir.
 */
export interface UserIdentityCardAssignRequest {
  identityCardId: string;
  replaceExisting: boolean;
}
