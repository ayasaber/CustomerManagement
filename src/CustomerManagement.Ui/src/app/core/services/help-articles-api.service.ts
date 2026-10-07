import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ApiValidationError } from '../models/customer-management.models';
import { CreateHelpArticleRequest, HelpArticleResponse, UpdateHelpArticleRequest } from '../models/help-article.models';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class HelpArticlesApiService {
  private readonly baseUrl = 'http://localhost:5101/api/help-articles';

  constructor(
    private readonly http: HttpClient,
    private readonly authService: AuthService
  ) {}

  listHelpArticles(activeOnly?: boolean): Observable<HelpArticleResponse[]> {
    const params = activeOnly === undefined ? undefined : new HttpParams().set('activeOnly', activeOnly);
    return this.authService
      .withAutoRefresh(() =>
        this.http.get<HelpArticleResponse[]>(this.baseUrl, { headers: this.buildHeaders(), params })
      )
      .pipe(catchError((error) => this.mapError(error)));
  }

  createHelpArticle(request: CreateHelpArticleRequest): Observable<HelpArticleResponse> {
    return this.authService
      .withAutoRefresh(() => this.http.post<HelpArticleResponse>(this.baseUrl, request, { headers: this.buildHeaders() }))
      .pipe(catchError((error) => this.mapError(error)));
  }

  updateHelpArticle(articleId: string, request: UpdateHelpArticleRequest): Observable<HelpArticleResponse> {
    return this.authService
      .withAutoRefresh(() =>
        this.http.put<HelpArticleResponse>(`${this.baseUrl}/${articleId}`, request, { headers: this.buildHeaders() })
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
