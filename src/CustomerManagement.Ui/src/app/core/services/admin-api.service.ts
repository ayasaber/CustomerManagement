import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export interface AdminUserResponse {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: string;
  roles: string[];
}

export interface AdminUsersListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: AdminUserResponse[];
}

export interface CreateAdminUserRequest {
  email: string;
  password: string;
  displayName: string;
  roles: string[];
}

export interface UpdateAdminUserRequest {
  displayName: string;
  isActive: boolean;
  rowVersion: string;
}

export interface UpdateUserRolesRequest {
  roles: string[];
  rowVersion: string;
}

export interface PermissionResponse {
  id: string;
  name: string;
  description?: string | null;
  createdAtUtc: string;
}

export interface RolePermissionsResponse {
  roleId: string;
  roleName: string;
  permissions: PermissionResponse[];
}

export interface AuditLogItemResponse {
  id: string;
  occurredAtUtc: string;
  actorUserId?: string | null;
  actorEmail?: string | null;
  actionType: string;
  entityName: string;
  entityId: string;
  result: string;
  metadataJson?: string | null;
}

export interface AuditLogResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: AuditLogItemResponse[];
}

export interface AuditLogQuery {
  page?: number;
  pageSize?: number;
  userId?: string;
  actionType?: string;
  fromUtc?: string;
  toUtc?: string;
}

export interface SystemSettingResponse {
  id: string;
  key: string;
  value: string;
  description?: string | null;
  updatedAtUtc: string;
  rowVersion: string;
}

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly adminBaseUrl = 'http://localhost:5101/api/admin';

  constructor(
    private readonly http: HttpClient,
    private readonly authService: AuthService
  ) {}

  listUsers(page = 1, pageSize = 50, isActive = true, role?: string): Observable<AdminUsersListResponse> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('isActive', isActive);

    if (role) {
      params = params.set('role', role);
    }

    return this.withAuth(() =>
      this.http.get<AdminUsersListResponse>(`${this.adminBaseUrl}/users`, {
        headers: this.authHeaders(),
        params
      })
    );
  }

  getUser(userId: string): Observable<AdminUserResponse> {
    return this.withAuth(() =>
      this.http.get<AdminUserResponse>(`${this.adminBaseUrl}/users/${userId}`, {
        headers: this.authHeaders()
      })
    );
  }

  createUser(request: CreateAdminUserRequest): Observable<AdminUserResponse> {
    return this.withAuth(() =>
      this.http.post<AdminUserResponse>(`${this.adminBaseUrl}/users`, request, {
        headers: this.authHeaders()
      })
    );
  }

  updateUser(userId: string, request: UpdateAdminUserRequest): Observable<AdminUserResponse> {
    return this.withAuth(() =>
      this.http.put<AdminUserResponse>(`${this.adminBaseUrl}/users/${userId}`, request, {
        headers: this.authHeaders()
      })
    );
  }

  deactivateUser(userId: string): Observable<void> {
    return this.withAuth(() =>
      this.http.delete<void>(`${this.adminBaseUrl}/users/${userId}`, {
        headers: this.authHeaders()
      })
    );
  }

  assignUserRoles(userId: string, request: UpdateUserRolesRequest): Observable<AdminUserResponse> {
    return this.withAuth(() =>
      this.http.put<AdminUserResponse>(`${this.adminBaseUrl}/users/${userId}/roles`, request, {
        headers: this.authHeaders()
      })
    );
  }

  listPermissions(): Observable<PermissionResponse[]> {
    return this.withAuth(() =>
      this.http.get<PermissionResponse[]>(`${this.adminBaseUrl}/permissions`, {
        headers: this.authHeaders()
      })
    );
  }

  createPermission(name: string, description?: string): Observable<PermissionResponse> {
    return this.withAuth(() =>
      this.http.post<PermissionResponse>(
        `${this.adminBaseUrl}/permissions`,
        { name, description: description || null },
        { headers: this.authHeaders() }
      )
    );
  }

  updatePermission(permissionId: string, name: string, description?: string): Observable<PermissionResponse> {
    return this.withAuth(() =>
      this.http.put<PermissionResponse>(
        `${this.adminBaseUrl}/permissions/${permissionId}`,
        { name, description: description || null },
        { headers: this.authHeaders() }
      )
    );
  }

  getRolePermissions(roleId: string): Observable<RolePermissionsResponse> {
    return this.withAuth(() =>
      this.http.get<RolePermissionsResponse>(`${this.adminBaseUrl}/roles/${roleId}/permissions`, {
        headers: this.authHeaders()
      })
    );
  }

  updateRolePermissions(roleId: string, permissionIds: string[]): Observable<RolePermissionsResponse> {
    return this.withAuth(() =>
      this.http.put<RolePermissionsResponse>(
        `${this.adminBaseUrl}/roles/${roleId}/permissions`,
        { permissionIds },
        { headers: this.authHeaders() }
      )
    );
  }

  queryAuditLogs(query: AuditLogQuery): Observable<AuditLogResponse> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 50);

    if (query.userId) {
      params = params.set('userId', query.userId);
    }

    if (query.actionType) {
      params = params.set('actionType', query.actionType);
    }

    if (query.fromUtc) {
      params = params.set('fromUtc', query.fromUtc);
    }

    if (query.toUtc) {
      params = params.set('toUtc', query.toUtc);
    }

    return this.withAuth(() =>
      this.http.get<AuditLogResponse>(`${this.adminBaseUrl}/audit-logs`, {
        headers: this.authHeaders(),
        params
      })
    );
  }

  listSystemSettings(): Observable<SystemSettingResponse[]> {
    return this.withAuth(() =>
      this.http.get<SystemSettingResponse[]>(`${this.adminBaseUrl}/system-settings`, {
        headers: this.authHeaders()
      })
    );
  }

  updateSystemSetting(key: string, value: string, rowVersion: string): Observable<SystemSettingResponse> {
    return this.withAuth(() =>
      this.http.put<SystemSettingResponse>(
        `${this.adminBaseUrl}/system-settings/${encodeURIComponent(key)}`,
        { value, rowVersion },
        { headers: this.authHeaders() }
      )
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
    const payload = httpError?.error as { message?: string; title?: string; detail?: string; errors?: Record<string, string[]> } | null;

    const validationMessage = payload?.errors
      ? Object.entries(payload.errors)
          .map(([key, values]) => `${key}: ${values.join(', ')}`)
          .join(' | ')
      : null;

    const message =
      validationMessage ||
      payload?.detail ||
      payload?.message ||
      payload?.title ||
      httpError?.message ||
      'Admin request failed.';

    return throwError(() => new Error(message));
  }
}
