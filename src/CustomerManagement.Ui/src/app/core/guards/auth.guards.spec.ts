import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { agentOnlyGuard } from './agent-only.guard';
import { adminOnlyGuard } from './admin-only.guard';
import { authenticatedGuard } from './authenticated.guard';
import { AuthService } from '../services/auth.service';

class AuthServiceStub {
  authenticated = false;
  roles: string[] = [];
  permissions: string[] = [];

  currentUser() {
    return () =>
      this.authenticated
        ? {
            userId: 'u-1',
            email: 'user@crm.local',
            roles: this.roles,
            permissions: this.permissions,
            accessTokenExpiresAtUtc: '2026-09-01T00:00:00Z',
            refreshTokenExpiresAtUtc: '2026-09-02T00:00:00Z'
          }
        : null;
  }

  isAuthenticated(): boolean {
    return this.authenticated;
  }

  hasRole(role: string): boolean {
    return this.roles.some((candidate) => candidate.toLowerCase() === role.toLowerCase());
  }

  hasPermission(permission: string): boolean {
    return this.permissions.some((candidate) => candidate.toLowerCase() === permission.toLowerCase());
  }
}

describe('Auth Guards', () => {
  let authStub: AuthServiceStub;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useClass: AuthServiceStub }]
    });

    authStub = TestBed.inject(AuthService) as unknown as AuthServiceStub;
    router = TestBed.inject(Router);
  });

  it('authenticated guard redirects to login for anonymous users', () => {
    authStub.authenticated = false;

    const result = TestBed.runInInjectionContext(() =>
      authenticatedGuard({} as never, { url: '/admin/users' } as never)
    );

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toContain('/auth/login');
  });

  it('admin guard allows admin users', () => {
    authStub.authenticated = true;
    authStub.roles = ['admin'];

    const result = TestBed.runInInjectionContext(() =>
      adminOnlyGuard({} as never, { url: '/admin/users' } as never)
    );

    expect(result).toBeTrue();
  });

  it('admin guard redirects non-admin users to forbidden', () => {
    authStub.authenticated = true;
    authStub.roles = ['agent'];

    const result = TestBed.runInInjectionContext(() =>
      adminOnlyGuard({} as never, { url: '/admin/users' } as never)
    );

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
  });

  it('agent guard allows agent users', () => {
    authStub.authenticated = true;
    authStub.roles = ['agent'];

    const result = TestBed.runInInjectionContext(() =>
      agentOnlyGuard({} as never, { url: '/agent/dashboard' } as never)
    );

    expect(result).toBeTrue();
  });

  it('agent guard redirects customer users to forbidden', () => {
    authStub.authenticated = true;
    authStub.roles = ['customer'];
    authStub.permissions = [];

    const result = TestBed.runInInjectionContext(() =>
      agentOnlyGuard({} as never, { url: '/agent/dashboard' } as never)
    );

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
  });
});
