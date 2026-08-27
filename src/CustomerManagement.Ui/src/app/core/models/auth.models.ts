export interface RegisterContactDetailRequest {
  channel: number;
  value: string;
  label?: string | null;
  isPrimary: boolean;
}

export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
  displayName: string;
  accountType: 'agent' | 'customer';
  fullName?: string;
  company?: string;
  contactDetails?: RegisterContactDetailRequest[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface LogoutRequest {
  refreshToken: string;
}

export interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  userId: string;
  email: string;
  roles: string[];
  permissions: string[];
}

export interface AuthUser {
  userId: string;
  email: string;
  roles: string[];
  permissions: string[];
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
}
