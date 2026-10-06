import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import {
  AssignTicketRequest,
  ApiValidationError,
  CreateTicketMessageRequest,
  CreateTicketMessageResponse,
  CreateTicketCategoryRequest,
  CreateTicketPriorityRequest,
  CreateTicketRequest,
  SelfAssignTicketRequest,
  TicketAttachmentResponse,
  TicketCategoryResponse,
  TicketHistoryResponse,
  TicketListResponse,
  TicketMessageListResponse,
  TicketPriorityResponse,
  TicketResponse,
  UpdateTicketCategoryRequest,
  UpdateTicketPriorityRequest,
  UpdateTicketStatusRequest
} from '../models/ticket-management.models';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class TicketManagementApiService {
  private readonly baseUrl = 'http://localhost:5101/api/tickets';
  private readonly ticketMessagesUrl = 'http://localhost:5101/api/ticket-messages';

  constructor(
    private readonly http: HttpClient,
    private readonly authService: AuthService
  ) {}

  listTickets(page = 1, pageSize = 25): Observable<TicketListResponse> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<TicketListResponse>(this.baseUrl, { headers: this.buildHeaders(), params })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  createTicket(request: CreateTicketRequest): Observable<TicketResponse> {
    return this.authService
      .withAutoRefresh(() => this.http.post<TicketResponse>(this.baseUrl, request, { headers: this.buildHeaders() }))
      .pipe(catchError((error) => this.mapError(error)));
  }

  getTicket(ticketId: string): Observable<TicketResponse> {
    return this.authService
      .withAutoRefresh(() => this.http.get<TicketResponse>(`${this.baseUrl}/${ticketId}`, { headers: this.buildHeaders() }))
      .pipe(catchError((error) => this.mapError(error)));
  }

  updateStatus(ticketId: string, request: UpdateTicketStatusRequest): Observable<TicketResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<TicketResponse>(`${this.baseUrl}/${ticketId}/status`, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  assignTicket(ticketId: string, request: AssignTicketRequest): Observable<TicketResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<TicketResponse>(`${this.baseUrl}/${ticketId}/assign`, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  assignSelf(ticketId: string, request: SelfAssignTicketRequest): Observable<TicketResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<TicketResponse>(`${this.baseUrl}/${ticketId}/self-assign`, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  listCategories(activeOnly = false): Observable<TicketCategoryResponse[]> {
    const params = activeOnly ? new HttpParams().set('activeOnly', true) : undefined;
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<TicketCategoryResponse[]>(`${this.baseUrl}/categories`, { headers: this.buildHeaders(), params })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  createCategory(request: CreateTicketCategoryRequest): Observable<TicketCategoryResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.post<TicketCategoryResponse>(`${this.baseUrl}/categories`, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  updateCategory(categoryId: string, request: UpdateTicketCategoryRequest): Observable<TicketCategoryResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<TicketCategoryResponse>(`${this.baseUrl}/categories/${categoryId}`, request, {
          headers: this.buildHeaders()
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  listPriorities(activeOnly = false): Observable<TicketPriorityResponse[]> {
    const params = activeOnly ? new HttpParams().set('activeOnly', true) : undefined;
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<TicketPriorityResponse[]>(`${this.baseUrl}/priorities`, { headers: this.buildHeaders(), params })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  createPriority(request: CreateTicketPriorityRequest): Observable<TicketPriorityResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.post<TicketPriorityResponse>(`${this.baseUrl}/priorities`, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  updatePriority(priorityId: string, request: UpdateTicketPriorityRequest): Observable<TicketPriorityResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<TicketPriorityResponse>(`${this.baseUrl}/priorities/${priorityId}`, request, {
          headers: this.buildHeaders()
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  getHistory(ticketId: string): Observable<TicketHistoryResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<TicketHistoryResponse>(`${this.baseUrl}/${ticketId}/history`, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  listTicketMessages(ticketId: string, page = 1, pageSize = 20): Observable<TicketMessageListResponse> {
    const params = new HttpParams().set('ticketId', ticketId).set('page', page).set('pageSize', pageSize);
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<TicketMessageListResponse>(this.ticketMessagesUrl, { headers: this.buildHeaders(), params })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  createTicketMessage(request: CreateTicketMessageRequest): Observable<CreateTicketMessageResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.post<CreateTicketMessageResponse>(this.ticketMessagesUrl, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  listTicketAttachments(ticketId: string): Observable<TicketAttachmentResponse[]> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<TicketAttachmentResponse[]>(`${this.baseUrl}/${ticketId}/attachments`, {
          headers: this.buildHeaders()
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  uploadTicketAttachment(ticketId: string, file: File): Observable<TicketAttachmentResponse> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.authService
      .withAutoRefresh(() =>
        this.http.post<TicketAttachmentResponse>(`${this.baseUrl}/${ticketId}/attachments`, formData, {
          headers: this.buildHeaders()
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  downloadTicketAttachment(ticketId: string, attachmentId: string): Observable<Blob> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get(`${this.baseUrl}/${ticketId}/attachments/${attachmentId}/content`, {
          headers: this.buildHeaders(),
          responseType: 'blob'
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  private buildHeaders(): HttpHeaders {
    const accessToken = this.authService.accessToken();
    return accessToken
      ? new HttpHeaders({ Authorization: `Bearer ${accessToken}` })
      : new HttpHeaders();
  }

  private mapError(error: unknown): Observable<never> {
    if (error instanceof Error && !(error instanceof HttpErrorResponse)) {
      return throwError(() => error);
    }

    const httpError = error as HttpErrorResponse;
    if (httpError.status === 0) {
      return throwError(() => new Error('Gateway is unreachable. Ensure UI, gateway, and API are running.'));
    }

    if (httpError.status === 401) {
      return throwError(() => new Error('Your session expired. Please sign in again.'));
    }

    if (httpError.status === 403) {
      return throwError(() => new Error('You do not have permission to perform this action.'));
    }

    const payload = httpError.error as (ApiValidationError & { detail?: string; message?: string }) | null;
    const problemDetail =
      payload && typeof payload === 'object' && 'detail' in payload && typeof payload.detail === 'string'
        ? payload.detail
        : null;
    const validationMessage = payload?.errors
      ? Object.entries(payload.errors)
          .map(([key, values]) => `${key}: ${values.join(', ')}`)
          .join(' | ')
      : null;

    const message = validationMessage || problemDetail || payload?.message || payload?.title || httpError.message || 'Request failed';
    return throwError(() => new Error(message));
  }
}
