// Queries — sunucu ciktisi, saf interface
export type { CabinetDetailDto } from './queries/cabinetDetailDto';
export type { CabinetStatusBreakdownDto, CabinetStatusContributorDto } from './queries/cabinetStatusBreakdownDto';
export type { ScadaSyncResultDto } from './queries/scadaSyncResultDto';

// Commands — C# DTO aynalari, saf interface
export type { CabinetCreateRequest } from './commands/cabinetCreateRequest';
export type { CabinetUpdateRequest } from './commands/cabinetUpdateRequest';

// Form — ekleme ve duzenlemenin ortak sekli + sunucu sekline projeksiyonlar
export {
  SCADA_MIN_TIMEOUT_MS,
  cabinetFormSchema,
  emptyCabinetForm,
  toCabinetForm,
  toCreateRequest,
  toUpdateRequest,
  type CabinetFormValues
} from './commands/cabinetForm';
