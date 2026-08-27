import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { RegisterPageComponent } from './register-page.component';

class AuthServiceStub {
  register = jasmine.createSpy().and.returnValue(
    of({
      accessToken: 'token',
      accessTokenExpiresAtUtc: '2026-08-26T10:00:00Z',
      refreshToken: 'refresh',
      refreshTokenExpiresAtUtc: '2026-09-26T10:00:00Z',
      userId: 'u-1',
      email: 'agent@crm.local',
      roles: ['agent'],
      permissions: ['customers.read']
    })
  );

  resolveLandingRoute = jasmine.createSpy().and.returnValue('/agent/dashboard');
}

describe('RegisterPageComponent', () => {
  let fixture: ComponentFixture<RegisterPageComponent>;
  let component: RegisterPageComponent;
  let authStub: AuthServiceStub;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterPageComponent],
      providers: [provideRouter([]), { provide: AuthService, useClass: AuthServiceStub }]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterPageComponent);
    component = fixture.componentInstance;
    authStub = TestBed.inject(AuthService) as unknown as AuthServiceStub;
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
  });

  it('completes role selection then submits register payload', () => {
    component.selectRole('customer');
    component.form.patchValue({
      displayName: 'Aya Hassan',
      email: 'aya@crm.local',
      password: 'Agent!23456',
      confirmPassword: 'Agent!23456',
      fullName: 'Aya Hassan',
      company: 'Contoso',
      primaryContactChannel: 1,
      primaryContactValue: 'aya@crm.local',
      primaryContactLabel: 'work'
    });

    component.submit();

    expect(authStub.register).toHaveBeenCalledWith(
      jasmine.objectContaining({
        accountType: 'customer',
        email: 'aya@crm.local',
        fullName: 'Aya Hassan',
        company: 'Contoso'
      })
    );
    expect(router.navigateByUrl).toHaveBeenCalledWith('/agent/dashboard');
  });
});
