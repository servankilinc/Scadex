import type { AccessToken } from '@/models/auth/queries/accessToken';

export interface RefreshAuthResponse {
  roles: string[] | null;
  accessToken: AccessToken;
}
