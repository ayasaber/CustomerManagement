import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { AdminSystemSettingsPageComponent } from './admin-system-settings-page.component';

class AdminApiServiceStub {
  listSystemSettings = jasmine.createSpy().and.returnValue(
    of([
      {
        id: 's-1',
        key: 'Auth.AccessTokenMinutes',
        value: '15',
        description: 'Access token lifetime',
        updatedAtUtc: '2026-08-26T00:00:00Z',
        rowVersion: 'AAAA'
      }
    ])
  );

  updateSystemSetting = jasmine
    .createSpy()
    .and.returnValue(throwError(() => new Error('Concurrency conflict')));
}

describe('AdminSystemSettingsPageComponent', () => {
  let fixture: ComponentFixture<AdminSystemSettingsPageComponent>;
  let component: AdminSystemSettingsPageComponent;
  let apiStub: AdminApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminSystemSettingsPageComponent],
      providers: [{ provide: AdminApiService, useClass: AdminApiServiceStub }]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminSystemSettingsPageComponent);
    component = fixture.componentInstance;
    apiStub = TestBed.inject(AdminApiService) as unknown as AdminApiServiceStub;
    fixture.detectChanges();
  });

  it('shows conflict feedback and reloads latest values', () => {
    const setting = {
      id: 's-1',
      key: 'Auth.AccessTokenMinutes',
      value: '15',
      description: 'Access token lifetime',
      updatedAtUtc: '2026-08-26T00:00:00Z',
      rowVersion: 'AAAA'
    };

    component.settings.set([setting]);
    component.settingsForm.addControl(setting.key, new FormControl(setting.value, { nonNullable: true }));

    component.saveSetting(setting);

    expect(component.conflictMessage()).toContain('Another admin updated');
    expect(apiStub.listSystemSettings).toHaveBeenCalledTimes(2);
  });
});
