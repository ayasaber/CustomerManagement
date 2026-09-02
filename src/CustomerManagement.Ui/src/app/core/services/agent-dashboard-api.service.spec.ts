import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { AgentDashboardApiService } from './agent-dashboard-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('AgentDashboardApiService', () => {
  let service: AgentDashboardApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AgentDashboardApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(AgentDashboardApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('calls assigned tickets endpoint with bearer token', () => {
    service.getAssignedTickets(1, 20).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/dashboard/me/assigned-tickets?page=1&pageSize=20');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    req.flush({ totalCount: 0, items: [] });
  });

  it('maps 409 errors to conflict message', () => {
    let actualError = '';

    service.createTicketNote({
      ticketId: 't-1',
      body: 'note',
      mentionedUserIds: [],
      offerReassign: false,
      reassignToUserId: null
    }).subscribe({
      error: (err: Error) => {
        actualError = err.message;
      }
    });

    const req = httpMock.expectOne('http://localhost:5101/api/ticket-notes');
    req.flush({ title: 'conflict' }, { status: 409, statusText: 'Conflict' });

    expect(actualError).toContain('Conflict detected');
  });
});
