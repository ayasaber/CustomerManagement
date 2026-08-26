import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { adminOnlyGuard } from './admin-only.guard';
import { authenticatedGuard } from './authenticated.guard';
import { AuthService } from '../services/auth.service';

class AuthServiceStub {
  authenticated = false;
  roles: string[] = [];
  permissions: string[] = [];

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
});
