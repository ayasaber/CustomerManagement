import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  AdminApiService,
  AdminUserResponse,
  CreateAdminUserRequest,
  UpdateAdminUserRequest,
  UpdateUserRolesRequest
} from '../../../core/services/admin-api.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-admin-users-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="admin-card">
      <header class="section-header">
        <h2>Users</h2>
        <button class="btn-primary" type="button" (click)="openCreateModal()">Create User</button>
      </header>

      <p class="status-banner error" *ngIf="error()">{{ error() }}</p>
      <p class="status-banner success" *ngIf="success()">{{ success() }}</p>

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Email</th>
              <th>Display Name</th>
              <th>Roles</th>
              <th>Active</th>
              <th>Created (UTC)</th>
              <th>Updated (UTC)</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let user of users()">
              <td>{{ user.email }}</td>
              <td>{{ user.displayName }}</td>
              <td>
                <div class="tag-list">
                  <span class="tag" *ngFor="let role of user.roles">{{ role }}</span>
                </div>
              </td>
              <td>{{ user.isActive ? 'Yes' : 'No' }}</td>
              <td>{{ user.createdAtUtc }}</td>
              <td>{{ user.updatedAtUtc }}</td>
              <td>
                <button class="btn-secondary" type="button" (click)="openEditPanel(user)">Edit</button>
                <button class="btn-secondary" type="button" (click)="openRolesModal(user)">Roles</button>
                <button class="btn-danger" type="button" (click)="deactivate(user)" [disabled]="isCurrentUser(user)">Deactivate</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="admin-card" *ngIf="showCreateModal()">
      <header class="section-header">
        <h2>Create User</h2>
        <button class="btn-secondary" type="button" (click)="closeCreateModal()">Close</button>
      </header>

      <form class="form-grid" [formGroup]="createForm" (ngSubmit)="submitCreate()">
        <label>
          Email
          <input formControlName="email" type="email" />
        </label>
        <label>
          Display Name
          <input formControlName="displayName" type="text" />
        </label>
        <label>
          Password
          <input formControlName="password" type="password" />
        </label>
        <label>
          Roles (comma separated)
          <input formControlName="roles" type="text" placeholder="agent" />
        </label>

        <div>
          <button class="btn-primary" type="submit" [disabled]="createForm.invalid || loading()">Create</button>
        </div>
      </form>
    </section>

    <section class="admin-card" *ngIf="editingUser()">
      <header class="section-header">
        <h2>Edit User</h2>
        <button class="btn-secondary" type="button" (click)="closeEditPanel()">Close</button>
      </header>

      <form class="form-grid" [formGroup]="editForm" (ngSubmit)="saveEdit()">
        <label>
          Display Name
          <input formControlName="displayName" type="text" />
        </label>
        <label>
          Active
          <select formControlName="isActive">
            <option [ngValue]="true">Active</option>
            <option [ngValue]="false">Inactive</option>
          </select>
        </label>

        <div>
          <button class="btn-primary" type="submit" [disabled]="editForm.invalid || loading()">Save</button>
        </div>
      </form>
    </section>

    <section class="admin-card" *ngIf="rolesUser()">
      <header class="section-header">
        <h2>Assign Roles</h2>
        <button class="btn-secondary" type="button" (click)="closeRolesModal()">Close</button>
      </header>

      <form class="form-grid" [formGroup]="rolesForm" (ngSubmit)="saveRoles()">
        <label>
          Roles (comma separated)
          <input formControlName="roles" type="text" placeholder="agent,customer" />
        </label>

        <div>
          <button class="btn-primary" type="submit" [disabled]="rolesForm.invalid || loading()">Update Roles</button>
        </div>
      </form>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminUsersPageComponent implements OnInit {
  private readonly adminApi = inject(AdminApiService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);

  readonly users = signal<AdminUserResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  readonly showCreateModal = signal(false);
  readonly editingUser = signal<AdminUserResponse | null>(null);
  readonly rolesUser = signal<AdminUserResponse | null>(null);

  readonly createForm = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/)
      ]
    ],
    roles: ['agent', [Validators.required]]
  });

  readonly editForm = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    isActive: [true, [Validators.required]]
  });

  readonly rolesForm = this.formBuilder.nonNullable.group({
    roles: ['agent', [Validators.required]]
  });

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.beginRequest();
    this.adminApi
      .listUsers(1, 100, true)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          this.users.set(response.items);
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  openCreateModal(): void {
    this.showCreateModal.set(true);
  }

  closeCreateModal(): void {
    this.showCreateModal.set(false);
    this.createForm.reset({ email: '', displayName: '', password: '', roles: 'agent' });
  }

  submitCreate(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    this.beginRequest();
    const parsedRoles = this.parseRoles(this.createForm.controls.roles.value);
    if (parsedRoles.length === 0) {
      this.loading.set(false);
      this.error.set('At least one role is required.');
      return;
    }

    const request: CreateAdminUserRequest = {
      email: this.createForm.controls.email.value.trim(),
      displayName: this.createForm.controls.displayName.value.trim(),
      password: this.createForm.controls.password.value,
      roles: parsedRoles
    };

    this.adminApi
      .createUser(request)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.success.set('User created successfully.');
          this.closeCreateModal();
          this.loadUsers();
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  openEditPanel(user: AdminUserResponse): void {
    this.editingUser.set(user);
    this.editForm.patchValue({
      displayName: user.displayName,
      isActive: user.isActive
    });
  }

  closeEditPanel(): void {
    this.editingUser.set(null);
  }

  saveEdit(): void {
    const user = this.editingUser();
    if (!user || this.editForm.invalid) {
      return;
    }

    this.beginRequest();
    const request: UpdateAdminUserRequest = {
      displayName: this.editForm.controls.displayName.value.trim(),
      isActive: this.editForm.controls.isActive.value,
      rowVersion: user.rowVersion
    };

    this.adminApi
      .updateUser(user.id, request)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.success.set('User updated.');
          this.closeEditPanel();
          this.loadUsers();
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  openRolesModal(user: AdminUserResponse): void {
    this.rolesUser.set(user);
    this.rolesForm.patchValue({ roles: user.roles.join(', ') });
  }

  closeRolesModal(): void {
    this.rolesUser.set(null);
  }

  saveRoles(): void {
    const user = this.rolesUser();
    if (!user || this.rolesForm.invalid) {
      return;
    }

    this.beginRequest();
    const request: UpdateUserRolesRequest = {
      roles: this.parseRoles(this.rolesForm.controls.roles.value),
      rowVersion: user.rowVersion
    };

    this.adminApi
      .assignUserRoles(user.id, request)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.success.set('User roles updated.');
          this.closeRolesModal();
          this.loadUsers();
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  deactivate(user: AdminUserResponse): void {
    if (this.isCurrentUser(user)) {
      this.error.set('Administrators cannot deactivate their own account.');
      return;
    }

    const confirmed = window.confirm(`Deactivate ${user.email}?`);
    if (!confirmed) {
      return;
    }

    this.beginRequest();
    this.adminApi
      .deactivateUser(user.id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => {
          this.success.set('User deactivated.');
          this.loadUsers();
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  private parseRoles(input: string): string[] {
    return input
      .split(',')
      .map((value) => value.trim().toLowerCase())
      .filter((value) => value.length > 0);
  }

  private beginRequest(): void {
    this.loading.set(true);
    this.error.set('');
    this.success.set('');
  }

  isCurrentUser(user: AdminUserResponse): boolean {
    const currentUser = this.authService.currentUser()();
    if (!currentUser) {
      return false;
    }

    return currentUser.userId === user.id || currentUser.email.toLowerCase() === user.email.toLowerCase();
  }
}
