import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AgentContextService } from '../../core/services/agent-context.service';
import { CustomerManagementApiService } from '../../core/services/customer-management-api.service';
import { CustomerManagementPageComponent } from './customer-management-page.component';

class CustomerManagementApiServiceStub {
  listCustomers = jasmine.createSpy().and.returnValue(of({ page: 1, pageSize: 100, totalCount: 0, items: [] }));

  getProfile = jasmine.createSpy().and.returnValue(
    of({
      id: 'cust-1',
      name: 'Aya',
      company: 'Contoso',
      createdAtUtc: '2026-08-25T00:00:00Z',
      updatedAtUtc: '2026-08-25T00:00:00Z',
      rowVersion: 'AAAAAAAAB9E=',
      contactDetails: []
    })
  );

  getContactDetails = jasmine.createSpy().and.returnValue(of({ customerId: 'cust-1', contactDetails: [] }));
  getInteractionHistory = jasmine.createSpy().and.returnValue(of({ page: 1, pageSize: 20, totalCount: 0, items: [] }));
  listNotes = jasmine.createSpy().and.returnValue(of([]));
  listAttachments = jasmine.createSpy().and.returnValue(of([]));
  createProfile = jasmine.createSpy().and.returnValue(of({
    id: 'cust-1',
    name: 'Aya',
    company: 'Contoso',
    createdAtUtc: '2026-08-25T00:00:00Z',
    updatedAtUtc: '2026-08-25T00:00:00Z',
    rowVersion: 'AAAAAAAAB9E=',
    contactDetails: []
  }));
  updateProfile = jasmine.createSpy().and.returnValue(of({
    id: 'cust-1',
    name: 'Aya',
    company: 'Contoso',
    createdAtUtc: '2026-08-25T00:00:00Z',
    updatedAtUtc: '2026-08-25T00:00:00Z',
    rowVersion: 'AAAAAAAAB9E=',
    contactDetails: []
  }));
  addNote = jasmine.createSpy().and.returnValue(of({}));
  uploadAttachment = jasmine.createSpy().and.returnValue(of({}));
  getAttachmentDownloadUrl = jasmine.createSpy().and.returnValue('#');
}

describe('CustomerManagementPageComponent', () => {
  let fixture: ComponentFixture<CustomerManagementPageComponent>;
  let component: CustomerManagementPageComponent;
  let apiStub: CustomerManagementApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerManagementPageComponent],
      providers: [
        AgentContextService,
        { provide: CustomerManagementApiService, useClass: CustomerManagementApiServiceStub }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerManagementPageComponent);
    component = fixture.componentInstance;
    apiStub = TestBed.inject(CustomerManagementApiService) as unknown as CustomerManagementApiServiceStub;
  });

  it('loads customer workspace data from gateway', () => {
    component.lookupForm.patchValue({ customerId: 'cust-1' });
    component.loadCustomer();

    expect(apiStub.getProfile).toHaveBeenCalledWith('cust-1');
    expect(component.profile()?.id).toBe('cust-1');
  });

  it('shows API error messages when load fails', () => {
    apiStub.getProfile.and.returnValue(throwError(() => new Error('gateway unavailable')));

    component.lookupForm.patchValue({ customerId: 'cust-1' });
    component.loadCustomer();

    expect(component.error()).toContain('gateway unavailable');
  });
});
