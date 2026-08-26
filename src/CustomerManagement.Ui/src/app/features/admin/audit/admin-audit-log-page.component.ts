import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AdminApiService, AuditLogItemResponse } from '../../../core/services/admin-api.service';

@Component({
  selector: 'app-admin-audit-log-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="admin-card">
      <header class="section-header">
        <h2>Audit Logs</h2>
        <button class="btn-primary" type="button" (click)="query()" [disabled]="loading()">Run Query</button>
      </header>

      <form class="form-grid" [formGroup]="filterForm" (ngSubmit)="query()">
        <label>
          User Id
          <input formControlName="userId" placeholder="optional guid" />
        </label>
        <label>
          Action Type
          <input formControlName="actionType" placeholder="auth.login" />
        </label>
        <label>
          From UTC
          <input formControlName="fromUtc" type="datetime-local" />
        </label>
        <label>
          To UTC
          <input formControlName="toUtc" type="datetime-local" />
        </label>
      </form>

      <p class="status-banner warn" *ngIf="validation()">{{ validation() }}</p>
      <p class="status-banner error" *ngIf="error()">{{ error() }}</p>

      <div class="table-wrap">
        <table class="data-table">
          <thead>
            <tr>
              <th>Time (UTC)</th>
              <th>Actor</th>
              <th>Action Type</th>
              <th>Entity</th>
              <th>Entity Id</th>
              <th>Result</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let item of items()">
              <td>{{ item.occurredAtUtc }}</td>
              <td>{{ item.actorEmail || item.actorUserId || '-' }}</td>
              <td>{{ item.actionType }}</td>
              <td>{{ item.entityName }}</td>
              <td>{{ item.entityId }}</td>
              <td>{{ item.result }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <p>Total: {{ totalCount() }}</p>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminAuditLogPageComponent {
  private readonly adminApi = inject(AdminApiService);
  private readonly formBuilder = inject(FormBuilder);

  readonly items = signal<AuditLogItemResponse[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly validation = signal('');

  readonly filterForm = this.formBuilder.nonNullable.group({
    userId: [''],
    actionType: [''],
    fromUtc: [''],
    toUtc: [''],
    page: [1, [Validators.required, Validators.min(1)]],
    pageSize: [50, [Validators.required, Validators.min(1), Validators.max(200)]]
  });

  query(): void {
    this.error.set('');
    this.validation.set('');

    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }

    const fromUtc = this.filterForm.controls.fromUtc.value;
    const toUtc = this.filterForm.controls.toUtc.value;

    if (fromUtc && toUtc && new Date(fromUtc) > new Date(toUtc)) {
      this.validation.set('From UTC must be earlier than or equal to To UTC.');
      return;
    }

    this.loading.set(true);
    this.adminApi
      .queryAuditLogs({
        page: this.filterForm.controls.page.value,
        pageSize: this.filterForm.controls.pageSize.value,
        userId: this.filterForm.controls.userId.value.trim() || undefined,
        actionType: this.filterForm.controls.actionType.value.trim() || undefined,
        fromUtc: fromUtc || undefined,
        toUtc: toUtc || undefined
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          this.items.set(response.items);
          this.totalCount.set(response.totalCount);
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }
}
