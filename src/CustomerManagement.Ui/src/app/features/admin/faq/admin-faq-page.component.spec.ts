import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FaqApiService } from '../../../core/services/faq-api.service';
import { AdminFaqPageComponent } from './admin-faq-page.component';

class FaqApiServiceStub {
  listFaqEntries = jasmine.createSpy().and.returnValue(
    of([
      {
        id: 'f-1',
        topic: 'Billing',
        question: 'How do I update my payment method?',
        answer: 'Go to Billing > Payment Methods.',
        sortOrder: 10,
        isActive: true,
        createdAtUtc: '2026-09-01T00:00:00Z',
        updatedAtUtc: '2026-09-01T00:00:00Z',
        rowVersion: [1]
      }
    ])
  );

  createFaqEntry = jasmine.createSpy().and.returnValue(
    of({
      id: 'f-2',
      topic: 'Shipping',
      question: 'When will it arrive?',
      answer: 'Within 3-5 business days.',
      sortOrder: 20,
      isActive: true,
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [1]
    })
  );

  updateFaqEntry = jasmine.createSpy().and.returnValue(
    of({
      id: 'f-1',
      topic: 'Billing',
      question: 'How do I update my payment method?',
      answer: 'Go to Billing > Payment Methods.',
      sortOrder: 10,
      isActive: false,
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [2]
    })
  );
}

describe('AdminFaqPageComponent', () => {
  let fixture: ComponentFixture<AdminFaqPageComponent>;
  let component: AdminFaqPageComponent;
  let apiStub: FaqApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminFaqPageComponent],
      providers: [{ provide: FaqApiService, useClass: FaqApiServiceStub }]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminFaqPageComponent);
    component = fixture.componentInstance;
    apiStub = TestBed.inject(FaqApiService) as unknown as FaqApiServiceStub;
    fixture.detectChanges();
  });

  it('renders FAQ entries from the API', () => {
    expect(apiStub.listFaqEntries).toHaveBeenCalled();
    expect(component.entries().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Billing');
  });

  it('creates a new FAQ entry from the form', () => {
    component.entryForm.setValue({
      topic: 'Shipping',
      question: 'When will it arrive?',
      answer: 'Within 3-5 business days.',
      sortOrder: 20
    });

    component.createEntry();

    expect(apiStub.createFaqEntry).toHaveBeenCalledWith({
      topic: 'Shipping',
      question: 'When will it arrive?',
      answer: 'Within 3-5 business days.',
      sortOrder: 20
    });
  });

  it('retires an active entry via the toggle action', () => {
    const entry = component.entries()[0];

    component.toggleActive(entry);

    expect(apiStub.updateFaqEntry).toHaveBeenCalledWith(entry.id, {
      topic: entry.topic,
      question: entry.question,
      answer: entry.answer,
      sortOrder: entry.sortOrder,
      isActive: false,
      rowVersion: entry.rowVersion
    });
  });
});
