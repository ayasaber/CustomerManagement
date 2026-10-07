import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { KnowledgeBaseSearchApiService } from './knowledge-base-search-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('KnowledgeBaseSearchApiService', () => {
  let service: KnowledgeBaseSearchApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [KnowledgeBaseSearchApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(KnowledgeBaseSearchApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('sends the q query param and bearer header', () => {
    service.search('billing').subscribe();

    const req = httpMock.expectOne((request) => request.url === 'http://localhost:5101/api/knowledge-base/search');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('q')).toBe('billing');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    req.flush({ query: 'billing', results: [] });
  });

  it('returns results from the response', () => {
    let actualResults: unknown;
    service.search('billing').subscribe((response) => {
      actualResults = response.results;
    });

    const req = httpMock.expectOne((request) => request.url === 'http://localhost:5101/api/knowledge-base/search');
    req.flush({
      query: 'billing',
      results: [{ contentType: 'Faq', id: 'f-1', title: 'Billing question', snippet: 'Answer text.' }]
    });

    expect(actualResults).toEqual([{ contentType: 'Faq', id: 'f-1', title: 'Billing question', snippet: 'Answer text.' }]);
  });

  it('maps a forbidden response to a user-friendly message', () => {
    let actualError = '';

    service.search('billing').subscribe({
      error: (error: Error) => {
        actualError = error.message;
      }
    });

    const req = httpMock.expectOne((request) => request.url === 'http://localhost:5101/api/knowledge-base/search');
    req.flush({}, { status: 403, statusText: 'Forbidden' });

    expect(actualError).toContain('permission');
  });
});
