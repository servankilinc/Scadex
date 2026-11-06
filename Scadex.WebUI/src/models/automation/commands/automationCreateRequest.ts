/** Ayna: Scadex.Model/Dtos/Automation/Commands/AutomationCreateDto.cs */
export interface AutomationCreateRequest {
  name: string;
  companyId: string;
  /** Konum seçilmediyse null — 0 DEĞİL (0,0 Gine Körfezi'nde gerçek bir nokta). */
  latitude: number | null;
  longitude: number | null;
  locationDescription: string | null;
}
