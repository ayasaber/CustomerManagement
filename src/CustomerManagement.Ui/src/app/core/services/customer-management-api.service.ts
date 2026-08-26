import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import {
  ApiValidationError,
  CreateCustomerNoteRequest,
  CreateCustomerProfileRequest,
  CustomerAttachmentResponse,
  CustomerContactDetailsResponse,
  CustomerInteractionHistoryResponse,
  CustomerNoteResponse,
  CustomerProfileResponse,
  UpdateCustomerProfileRequest
} from '../models/customer-management.models';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class CustomerManagementApiService {
  private readonly baseUrl = 'http://localhost:5101/api/customers';

  constructor(
    private readonly http: HttpClient,
    private readonly authService: AuthService
  ) {}

  createProfile(request: CreateCustomerProfileRequest): Observable<CustomerProfileResponse> {
    return this.authService
      .withAutoRefresh(() => this.http.post<CustomerProfileResponse>(this.baseUrl, request, { headers: this.buildHeaders() }))
      .pipe(catchError((error) => this.mapError(error)));
  }

  getProfile(customerId: string): Observable<CustomerProfileResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<CustomerProfileResponse>(`${this.baseUrl}/${customerId}`, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  updateProfile(customerId: string, request: UpdateCustomerProfileRequest): Observable<CustomerProfileResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<CustomerProfileResponse>(`${this.baseUrl}/${customerId}`, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  getContactDetails(customerId: string): Observable<CustomerContactDetailsResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<CustomerContactDetailsResponse>(`${this.baseUrl}/${customerId}/contact-details`, {
          headers: this.buildHeaders()
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  getInteractionHistory(customerId: string, page = 1, pageSize = 20): Observable<CustomerInteractionHistoryResponse> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<CustomerInteractionHistoryResponse>(`${this.baseUrl}/${customerId}/interaction-history`, {
          headers: this.buildHeaders(),
          params
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  listNotes(customerId: string): Observable<CustomerNoteResponse[]> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<CustomerNoteResponse[]>(`${this.baseUrl}/${customerId}/notes`, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  addNote(customerId: string, request: CreateCustomerNoteRequest): Observable<CustomerNoteResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.post<CustomerNoteResponse>(`${this.baseUrl}/${customerId}/notes`, request, { headers: this.buildHeaders() })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  listAttachments(customerId: string): Observable<CustomerAttachmentResponse[]> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<CustomerAttachmentResponse[]>(`${this.baseUrl}/${customerId}/attachments`, {
          headers: this.buildHeaders()
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  uploadAttachment(customerId: string, file: File): Observable<CustomerAttachmentResponse> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.authService
      .withAutoRefresh(() =>
        this.http.post<CustomerAttachmentResponse>(`${this.baseUrl}/${customerId}/attachments`, formData, {
          headers: this.buildHeaders()
        })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  downloadAttachment(customerId: string, attachmentId: string): Observable<Blob> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.get(`${this.baseUrl}/${customerId}/attachments/${attachmentId}/content`, {
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

    const payload = httpError.error as ApiValidationError | null;
    const validationMessage = payload?.errors
      ? Object.entries(payload.errors)
          .map(([key, values]) => `${key}: ${values.join(', ')}`)
          .join(' | ')
      : null;

    const message = validationMessage || payload?.title || httpError.message || 'Request failed';
    return throwError(() => new Error(message));
  }
}
