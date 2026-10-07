import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { customerOnlyGuard } from './customer-only.guard';
import { AuthService } from '../services/auth.service';

class AuthServiceStub {
  authenticated = false;
  roles: string[] = [];

  currentUser() {
    return () =>
      this.authenticated
        ? {
            userId: 'u-1',
            email: 'customer@crm.local',
            roles: this.roles,
            permissions: [],
            accessTokenExpiresAtUtc: '2026-09-01T00:00:00Z',
            refreshTokenExpiresAtUtc: '2026-09-02T00:00:00Z'
          }
        : null;
  }
}

describe('customerOnlyGuard', () => {
  let authStub: AuthServiceStub;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useClass: AuthServiceStub }]
    });

    authStub = TestBed.inject(AuthService) as unknown as AuthServiceStub;
    router = TestBed.inject(Router);
  });

  it('redirects anonymous users to login', () => {
    authStub.authenticated = false;

    const result = TestBed.runInInjectionContext(() => customerOnlyGuard({} as never, { url: '/customer/portal' } as never));

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toContain('/auth/login');
  });

  it('allows customer users', () => {
    authStub.authenticated = true;
    authStub.roles = ['customer'];

    const result = TestBed.runInInjectionContext(() => customerOnlyGuard({} as never, { url: '/customer/portal' } as never));

    expect(result).toBeTrue();
  });

  it('redirects non-customer users to forbidden', () => {
    authStub.authenticated = true;
    authStub.roles = ['agent'];

    const result = TestBed.runInInjectionContext(() => customerOnlyGuard({} as never, { url: '/customer/portal' } as never));

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
  });
});
