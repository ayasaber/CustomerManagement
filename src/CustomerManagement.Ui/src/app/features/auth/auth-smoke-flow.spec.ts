import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { LoginPageComponent } from './login/login-page.component';
import { RegisterPageComponent } from './register/register-page.component';

class AuthServiceSmokeStub {
  register = jasmine.createSpy().and.callFake((request: { accountType: string }) =>
    of({
      accessToken: 'token',
      accessTokenExpiresAtUtc: '2026-08-26T10:00:00Z',
      refreshToken: 'refresh',
      refreshTokenExpiresAtUtc: '2026-09-26T10:00:00Z',
      userId: 'u-1',
      email: 'user@crm.local',
      roles: [request.accountType],
      permissions: []
    })
  );

  login = jasmine.createSpy().and.returnValue(
    of({
      accessToken: 'token-admin',
      accessTokenExpiresAtUtc: '2026-08-26T10:00:00Z',
      refreshToken: 'refresh-admin',
      refreshTokenExpiresAtUtc: '2026-09-26T10:00:00Z',
      userId: 'admin-1',
      email: 'admin@crm.local',
      roles: ['admin'],
      permissions: ['admin.users.manage']
    })
  );

  resolveLandingRoute(payload: { roles: string[] }): string {
    if (payload.roles.includes('admin')) {
      return '/admin/users';
    }

    if (payload.roles.includes('agent')) {
      return '/agent/dashboard';
    }

    return '/customer/portal';
  }
}

describe('Auth Smoke Flows', () => {
  let authStub: AuthServiceSmokeStub;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useClass: AuthServiceSmokeStub }]
    }).compileComponents();

    authStub = TestBed.inject(AuthService) as unknown as AuthServiceSmokeStub;
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
  });

  it('registers agent and redirects to /agent/dashboard', () => {
    const fixture: ComponentFixture<RegisterPageComponent> = TestBed.createComponent(RegisterPageComponent);
    const component = fixture.componentInstance;

    component.selectRole('agent');
    component.form.patchValue({
      displayName: 'Agent User',
      email: 'agent@crm.local',
      password: 'Agent!23456',
      confirmPassword: 'Agent!23456'
    });

    component.submit();

    expect(authStub.register).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/agent/dashboard');
  });

  it('registers customer and redirects to /customer/portal', () => {
    const fixture: ComponentFixture<RegisterPageComponent> = TestBed.createComponent(RegisterPageComponent);
    const component = fixture.componentInstance;

    component.selectRole('customer');
    component.form.patchValue({
      displayName: 'Customer User',
      email: 'customer@crm.local',
      password: 'Customer!23456',
      confirmPassword: 'Customer!23456'
    });

    component.submit();

    expect(authStub.register).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/customer/portal');
  });

  it('logs in admin and redirects to /admin/users', () => {
    const fixture: ComponentFixture<LoginPageComponent> = TestBed.createComponent(LoginPageComponent);
    const component = fixture.componentInstance;

    component.form.patchValue({
      email: 'admin@crm.local',
      password: 'Admin!23456'
    });

    component.submit();

    expect(authStub.login).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin/users');
  });
});
