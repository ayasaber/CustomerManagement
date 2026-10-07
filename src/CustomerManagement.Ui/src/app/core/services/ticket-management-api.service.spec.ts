import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { TicketManagementApiService } from './ticket-management-api.service';

class AuthServiceStub {
  accessToken(): string {
    return 'test-access-token';
  }

  withAutoRefresh<T>(requestFactory: () => Observable<T>): Observable<T> {
    return requestFactory();
  }
}

describe('TicketManagementApiService', () => {
  let service: TicketManagementApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [TicketManagementApiService, { provide: AuthService, useClass: AuthServiceStub }]
    });

    service = TestBed.inject(TicketManagementApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('creates a ticket without sending priorityId when omitted', () => {
    const request = { categoryId: 'cat-1', subject: 'Need help', description: 'Something is broken.' };
    service.createTicket(request).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/tickets');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    expect(req.request.body.priorityId).toBeUndefined();
    expect(req.request.body.categoryId).toBe('cat-1');
    req.flush({
      id: 't-1',
      customerId: 'c-1',
      assignedToUserId: null,
      assignedToUserEmail: null,
      categoryId: 'cat-1',
      categoryName: 'General',
      categoryIsActive: true,
      priorityId: 'p-1',
      priorityName: 'Normal',
      priorityIsActive: true,
      prioritySortOrder: 20,
      subject: request.subject,
      description: request.description,
      status: 'new',
      isEscalated: false,
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [1]
    });
  });

  it('lists ticket attachments with bearer header', () => {
    service.listTicketAttachments('t-1').subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/tickets/t-1/attachments');
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-access-token');
    req.flush([
      {
        id: 'a-1',
        ticketId: 't-1',
        originalFileName: 'evidence.txt',
        contentType: 'text/plain',
        sizeBytes: 12,
        uploadedByUserId: 'u-1',
        uploadedByDisplayName: 'Aya',
        createdAtUtc: '2026-09-01T00:00:00Z'
      }
    ]);
  });

  it('uploads a ticket attachment as multipart form data', () => {
    const file = new File(['hello'], 'evidence.txt', { type: 'text/plain' });
    service.uploadTicketAttachment('t-1', file).subscribe();

    const req = httpMock.expectOne('http://localhost:5101/api/tickets/t-1/attachments');
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBeTrue();
    const uploadedFile = (req.request.body as FormData).get('file') as File;
    expect(uploadedFile.name).toBe('evidence.txt');
    req.flush({
      id: 'a-1',
      ticketId: 't-1',
      originalFileName: 'evidence.txt',
      contentType: 'text/plain',
      sizeBytes: 5,
      uploadedByUserId: 'u-1',
      uploadedByDisplayName: 'Aya',
      createdAtUtc: '2026-09-01T00:00:00Z'
    });
  });

  it('downloads a ticket attachment as a blob', () => {
    service.downloadTicketAttachment('t-1', 'a-1').subscribe((blob) => {
      expect(blob instanceof Blob).toBeTrue();
    });

    const req = httpMock.expectOne('http://localhost:5101/api/tickets/t-1/attachments/a-1/content');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['file-bytes']));
  });
});
