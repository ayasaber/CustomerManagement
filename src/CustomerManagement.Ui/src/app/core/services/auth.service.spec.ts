import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerSpy: jasmine.SpyObj<Router>;

  beforeEach(() => {
    localStorage.clear();
    routerSpy = jasmine.createSpyObj<Router>('Router', ['navigate', 'navigateByUrl']);
    routerSpy.navigate.and.resolveTo(true);
    routerSpy.navigateByUrl.and.resolveTo(true);

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService, { provide: Router, useValue: routerSpy }]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('stores session after login', () => {
    service.login({ email: 'agent@crm.local', password: 'Agent!23456' }).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush({
      accessToken: 'access-1',
      accessTokenExpiresAtUtc: '2026-08-26T10:00:00Z',
      refreshToken: 'refresh-1',
      refreshTokenExpiresAtUtc: '2026-09-10T10:00:00Z',
      userId: 'u-1',
      email: 'agent@crm.local',
      roles: ['agent'],
      permissions: ['customers.read']
    });

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.currentUser()()?.email).toBe('agent@crm.local');
    expect(service.accessToken()).toBe('access-1');
  });

  it('refreshes token using stored refresh token', () => {
    service.login({ email: 'agent@crm.local', password: 'Agent!23456' }).subscribe();
    httpMock.expectOne('http://localhost:5101/api/auth/login').flush({
      accessToken: 'access-1',
      accessTokenExpiresAtUtc: '2026-08-26T10:00:00Z',
      refreshToken: 'refresh-1',
      refreshTokenExpiresAtUtc: '2026-09-10T10:00:00Z',
      userId: 'u-1',
      email: 'agent@crm.local',
      roles: ['agent'],
      permissions: ['customers.read']
    });

    service.refresh().subscribe();

    const refresh = httpMock.expectOne('http://localhost:5101/api/auth/refresh');
    expect(refresh.request.body.refreshToken).toBe('refresh-1');
    refresh.flush({
      accessToken: 'access-2',
      accessTokenExpiresAtUtc: '2026-08-26T11:00:00Z',
      refreshToken: 'refresh-2',
      refreshTokenExpiresAtUtc: '2026-09-11T11:00:00Z',
      userId: 'u-1',
      email: 'agent@crm.local',
      roles: ['agent'],
      permissions: ['customers.read']
    });

    expect(service.accessToken()).toBe('access-2');
  });

  it('maps landing route priority as admin > agent > customer', () => {
    expect(service.resolveLandingRoute({ roles: ['admin', 'agent'], permissions: [] })).toBe('/admin/users');
    expect(service.resolveLandingRoute({ roles: ['agent'], permissions: [] })).toBe('/agent/dashboard');
    expect(service.resolveLandingRoute({ roles: ['customer'], permissions: [] })).toBe('/customer/portal');
  });
});
