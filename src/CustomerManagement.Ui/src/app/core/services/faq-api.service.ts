import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ApiValidationError } from '../models/customer-management.models';
import { CreateFaqEntryRequest, FaqEntryResponse, UpdateFaqEntryRequest } from '../models/faq.models';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class FaqApiService {
  private readonly baseUrl = 'http://localhost:5101/api/faq';

  constructor(
    private readonly http: HttpClient,
    private readonly authService: AuthService
  ) {}

  listFaqEntries(activeOnly?: boolean): Observable<FaqEntryResponse[]> {
    const params = activeOnly === undefined ? undefined : new HttpParams().set('activeOnly', activeOnly);
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<FaqEntryResponse[]>(this.baseUrl, { headers: this.buildHeaders(), params })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  createFaqEntry(request: CreateFaqEntryRequest): Observable<FaqEntryResponse> {
    return this.authService
      .withAutoRefresh(() => this.http.post<FaqEntryResponse>(this.baseUrl, request, { headers: this.buildHeaders() }))
      .pipe(catchError((error) => this.mapError(error)));
  }

  updateFaqEntry(faqId: string, request: UpdateFaqEntryRequest): Observable<FaqEntryResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<FaqEntryResponse>(`${this.baseUrl}/${faqId}`, request, { headers: this.buildHeaders() })
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
