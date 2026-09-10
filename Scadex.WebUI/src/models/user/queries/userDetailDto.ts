/** Ayna: Scadex.Model/Dtos/User/Queries/UserDetailDto.cs */
export interface UserDetailDto {
  id: string;
  /** Giriş adı — ad soyad tekil değildir, kullanıcıyı ayırt eden bu. */
  userName: string | null;
  email: string | null;
  companyId: string;
  companyName: string;
  fullName: string;
  phoneNumber: string | null;
  createdBy: string | null;
  updatedBy: string | null;
  /** `Z` soneki YOK (`datetime2`) — gösterilecekse `toUtcDate` ile. */
  createDateUtc: string | null;
  updateDateUtc: string | null;
  isActive: boolean;
}
