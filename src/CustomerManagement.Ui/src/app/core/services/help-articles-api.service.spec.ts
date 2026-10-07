import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { HelpArticlesApiService } from './help-articles-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('HelpArticlesApiService', () => {
  let service: HelpArticlesApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [HelpArticlesApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(HelpArticlesApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists help articles with bearer header and no activeOnly param by default', () => {
    service.listHelpArticles().subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/help-articles');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect(req.request.params.has('activeOnly')).toBeFalse();
    req.flush([]);
  });

  it('lists help articles with activeOnly param when provided', () => {
    service.listHelpArticles(true).subscribe();

    const req = httpMock.expectOne((request) => request.url === 'http://localhost:5101/api/help-articles');
    expect(req.request.params.get('activeOnly')).toBe('true');
    req.flush([]);
  });

  it('creates a help article', () => {
    const request = { title: 'Getting started', body: 'Go to Settings to configure your profile.' };
    service.createHelpArticle(request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/help-articles');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({
      id: 'a-1',
      title: request.title,
      body: request.body,
      isActive: true,
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [1]
    });
  });

  it('updates a help article', () => {
    const request = {
      title: 'Getting started',
      body: 'Go to Settings to configure your profile.',
      isActive: false,
      rowVersion: [1]
    };
    service.updateHelpArticle('a-1', request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/help-articles/a-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({ id: 'a-1', ...request, createdAtUtc: '2026-09-01T00:00:00Z', updatedAtUtc: '2026-09-01T00:00:00Z' });
  });

  it('maps validation errors to a user-friendly message', () => {
    let actualError = '';

    service.createHelpArticle({ title: '', body: 'Body' }).subscribe({
      error: (error: Error) => {
        actualError = error.message;
      }
    });

    const req = httpMock.expectOne('http://localhost:5101/api/help-articles');
    req.flush(
      { title: 'One or more validation errors occurred.', errors: { title: ['Title is required.'] } },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(actualError).toContain('title');
  });
});
