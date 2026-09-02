import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DashboardOpenTaskSummaryItemResponse } from '../../../core/models/agent-dashboard.models';

export interface CreateTaskDraftPayload {
  description: string;
  dueAtUtc: string;
}

@Component({
  selector: 'app-open-tasks-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="panel">
      <header>
        <h3>Open Tasks</h3>
        <span>{{ tasks.length }} items</span>
      </header>

      <form [formGroup]="form" (ngSubmit)="submitCreate()" class="compose">
        <label class="field">
          Add task for selected ticket
          <input formControlName="description" type="text" placeholder="Follow up with customer" />
        </label>

        <label class="field">
          Due (UTC)
          <input formControlName="dueAt" type="datetime-local" />
        </label>

        <p class="hint" *ngIf="!selectedTicketId">Select a ticket above to add a task.</p>
        <p class="error" *ngIf="submissionError">{{ submissionError }}</p>

        <button type="submit" [disabled]="!selectedTicketId">Add task</button>
      </form>

      <ul *ngIf="tasks.length; else emptyState">
        <li *ngFor="let task of tasks">
          <div class="task-meta">
            <strong>{{ task.description }}</strong>
            <small>{{ task.ticketNumber }} · Due {{ task.dueAtUtc | date: 'yyyy-MM-dd HH:mm':'UTC' }}</small>
          </div>
          <button type="button" class="complete" (click)="completeClicked.emit(task.taskId)">Mark done</button>
        </li>
      </ul>

      <ng-template #emptyState>
        <p class="empty">No open tasks.</p>
      </ng-template>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d8e2ef; border-radius: 14px; background: #fff; padding: 0.8rem; }
      header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 0.6rem; }
      h3 { margin: 0; font-size: 1rem; }
      .compose { display: grid; gap: 0.45rem; margin-bottom: 0.7rem; }
      .field { display: grid; gap: 0.2rem; color: #284968; font-size: 0.9rem; }
      input { width: 100%; border: 1px solid #bfd2e8; border-radius: 8px; padding: 0.42rem 0.5rem; }
      ul { list-style: none; padding: 0; margin: 0; display: grid; gap: 0.5rem; }
      li { display: flex; justify-content: space-between; gap: 0.6rem; border: 1px solid #ecf1f6; border-radius: 10px; padding: 0.55rem; }
      .task-meta { display: grid; gap: 0.15rem; }
      small { color: #5d7592; }
      button { border: 1px solid #86a3c5; border-radius: 8px; background: #f6fbff; color: #1f4f84; padding: 0.4rem 0.7rem; }
      .empty { margin: 0; color: #58708f; }
      .hint { margin: 0; color: #5d7592; font-size: 0.85rem; }
      .error { margin: 0; color: #8a2d24; font-size: 0.85rem; }
    `
  ]
})
export class OpenTasksListComponent {
  @Input() selectedTicketId = '';
  @Input() tasks: DashboardOpenTaskSummaryItemResponse[] = [];
  @Output() readonly completeClicked = new EventEmitter<string>();
  @Output() readonly createClicked = new EventEmitter<CreateTaskDraftPayload>();

  readonly form;
  submissionError = '';

  constructor(formBuilder: FormBuilder) {
    const defaultDue = new Date(Date.now() + 60 * 60 * 1000);
    this.form = formBuilder.nonNullable.group({
      description: ['', [Validators.required, Validators.maxLength(250)]],
      dueAt: [this.toDateTimeLocalValue(defaultDue), [Validators.required]]
    });
  }

  submitCreate(): void {
    this.submissionError = '';
    if (!this.selectedTicketId) {
      this.submissionError = 'Select a ticket first.';
      return;
    }

    if (this.form.invalid) {
      this.submissionError = 'Description and due date are required.';
      return;
    }

    const value = this.form.getRawValue();
    const description = value.description.trim();
    if (!description) {
      this.submissionError = 'Task description is required.';
      return;
    }

    const dueAtLocal = new Date(value.dueAt);
    if (Number.isNaN(dueAtLocal.getTime())) {
      this.submissionError = 'Due date is invalid.';
      return;
    }

    this.createClicked.emit({
      description,
      dueAtUtc: dueAtLocal.toISOString()
    });

    const nextDue = new Date(Date.now() + 60 * 60 * 1000);
    this.form.reset({ description: '', dueAt: this.toDateTimeLocalValue(nextDue) });
  }

  private toDateTimeLocalValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hour = String(date.getHours()).padStart(2, '0');
    const minute = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hour}:${minute}`;
  }
}
