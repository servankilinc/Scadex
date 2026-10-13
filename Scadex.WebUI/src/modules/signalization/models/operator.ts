/**
 * Ayna: Scadex.Signalization/Dtos/Operator/SignalOperatorDtos.cs
 *
 * Operatör = AKTİF kullanıcı; ayrı bir operatör tablosu yoktur. Kurum, kullanıcının sahip olduğu kurum
 * rolünden türetilir (saklanmaz), kart numarası `User.IdentityCardId`'dir.
 */
export interface SignalOperatorDto {
  userId: string;
  fullName: string;
  userName: string | null;
  identityCardId: string | null;
  /** Tek kurumu varsa dolu; hiç yoksa ya da birden fazlaysa `null`. */
  authorityId: string | null;
  /** Birden fazla kurum varsa virgülle birleştirilmiş adlar. */
  authorityName: string | null;
  /**
   * Kullanıcıya /admin/users'tan elle birden fazla kurum rolü atanmış. Bu durumda kartla kapı AÇILMAZ
   * (`MultipleAuthorities`); operatör ekranından tek kurum seçilerek düzeltilir.
   */
  hasMultipleAuthorities: boolean;
}

/** `null` = kurum rolünü kaldır. Kurum dışı roller korunur. */
export interface SignalOperatorAuthorityRequest {
  authorityId: string | null;
}
