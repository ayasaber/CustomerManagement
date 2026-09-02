import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import {
  CompleteTicketTaskRequest,
  CreateQuickReplyRequest,
  CreateTicketHandoffRequest,
  CreateTicketInternalNoteRequest,
  CreateTicketTaskRequest,
  DashboardAssignedTicketsResponse,
  DashboardAgentDirectoryResponse,
  DashboardCustomerContextResponse,
  DashboardOpenTaskSummaryResponse,
  QuickReplyListResponse,
  QuickReplyResponse,
  RespondTicketHandoffRequest,
  TicketHandoffRequestListResponse,
  TicketHandoffRequestResponse,
  TicketInternalNoteCreateResponse,
  TicketInternalNoteListResponse,
  TicketTaskListResponse,
  TicketTaskResponse,
  UpdateQuickReplyRequest,
  UpdateTicketTaskRequest
} from '../models/agent-dashboard.models';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AgentDashboardApiService {
  private readonly gatewayBaseUrl = 'http://localhost:5101/api';

  constructor(
    private readonly http: HttpClient,
    private readonly authService: AuthService
  ) {}

  getAssignedTickets(page = 1, pageSize = 20, status?: string): Observable<DashboardAssignedTicketsResponse> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }

    return this.withAuth(() =>
      this.http.get<DashboardAssignedTicketsResponse>(`${this.gatewayBaseUrl}/dashboard/me/assigned-tickets`, {
        headers: this.authHeaders(),
        params
      })
    );
  }

  getOpenTasks(page = 1, pageSize = 20): Observable<DashboardOpenTaskSummaryResponse> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.withAuth(() =>
      this.http.get<DashboardOpenTaskSummaryResponse>(`${this.gatewayBaseUrl}/dashboard/me/open-tasks`, {
        headers: this.authHeaders(),
        params
      })
    );
  }

  getTicketCustomerContext(ticketId: string): Observable<DashboardCustomerContextResponse> {
    return this.withAuth(() =>
      this.http.get<DashboardCustomerContextResponse>(`${this.gatewayBaseUrl}/dashboard/tickets/${ticketId}/customer-context`, {
        headers: this.authHeaders()
      })
    );
  }

  getAssignableAgents(): Observable<DashboardAgentDirectoryResponse> {
    return this.withAuth(() =>
      this.http.get<DashboardAgentDirectoryResponse>(`${this.gatewayBaseUrl}/dashboard/agents`, {
        headers: this.authHeaders()
      })
    );
  }

  listTicketNotes(ticketId: string): Observable<TicketInternalNoteListResponse> {
    const params = new HttpParams().set('ticketId', ticketId);
    return this.withAuth(() =>
      this.http.get<TicketInternalNoteListResponse>(`${this.gatewayBaseUrl}/ticket-notes`, {
        headers: this.authHeaders(),
        params
      })
    );
  }

  createTicketNote(request: CreateTicketInternalNoteRequest): Observable<TicketInternalNoteCreateResponse> {
    return this.withAuth(() =>
      this.http.post<TicketInternalNoteCreateResponse>(`${this.gatewayBaseUrl}/ticket-notes`, request, {
        headers: this.authHeaders()
      })
    );
  }

  createHandoffRequest(noteId: string, request: CreateTicketHandoffRequest): Observable<TicketHandoffRequestResponse> {
    return this.withAuth(() =>
      this.http.post<TicketHandoffRequestResponse>(`${this.gatewayBaseUrl}/ticket-notes/${noteId}/handoff-requests`, request, {
        headers: this.authHeaders()
      })
    );
  }

  getMyHandoffRequests(): Observable<TicketHandoffRequestListResponse> {
    return this.withAuth(() =>
      this.http.get<TicketHandoffRequestListResponse>(`${this.gatewayBaseUrl}/ticket-notes/handoff-requests/me`, {
        headers: this.authHeaders()
      })
    );
  }

  respondToHandoffRequest(
    handoffRequestId: string,
    request: RespondTicketHandoffRequest
  ): Observable<TicketHandoffRequestResponse> {
    return this.withAuth(() =>
      this.http.post<TicketHandoffRequestResponse>(
        `${this.gatewayBaseUrl}/ticket-notes/handoff-requests/${handoffRequestId}/respond`,
        request,
        {
          headers: this.authHeaders()
        }
      )
    );
  }

  listQuickReplies(activeOnly = true): Observable<QuickReplyListResponse> {
    const params = new HttpParams().set('activeOnly', activeOnly);
    return this.withAuth(() =>
      this.http.get<QuickReplyListResponse>(`${this.gatewayBaseUrl}/quick-replies`, {
        headers: this.authHeaders(),
        params
      })
    );
  }

  createQuickReply(request: CreateQuickReplyRequest): Observable<QuickReplyResponse> {
    return this.withAuth(() =>
      this.http.post<QuickReplyResponse>(`${this.gatewayBaseUrl}/quick-replies`, request, {
        headers: this.authHeaders()
      })
    );
  }

  updateQuickReply(quickReplyId: string, request: UpdateQuickReplyRequest): Observable<QuickReplyResponse> {
    return this.withAuth(() =>
      this.http.put<QuickReplyResponse>(`${this.gatewayBaseUrl}/quick-replies/${quickReplyId}`, request, {
        headers: this.authHeaders()
      })
    );
  }

  createTicketTask(request: CreateTicketTaskRequest): Observable<TicketTaskResponse> {
    return this.withAuth(() =>
      this.http.post<TicketTaskResponse>(`${this.gatewayBaseUrl}/ticket-tasks`, request, {
        headers: this.authHeaders()
      })
    );
  }

  updateTicketTask(taskId: string, request: UpdateTicketTaskRequest): Observable<TicketTaskResponse> {
    return this.withAuth(() =>
      this.http.put<TicketTaskResponse>(`${this.gatewayBaseUrl}/ticket-tasks/${taskId}`, request, {
        headers: this.authHeaders()
      })
    );
  }

  completeTicketTask(taskId: string, request: CompleteTicketTaskRequest): Observable<TicketTaskResponse> {
    return this.withAuth(() =>
      this.http.put<TicketTaskResponse>(`${this.gatewayBaseUrl}/ticket-tasks/${taskId}/complete`, request, {
        headers: this.authHeaders()
      })
    );
  }

  listTicketTasks(assignedToMeOnly = true): Observable<TicketTaskListResponse> {
    const params = new HttpParams().set('assignedToMeOnly', assignedToMeOnly);
    return this.withAuth(() =>
      this.http.get<TicketTaskListResponse>(`${this.gatewayBaseUrl}/ticket-tasks`, {
        headers: this.authHeaders(),
        params
      })
    );
  }

  private withAuth<T>(requestFactory: () => Observable<T>): Observable<T> {
    return this.authService.withAutoRefresh(requestFactory).pipe(catchError((error) => this.mapError(error)));
  }

  private authHeaders(): HttpHeaders {
    const accessToken = this.authService.accessToken();
    return accessToken ? new HttpHeaders({ Authorization: `Bearer ${accessToken}` }) : new HttpHeaders();
  }

  private mapError(error: unknown): Observable<never> {
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

    if (httpError.status === 409) {
      return throwError(() => new Error('Conflict detected. Reload latest data and try again.'));
    }

    const payload = httpError.error as { message?: string; title?: string; detail?: string; errors?: Record<string, string[]> } | null;
    const validationMessage = payload?.errors
      ? Object.entries(payload.errors)
          .map(([key, values]) => `${key}: ${values.join(', ')}`)
          .join(' | ')
      : null;

    const message = validationMessage || payload?.detail || payload?.message || payload?.title || httpError.message || 'Request failed';
    return throwError(() => new Error(message));
  }
}
