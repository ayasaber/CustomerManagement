import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { FeedbackApiService } from './feedback-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('FeedbackApiService', () => {
  let service: FeedbackApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [FeedbackApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(FeedbackApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('submits feedback with bearer header', () => {
    const request = { rating: 5, comment: 'Great support!' };
    service.submitFeedback(request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/feedback');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect(req.request.body).toEqual(request);
    req.flush({
      id: 'fb-1',
      customerId: 'c-1',
      customerName: 'Aya',
      rating: request.rating,
      comment: request.comment,
      createdAtUtc: '2026-09-01T00:00:00Z'
    });
  });

  it('submits feedback without a comment', () => {
    const request = { rating: 4 };
    service.submitFeedback(request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/feedback');
    expect(req.request.body).toEqual(request);
    req.flush({
      id: 'fb-2',
      customerId: 'c-1',
      customerName: 'Aya',
      rating: 4,
      comment: null,
      createdAtUtc: '2026-09-01T00:00:00Z'
    });
  });

  it('maps validation errors to a user-friendly message', () => {
    let actualError = '';

    service.submitFeedback({ rating: 0 }).subscribe({
      error: (error: Error) => {
        actualError = error.message;
      }
    });

    const req = httpMock.expectOne('http://localhost:5101/api/feedback');
    req.flush(
      { title: 'One or more validation errors occurred.', errors: { rating: ['Rating must be between 1 and 5.'] } },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(actualError).toContain('rating');
  });

  it('maps forbidden responses to a permission message', () => {
    let actualError = '';

    service.submitFeedback({ rating: 5 }).subscribe({
      error: (error: Error) => {
        actualError = error.message;
      }
    });

    const req = httpMock.expectOne('http://localhost:5101/api/feedback');
    req.flush({}, { status: 403, statusText: 'Forbidden' });

    expect(actualError).toContain('permission');
  });
});
