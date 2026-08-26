import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AdminApiService, PermissionResponse } from '../../../core/services/admin-api.service';

interface RoleTarget {
  name: string;
  roleId: string;
}

@Component({
  selector: 'app-admin-roles-permissions-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="admin-card">
      <header class="section-header">
        <h2>Roles & Permissions</h2>
      </header>

      <p class="status-banner error" *ngIf="error()">{{ error() }}</p>
      <p class="status-banner success" *ngIf="success()">{{ success() }}</p>

      <div class="form-grid">
        <label>
          Role
          <select [formControl]="roleControl">
            <option *ngFor="let role of roles()" [ngValue]="role">{{ role.name }}</option>
          </select>
        </label>
        <label>
          Role Id (GUID)
          <input [formControl]="roleIdControl" placeholder="paste role id" />
        </label>
      </div>

      <div style="margin-top: 0.8rem; display: flex; gap: 0.6rem;">
        <button class="btn-secondary" type="button" (click)="loadRolePermissions()" [disabled]="!roleIdControl.value">Load</button>
        <button class="btn-primary" type="button" (click)="save()" [disabled]="loading() || !roleIdControl.value">Save</button>
      </div>

      <div style="margin-top: 1rem;" *ngIf="permissions().length > 0">
        <h3 style="margin: 0 0 0.6rem;">Permission checklist</h3>
        <div class="form-grid" style="grid-template-columns: repeat(3, minmax(0, 1fr));">
          <label *ngFor="let permission of permissions()" style="font-weight: 500;">
            <input
              type="checkbox"
              [checked]="selectedPermissionIds().has(permission.id)"
              (change)="togglePermission(permission.id, $event)" />
            {{ permission.name }}
          </label>
        </div>
      </div>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminRolesPermissionsPageComponent implements OnInit {
  private readonly adminApi = inject(AdminApiService);
  private readonly formBuilder = inject(FormBuilder);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  readonly permissions = signal<PermissionResponse[]>([]);
  readonly selectedPermissionIds = signal<Set<string>>(new Set<string>());
  readonly roles = signal<RoleTarget[]>([
    { name: 'Admin', roleId: '' },
    { name: 'Agent', roleId: '' },
    { name: 'Customer', roleId: '' }
  ]);

  readonly roleControl = this.formBuilder.control<RoleTarget | null>(this.roles()[0], { validators: [Validators.required] });
  readonly roleIdControl = this.formBuilder.nonNullable.control('', [Validators.required]);

  ngOnInit(): void {
    this.adminApi.listPermissions().subscribe({
      next: (permissions) => this.permissions.set(permissions),
      error: (error: Error) => this.error.set(error.message)
    });

    const selected = this.roleControl.value;
    if (selected?.roleId) {
      this.roleIdControl.setValue(selected.roleId);
    }

    this.roleControl.valueChanges.subscribe((role) => {
      this.roleIdControl.setValue(role?.roleId || '');
    });
  }

  loadRolePermissions(): void {
    if (!this.roleIdControl.value) {
      return;
    }

    this.beginRequest();
    this.adminApi
      .getRolePermissions(this.roleIdControl.value)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          this.selectedPermissionIds.set(new Set(response.permissions.map((permission) => permission.id)));
          this.success.set(`Loaded permissions for ${response.roleName}.`);
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  save(): void {
    if (!this.roleIdControl.value) {
      return;
    }

    this.beginRequest();
    this.adminApi
      .updateRolePermissions(this.roleIdControl.value, Array.from(this.selectedPermissionIds()))
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          this.success.set(`Saved ${response.permissions.length} permissions for ${response.roleName}.`);
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  togglePermission(permissionId: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const updated = new Set(this.selectedPermissionIds());

    if (checked) {
      updated.add(permissionId);
    } else {
      updated.delete(permissionId);
    }

    this.selectedPermissionIds.set(updated);
  }

  private beginRequest(): void {
    this.loading.set(true);
    this.error.set('');
    this.success.set('');
  }
}
