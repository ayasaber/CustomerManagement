import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminUsersPageComponent } from './admin-users-page.component';

class AdminApiServiceStub {
  listUsers = jasmine.createSpy().and.returnValue(
    of({
      page: 1,
      pageSize: 50,
      totalCount: 1,
      items: [
        {
          id: 'u-1',
          email: 'admin@crm.local',
          displayName: 'Admin User',
          isActive: true,
          createdAtUtc: '2026-08-26T00:00:00Z',
          updatedAtUtc: '2026-08-26T00:00:00Z',
          rowVersion: 'AAAA',
          roles: ['admin']
        }
      ]
    })
  );

  createUser = jasmine.createSpy().and.returnValue(
    of({
      id: 'u-2',
      email: 'agent@crm.local',
      displayName: 'Agent',
      isActive: true,
      createdAtUtc: '2026-08-26T00:00:00Z',
      updatedAtUtc: '2026-08-26T00:00:00Z',
      rowVersion: 'BBBB',
      roles: ['agent']
    })
  );

  updateUser = jasmine.createSpy().and.returnValue(of({}));
  assignUserRoles = jasmine.createSpy().and.returnValue(of({}));
  deactivateUser = jasmine.createSpy().and.returnValue(of(void 0));
}

class AuthServiceStub {
  currentUser = jasmine.createSpy().and.returnValue(() => ({
    userId: 'not-u-1',
    email: 'operator@crm.local',
    roles: ['admin'],
    permissions: ['admin.users.manage'],
    accessTokenExpiresAtUtc: '2026-09-14T00:00:00Z',
    refreshTokenExpiresAtUtc: '2026-09-14T00:00:00Z'
  }));
}

describe('AdminUsersPageComponent', () => {
  let fixture: ComponentFixture<AdminUsersPageComponent>;
  let component: AdminUsersPageComponent;
  let apiStub: AdminApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminUsersPageComponent],
      providers: [
        { provide: AdminApiService, useClass: AdminApiServiceStub },
        { provide: AuthService, useClass: AuthServiceStub }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminUsersPageComponent);
    component = fixture.componentInstance;
    apiStub = TestBed.inject(AdminApiService) as unknown as AdminApiServiceStub;
    fixture.detectChanges();
  });

  it('renders users table from API', () => {
    expect(apiStub.listUsers).toHaveBeenCalled();
    expect(component.users().length).toBe(1);
  });

  it('creates user from modal form', () => {
    component.openCreateModal();
    component.createForm.patchValue({
      email: 'new@crm.local',
      displayName: 'New User',
      password: 'Agent!23456',
      roles: 'agent'
    });

    component.submitCreate();

    expect(apiStub.createUser).toHaveBeenCalled();
  });

  it('deactivates user when confirmed', () => {
    spyOn(window, 'confirm').and.returnValue(true);

    component.deactivate(component.users()[0]);

    expect(apiStub.deactivateUser).toHaveBeenCalledWith('u-1');
  });

  it('blocks self-deactivation', () => {
    const selfUser = {
      ...component.users()[0],
      id: 'self-user-id',
      email: 'self@crm.local'
    };

    const authStub = TestBed.inject(AuthService) as unknown as AuthServiceStub;
    authStub.currentUser.and.returnValue(() => ({
      userId: 'self-user-id',
      email: 'self@crm.local',
      roles: ['admin'],
      permissions: ['admin.users.manage'],
      accessTokenExpiresAtUtc: '2026-09-14T00:00:00Z',
      refreshTokenExpiresAtUtc: '2026-09-14T00:00:00Z'
    }));

    component.deactivate(selfUser);

    expect(apiStub.deactivateUser).not.toHaveBeenCalledWith('self-user-id');
    expect(component.error()).toContain('cannot deactivate their own account');
  });
});
