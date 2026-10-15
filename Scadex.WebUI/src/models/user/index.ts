// Queries — sunucu çıktısı, saf interface
export type { UserBaseDto } from './queries/userBaseDto';
export type { UserDetailDto } from './queries/userDetailDto';

// Commands — zod şemaları sunucudaki FluentValidation kurallarının aynası
export { userCreateRequestSchema, type UserCreateRequest } from './commands/userCreateRequest';
export { userUpdateRequestSchema, type UserUpdateRequest } from './commands/userUpdateRequest';
export type { UserIdentityCardAssignRequest } from './commands/userIdentityCardAssignRequest';
export { userPasswordChangeSchema, type UserPasswordChangeForm, type UserPasswordChangeRequest } from './commands/userPasswordChangeRequest';
export type { UserCardHolderDto } from './queries/userCardHolderDto';
