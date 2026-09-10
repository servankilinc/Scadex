/** Ayna: Scadex.Model/Dtos/Cabinet/Commands/CabinetCreateDto.cs */
export interface CabinetCreateRequest {
  name: string;
  companyId: string;
  /** Konum seçilmediyse null — 0 DEĞİL (0,0 Gine Körfezi'nde gerçek bir nokta). */
  latitude: number | null;
  longitude: number | null;
  locationDescription: string | null;
  gsmIp: string | null;
  networkIp: string | null;
  scadaBaseUrl: string | null;
  scadaCommandTimeoutMs: number;
  scadaIsEnabled: boolean;
}
