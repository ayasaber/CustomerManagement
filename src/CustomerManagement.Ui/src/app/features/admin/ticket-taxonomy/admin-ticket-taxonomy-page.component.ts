import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  TicketCategoryResponse,
  TicketPriorityResponse
} from '../../../core/models/ticket-management.models';
import { TicketManagementApiService } from '../../../core/services/ticket-management-api.service';

@Component({
  selector: 'app-admin-ticket-taxonomy-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="taxonomy-page">
      <h2>Ticket Taxonomy</h2>

      <p class="status error" *ngIf="error()">{{ error() }}</p>
      <p class="status success" *ngIf="success()">{{ success() }}</p>

      <div class="forms">
        <form [formGroup]="categoryForm" (ngSubmit)="createCategory()" class="card">
          <h3>New Category</h3>
          <label>
            Name
            <input type="text" formControlName="name" maxlength="100" />
          </label>
          <label>
            Description
            <textarea formControlName="description" rows="3" maxlength="500"></textarea>
          </label>
          <button type="submit" [disabled]="categoryForm.invalid || loading()">Create Category</button>
        </form>

        <form [formGroup]="priorityForm" (ngSubmit)="createPriority()" class="card">
          <h3>New Priority</h3>
          <label>
            Name
            <input type="text" formControlName="name" maxlength="100" />
          </label>
          <label>
            Sort Order
            <input type="number" formControlName="sortOrder" min="0" max="1000" />
          </label>
          <button type="submit" [disabled]="priorityForm.invalid || loading()">Create Priority</button>
        </form>
      </div>

      <div class="lists">
        <section class="card">
          <h3>Categories</h3>
          <ul>
            <li *ngFor="let category of categories()">{{ category.name }} <small>({{ category.isActive ? 'active' : 'inactive' }})</small></li>
          </ul>
        </section>

        <section class="card">
          <h3>Priorities</h3>
          <ul>
            <li *ngFor="let priority of priorities()">{{ priority.name }} <small>(sort: {{ priority.sortOrder }})</small></li>
          </ul>
        </section>
      </div>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminTicketTaxonomyPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly categories = signal<TicketCategoryResponse[]>([]);
  readonly priorities = signal<TicketPriorityResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  readonly categoryForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.maxLength(500)]]
  });

  readonly priorityForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    sortOrder: [0, [Validators.required, Validators.min(0), Validators.max(1000)]]
  });

  constructor(
    private readonly api: TicketManagementApiService
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  createCategory(): void {
    if (this.categoryForm.invalid) {
      return;
    }

    const value = this.categoryForm.getRawValue();
    this.beginRequest();
    this.api
      .createCategory({
        name: value.name.trim(),
        description: value.description.trim() || null
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('Category created.');
          this.categoryForm.reset({ name: '', description: '' });
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Create category failed: ${err.message}`)
      });
  }

  createPriority(): void {
    if (this.priorityForm.invalid) {
      return;
    }

    const value = this.priorityForm.getRawValue();
    this.beginRequest();
    this.api
      .createPriority({
        name: value.name.trim(),
        sortOrder: value.sortOrder
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('Priority created.');
          this.priorityForm.reset({ name: '', sortOrder: 0 });
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Create priority failed: ${err.message}`)
      });
  }

  private refresh(): void {
    this.api.listCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: (err: Error) => this.error.set(`Category list failed: ${err.message}`)
    });

    this.api.listPriorities().subscribe({
      next: (priorities) => this.priorities.set(priorities),
      error: (err: Error) => this.error.set(`Priority list failed: ${err.message}`)
    });
  }

  private beginRequest(): void {
    this.loading.set(true);
    this.error.set('');
    this.success.set('');
  }

  private finishRequest(): void {
    this.loading.set(false);
  }
}
