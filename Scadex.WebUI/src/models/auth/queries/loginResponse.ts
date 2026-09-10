import type { AccessToken } from '@/models/auth/queries/accessToken';
import type { UserBaseDto } from '@/models/user/queries/userBaseDto';

export interface LoginResponse {
  roles: string[] | null;
  permissions: string[];
  accessToken: AccessToken;
  deviceId: string;
  user: UserBaseDto;
}
