import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { GuidesApiService } from './guides-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('GuidesApiService', () => {
  let service: GuidesApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [GuidesApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(GuidesApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('lists guides with bearer header and no activeOnly param by default', () => {
    service.listGuides().subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/guides');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect(req.request.params.has('activeOnly')).toBeFalse();
    req.flush([]);
  });

  it('lists guides with activeOnly param when provided', () => {
    service.listGuides(true).subscribe();

    const req = httpMock.expectOne((request) => request.url === 'http://localhost:5101/api/guides');
    expect(req.request.params.get('activeOnly')).toBe('true');
    req.flush([]);
  });

  it('creates a guide', () => {
    const request = { title: 'Order did not arrive', steps: ['Check tracking.', 'Contact carrier.'] };
    service.createGuide(request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/guides');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({
      id: 'g-1',
      title: request.title,
      isActive: true,
      steps: request.steps.map((instruction, index) => ({ stepNumber: index + 1, instruction })),
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [1]
    });
  });

  it('updates a guide', () => {
    const request = {
      title: 'Order did not arrive',
      steps: ['Check tracking.'],
      isActive: false,
      rowVersion: [1]
    };
    service.updateGuide('g-1', request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/guides/g-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush({
      id: 'g-1',
      title: request.title,
      isActive: request.isActive,
      steps: request.steps.map((instruction, index) => ({ stepNumber: index + 1, instruction })),
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: request.rowVersion
    });
  });

  it('maps validation errors to a user-friendly message', () => {
    let actualError = '';

    service.createGuide({ title: '', steps: [] }).subscribe({
      error: (error: Error) => {
        actualError = error.message;
      }
    });

    const req = httpMock.expectOne('http://localhost:5101/api/guides');
    req.flush(
      { title: 'One or more validation errors occurred.', errors: { steps: ['At least 1 step is required.'] } },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(actualError).toContain('steps');
  });
});
