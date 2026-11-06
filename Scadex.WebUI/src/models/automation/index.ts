// Queries — sunucu ciktisi, saf interface
export type { AutomationDetailDto } from './queries/automationDetailDto';
export type { AutomationStatusBreakdownDto, AutomationStatusContributorDto } from './queries/automationStatusBreakdownDto';
export type { ScadaSyncResultDto } from './queries/scadaSyncResultDto';

// Commands — C# DTO aynalari, saf interface
export type { AutomationCreateRequest } from './commands/automationCreateRequest';
export type { AutomationUpdateRequest } from './commands/automationUpdateRequest';

// Form — ekleme ve duzenlemenin ortak sekli + sunucu sekline projeksiyonlar
export {
  automationFormSchema,
  emptyAutomationForm,
  toAutomationForm,
  toCreateRequest,
  toUpdateRequest,
  type AutomationFormValues
} from './commands/automationForm';
