import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { AdminAuditLogPageComponent } from './admin-audit-log-page.component';

class AdminApiServiceStub {
  queryAuditLogs = jasmine.createSpy().and.returnValue(
    of({
      page: 1,
      pageSize: 50,
      totalCount: 0,
      items: []
    })
  );
}

describe('AdminAuditLogPageComponent', () => {
  let fixture: ComponentFixture<AdminAuditLogPageComponent>;
  let component: AdminAuditLogPageComponent;
  let apiStub: AdminApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminAuditLogPageComponent],
      providers: [{ provide: AdminApiService, useClass: AdminApiServiceStub }]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminAuditLogPageComponent);
    component = fixture.componentInstance;
    apiStub = TestBed.inject(AdminApiService) as unknown as AdminApiServiceStub;
  });

  it('blocks submit and shows inline validation when date range is invalid', () => {
    component.filterForm.patchValue({
      fromUtc: '2026-08-26T22:00',
      toUtc: '2026-08-25T22:00'
    });

    component.query();

    expect(component.validation()).toContain('From UTC');
    expect(apiStub.queryAuditLogs).not.toHaveBeenCalled();
  });

  it('queries API when form is valid', () => {
    component.filterForm.patchValue({
      fromUtc: '2026-08-25T22:00',
      toUtc: '2026-08-26T22:00'
    });

    component.query();

    expect(apiStub.queryAuditLogs).toHaveBeenCalled();
  });
});
