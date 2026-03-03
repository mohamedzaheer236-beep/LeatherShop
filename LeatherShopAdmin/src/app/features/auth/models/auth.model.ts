/** Data payload returned by the backend login/refresh endpoints. */
export interface LoginData {
  token: string;
  username: string;
  expiresAt: string;
}
