// Queries — sunucu çıktısı, saf interface
export type { CompanyDto } from './queries/companyDto';

// Commands — zod şemaları sunucudaki FluentValidation kurallarının aynası
export { companyCreateRequestSchema, type CompanyCreateRequest } from './commands/companyCreateRequest';
export { companyUpdateRequestSchema, type CompanyUpdateRequest } from './commands/companyUpdateRequest';
