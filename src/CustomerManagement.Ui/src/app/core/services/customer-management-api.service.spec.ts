import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { CustomerManagementApiService } from './customer-management-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('CustomerManagementApiService', () => {
  let service: CustomerManagementApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [CustomerManagementApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(CustomerManagementApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should call gateway profile endpoint with bearer header', () => {
    service.getProfile('abc').subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/customers/abc');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    req.flush({
      id: 'abc',
      name: 'Aya',
      company: 'Contoso',
      createdAtUtc: '2026-08-25T00:00:00Z',
      updatedAtUtc: '2026-08-25T00:00:00Z',
      rowVersion: 'AAAAAAAAB9E=',
      contactDetails: []
    });
  });

  it('should map validation errors to user-friendly message', () => {
    let actualError = '';

    service.createProfile({ name: '', company: null, contactDetails: null }).subscribe({
      error: (error: Error) => {
        actualError = error.message;
      }
    });

    const req = httpMock.expectOne('http://localhost:5101/api/customers');
    req.flush(
      {
        title: 'One or more validation errors occurred.',
        errors: {
          name: ['The name field is required.']
        }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(actualError).toContain('name');
  });
});
