/** Ayna: Scadex.Model/Dtos/Company/Queries/CompanyDto.cs */
export interface CompanyDto {
  id: string;
  name: string;
  description: string | null;
  createdBy: string | null;
  updatedBy: string | null;
  createDateUtc: string | null;
  updateDateUtc: string | null;
  isActive: boolean;
}
