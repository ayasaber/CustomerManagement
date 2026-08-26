import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, Signal, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, tap, throwError } from 'rxjs';
import {
  AuthUser,
  LoginRequest,
  LogoutRequest,
  RefreshTokenRequest,
  RegisterRequest,
  TokenResponse
} from '../models/auth.models';

interface PersistedAuthState {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authBaseUrl = 'http://localhost:5101/api/auth';
  private readonly storageKey = 'crm-auth-state';

  private readonly currentUserSignal = signal<AuthUser | null>(null);
  private readonly accessTokenSignal = signal<string | null>(null);
  private readonly refreshTokenSignal = signal<string | null>(null);
  private refreshInFlight$: Observable<TokenResponse> | null = null;

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router
  ) {
    this.restoreSession();
  }

  register(request: RegisterRequest): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>(`${this.authBaseUrl}/register`, request)
      .pipe(
        tap((response) => this.setSession(response)),
        catchError((error) => this.mapError(error))
      );
  }

  login(request: LoginRequest): Observable<TokenResponse> {
    return this.http
      .post<TokenResponse>(`${this.authBaseUrl}/login`, request)
      .pipe(
        tap((response) => this.setSession(response)),
        catchError((error) => this.mapError(error))
      );
  }

  refresh(): Observable<TokenResponse> {
    if (this.refreshInFlight$) {
      return this.refreshInFlight$;
    }

    const refreshToken = this.refreshTokenSignal();
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token available.'));
    }

    const request: RefreshTokenRequest = { refreshToken };
    const refresh$ = this.http
      .post<TokenResponse>(`${this.authBaseUrl}/refresh`, request)
      .pipe(
        tap((response) => this.setSession(response)),
        catchError((error) => {
          this.clearSession();
          return this.mapError(error);
        }),
        finalize(() => {
          this.refreshInFlight$ = null;
        }),
        shareReplay(1)
      );

    this.refreshInFlight$ = refresh$;
    return refresh$;
  }

  logout(): Observable<void> {
    const refreshToken = this.refreshTokenSignal();
    if (!refreshToken) {
      this.clearSession();
      return of(void 0);
    }

    const request: LogoutRequest = { refreshToken };
    return this.http
      .post(`${this.authBaseUrl}/logout`, request, { headers: this.authHeaders() })
      .pipe(
        map(() => void 0),
        catchError(() => of(void 0)),
        tap(() => this.clearSession())
      );
  }

  accessToken(): string | null {
    return this.accessTokenSignal();
  }

  currentUser(): Signal<AuthUser | null> {
    return this.currentUserSignal.asReadonly();
  }

  isAuthenticated(): boolean {
    return !!this.accessTokenSignal() && !!this.currentUserSignal();
  }

  hasRole(role: string): boolean {
    const user = this.currentUserSignal();
    if (!user) {
      return false;
    }

    return user.roles.some((candidate) => candidate.toLowerCase() === role.toLowerCase());
  }

  hasPermission(permission: string): boolean {
    const user = this.currentUserSignal();
    if (!user) {
      return false;
    }

    return user.permissions.some((candidate) => candidate.toLowerCase() === permission.toLowerCase());
  }

  resolveLandingRoute(user: Pick<AuthUser, 'roles' | 'permissions'>): string {
    const roles = user.roles.map((role) => role.toLowerCase());
    const permissions = user.permissions.map((permission) => permission.toLowerCase());

    if (roles.includes('admin') || permissions.includes('admin.console.access') || permissions.includes('admin.users.manage')) {
      return '/admin/users';
    }

    if (roles.includes('agent')) {
      return '/agent/dashboard';
    }

    if (roles.includes('customer')) {
      return '/customer/portal';
    }

    return '/forbidden';
  }

  navigateToLanding(): Promise<boolean> {
    const user = this.currentUserSignal();
    if (!user) {
      return this.router.navigate(['/auth/login']);
    }

    return this.router.navigateByUrl(this.resolveLandingRoute(user));
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory().pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status !== 401 || !this.refreshTokenSignal()) {
          return this.mapError(error);
        }

        return this.refresh().pipe(
          switchMap(() => requestFactory()),
          catchError((refreshError) => {
            this.clearSession();
            void this.router.navigate(['/auth/login']);
            return this.mapError(refreshError as HttpErrorResponse);
          })
        );
      })
    );
  }

  private setSession(response: TokenResponse): void {
    const user: AuthUser = {
      userId: response.userId,
      email: response.email,
      roles: response.roles,
      permissions: response.permissions,
      accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
      refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc
    };

    this.currentUserSignal.set(user);
    this.accessTokenSignal.set(response.accessToken);
    this.refreshTokenSignal.set(response.refreshToken);

    const persisted: PersistedAuthState = {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      user
    };

    localStorage.setItem(this.storageKey, JSON.stringify(persisted));
  }

  private clearSession(): void {
    this.currentUserSignal.set(null);
    this.accessTokenSignal.set(null);
    this.refreshTokenSignal.set(null);
    localStorage.removeItem(this.storageKey);
  }

  private restoreSession(): void {
    const raw = localStorage.getItem(this.storageKey);
    if (!raw) {
      return;
    }

    try {
      const parsed = JSON.parse(raw) as PersistedAuthState;
      if (!parsed.accessToken || !parsed.refreshToken || !parsed.user) {
        this.clearSession();
        return;
      }

      this.accessTokenSignal.set(parsed.accessToken);
      this.refreshTokenSignal.set(parsed.refreshToken);
      this.currentUserSignal.set(parsed.user);
    } catch {
      this.clearSession();
    }
  }

  private authHeaders(): HttpHeaders {
    const accessToken = this.accessTokenSignal();
    return accessToken ? new HttpHeaders({ Authorization: `Bearer ${accessToken}` }) : new HttpHeaders();
  }

  private mapError(error: HttpErrorResponse): Observable<never> {
    const payload = error.error as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    const validationMessage = payload?.errors
      ? Object.entries(payload.errors)
          .map(([key, values]) => `${key}: ${values.join(', ')}`)
          .join(' | ')
      : null;

    const message = validationMessage || payload?.message || payload?.title || error.message || 'Authentication request failed.';
    return throwError(() => new Error(message));
  }
}
