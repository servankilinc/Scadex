// Queries — sunucu çıktısı, saf interface
export type { RoleDto } from './queries/roleDto';

// Commands — zod şemaları sunucudaki FluentValidation kurallarının aynası
export { roleCreateRequestSchema, type RoleCreateRequest } from './commands/roleCreateRequest';
export { roleUpdateRequestSchema, type RoleUpdateRequest } from './commands/roleUpdateRequest';
