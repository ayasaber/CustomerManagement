import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { FaqApiService } from '../../core/services/faq-api.service';
import { FeedbackApiService } from '../../core/services/feedback-api.service';
import { TicketManagementApiService } from '../../core/services/ticket-management-api.service';
import { TicketConversationPanelComponent } from '../tickets/components/ticket-conversation-panel.component';
import { CustomerPortalPageComponent } from './customer-portal-page.component';

const ticketListItem = {
  id: 't-1',
  customerId: 'c-1',
  assignedToUserId: null,
  assignedToUserEmail: null,
  categoryId: 'cat-1',
  categoryName: 'General',
  priorityId: 'p-1',
  priorityName: 'Normal',
  subject: 'Cannot sign in',
  status: 'new',
  isEscalated: false,
  createdAtUtc: '2026-09-01T00:00:00Z',
  updatedAtUtc: '2026-09-01T00:00:00Z',
  rowVersion: [1]
};

class TicketManagementApiServiceStub {
  listTickets = jasmine.createSpy().and.returnValue(of({ page: 1, pageSize: 50, totalCount: 1, items: [ticketListItem] }));
  listCategories = jasmine.createSpy().and.returnValue(
    of([{ id: 'cat-1', name: 'General', description: null, isActive: true, createdAtUtc: '', updatedAtUtc: '', rowVersion: [1] }])
  );
  listTicketMessages = jasmine.createSpy().and.returnValue(of({ page: 1, pageSize: 50, totalCount: 0, items: [] }));
  listTicketAttachments = jasmine.createSpy().and.returnValue(of([]));
  createTicketMessage = jasmine.createSpy();
  createTicket = jasmine.createSpy();
  uploadTicketAttachment = jasmine.createSpy();
}

class FaqApiServiceStub {
  listFaqEntries = jasmine.createSpy().and.returnValue(of([]));
}

class FeedbackApiServiceStub {
  submitFeedback = jasmine.createSpy();
}

describe('CustomerPortalPageComponent', () => {
  let fixture: ComponentFixture<CustomerPortalPageComponent>;
  let component: CustomerPortalPageComponent;
  let ticketApiStub: TicketManagementApiServiceStub;
  let faqApiStub: FaqApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerPortalPageComponent],
      providers: [
        provideRouter([]),
        { provide: TicketManagementApiService, useClass: TicketManagementApiServiceStub },
        { provide: FaqApiService, useClass: FaqApiServiceStub },
        { provide: FeedbackApiService, useClass: FeedbackApiServiceStub }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerPortalPageComponent);
    component = fixture.componentInstance;
    ticketApiStub = TestBed.inject(TicketManagementApiService) as unknown as TicketManagementApiServiceStub;
    faqApiStub = TestBed.inject(FaqApiService) as unknown as FaqApiServiceStub;
    fixture.detectChanges();
  });

  it('renders My Requests by default with the mocked ticket list', () => {
    expect(component.section()).toBe('requests');
    expect(ticketApiStub.listTickets).toHaveBeenCalled();

    const rows = fixture.nativeElement.querySelectorAll('.request-list li');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Cannot sign in');
  });

  it('new request form requires category, subject, and description, and has no priority control', () => {
    component.selectSection('new-request');
    fixture.detectChanges();

    expect(component.createForm.contains('priorityId')).toBeFalse();
    expect(component.createForm.invalid).toBeTrue();

    const prioritySelect = fixture.nativeElement.querySelector('select[formControlName="priorityId"]');
    const priorityInput = fixture.nativeElement.querySelector('input[formControlName="priorityId"]');
    expect(prioritySelect).toBeFalsy();
    expect(priorityInput).toBeFalsy();

    component.createForm.setValue({ categoryId: 'cat-1', subject: 'Need help', description: 'Something is broken.' });
    expect(component.createForm.valid).toBeTrue();
  });

  it('renders the ticket conversation panel with compose enabled when a ticket is selected', () => {
    component.selectTicket('t-1');
    fixture.detectChanges();

    const panel = fixture.debugElement.query(By.directive(TicketConversationPanelComponent));
    expect(panel).toBeTruthy();
    expect(panel.componentInstance.canCompose).toBeTrue();
  });

  it('FAQ section shows an empty state when the mocked service returns no entries', () => {
    component.selectSection('faq');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.faq-topic')).toBeFalsy();
    expect(fixture.nativeElement.textContent).toContain('No FAQ entries are available yet.');
  });

  it('FAQ section groups entries by topic', () => {
    faqApiStub.listFaqEntries.and.returnValue(
      of([
        {
          id: 'f-1',
          topic: 'Billing',
          question: 'How do I pay?',
          answer: 'Use the billing portal.',
          sortOrder: 1,
          isActive: true,
          createdAtUtc: '',
          updatedAtUtc: '',
          rowVersion: [1]
        },
        {
          id: 'f-2',
          topic: 'Billing',
          question: 'Can I get a refund?',
          answer: 'Contact support.',
          sortOrder: 2,
          isActive: true,
          createdAtUtc: '',
          updatedAtUtc: '',
          rowVersion: [1]
        }
      ])
    );

    component.selectSection('faq');
    fixture.detectChanges();

    const topics = fixture.nativeElement.querySelectorAll('.faq-topic h3');
    expect(topics.length).toBe(1);
    expect(topics[0].textContent).toContain('Billing');
  });

  it('feedback section requires a rating before allowing submit', () => {
    component.selectSection('feedback');
    component.feedbackForm.patchValue({ rating: null as unknown as number });
    fixture.detectChanges();

    expect(component.feedbackForm.invalid).toBeTrue();
    const submitButton = fixture.nativeElement.querySelector('section.panel form button[type="submit"]') as HTMLButtonElement;
    expect(submitButton.disabled).toBeTrue();

    component.feedbackForm.patchValue({ rating: 4 });
    fixture.detectChanges();

    expect(component.feedbackForm.valid).toBeTrue();
    expect(submitButton.disabled).toBeFalse();
  });
});
