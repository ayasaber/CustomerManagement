import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { FaqApiService } from './faq-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('FaqApiService', () => {
  let service: FaqApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FaqApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(FaqApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists FAQ entries with bearer header and no activeOnly param by default', () => {
    service.listFaqEntries().subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/faq');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect(req.request.params.has('activeOnly')).toBeFalse();
    req.flush([]);
  });

  it('lists FAQ entries with activeOnly param when provided', () => {
    service.listFaqEntries(true).subscribe();

    const req = httpMock.expectOne((request) => request.url === 'http://localhost:5101/api/faq');
    expect(req.request.params.get('activeOnly')).toBe('true');
    req.flush([]);
  });

  it('creates a FAQ entry', () => {
    const request = { topic: 'Billing', question: 'How do I pay?', answer: 'Use the billing portal.', sortOrder: 10 };
    service.createFaqEntry(request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/faq');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({
      id: 'f-1',
      topic: request.topic,
      question: request.question,
      answer: request.answer,
      sortOrder: request.sortOrder,
      isActive: true,
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [1]
    });
  });

  it('updates a FAQ entry', () => {
    const request = {
      topic: 'Billing',
      question: 'How do I pay?',
      answer: 'Use the billing portal.',
      sortOrder: 10,
      isActive: false,
      rowVersion: [1]
    };
    service.updateFaqEntry('f-1', request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/faq/f-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({ id: 'f-1', ...request, createdAtUtc: '2026-09-01T00:00:00Z', updatedAtUtc: '2026-09-01T00:00:00Z' });
  });

  it('maps validation errors to a user-friendly message', () => {
    let actualError = '';

    service.createFaqEntry({ topic: '', question: 'Q', answer: 'A', sortOrder: 0 }).subscribe({
      error: (error: Error) => {
        actualError = error.message;
      }
    });

    const req = httpMock.expectOne('http://localhost:5101/api/faq');
    req.flush(
      { title: 'One or more validation errors occurred.', errors: { topic: ['Topic is required.'] } },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(actualError).toContain('topic');
  });
});
