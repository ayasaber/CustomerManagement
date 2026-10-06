import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { FaqEntryResponse } from '../../../core/models/faq.models';
import { FaqApiService } from '../../../core/services/faq-api.service';

@Component({
  selector: 'app-admin-faq-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="taxonomy-page">
      <h2>FAQ</h2>

      <p class="status error" *ngIf="error()">{{ error() }}</p>
      <p class="status success" *ngIf="success()">{{ success() }}</p>

      <div class="forms">
        <form [formGroup]="entryForm" (ngSubmit)="createEntry()" class="card">
          <h3>New FAQ Entry</h3>
          <label>
            Topic
            <input type="text" formControlName="topic" maxlength="150" />
          </label>
          <label>
            Question
            <textarea formControlName="question" rows="2" maxlength="500"></textarea>
          </label>
          <label>
            Answer
            <textarea formControlName="answer" rows="4" maxlength="4000"></textarea>
          </label>
          <label>
            Sort Order
            <input type="number" formControlName="sortOrder" min="0" max="1000" />
          </label>
          <button type="submit" [disabled]="entryForm.invalid || loading()">Create Entry</button>
        </form>
      </div>

      <div class="lists">
        <section class="card">
          <h3>Entries</h3>
          <ul>
            <li *ngFor="let entry of entries()">
              <strong>{{ entry.topic }}</strong> — {{ entry.question }}
              <small>({{ entry.isActive ? 'active' : 'retired' }})</small>
              <button type="button" (click)="toggleActive(entry)" [disabled]="loading()">
                {{ entry.isActive ? 'Retire' : 'Reactivate' }}
              </button>
            </li>
          </ul>
        </section>
      </div>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminFaqPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly entries = signal<FaqEntryResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  readonly entryForm = this.formBuilder.nonNullable.group({
    topic: ['', [Validators.required, Validators.maxLength(150)]],
    question: ['', [Validators.required, Validators.maxLength(500)]],
    answer: ['', [Validators.required, Validators.maxLength(4000)]],
    sortOrder: [0, [Validators.required, Validators.min(0), Validators.max(1000)]]
  });

  constructor(private readonly api: FaqApiService) {}

  ngOnInit(): void {
    this.refresh();
  }

  createEntry(): void {
    if (this.entryForm.invalid) {
      return;
    }

    const value = this.entryForm.getRawValue();
    this.beginRequest();
    this.api
      .createFaqEntry({
        topic: value.topic.trim(),
        question: value.question.trim(),
        answer: value.answer.trim(),
        sortOrder: value.sortOrder
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('FAQ entry created.');
          this.entryForm.reset({ topic: '', question: '', answer: '', sortOrder: 0 });
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Create FAQ entry failed: ${err.message}`)
      });
  }

  toggleActive(entry: FaqEntryResponse): void {
    this.beginRequest();
    this.api
      .updateFaqEntry(entry.id, {
        topic: entry.topic,
        question: entry.question,
        answer: entry.answer,
        sortOrder: entry.sortOrder,
        isActive: !entry.isActive,
        rowVersion: entry.rowVersion
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set(entry.isActive ? 'FAQ entry retired.' : 'FAQ entry reactivated.');
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Update FAQ entry failed: ${err.message}`)
      });
  }

  private refresh(): void {
    this.api.listFaqEntries().subscribe({
      next: (entries) => this.entries.set(entries),
      error: (err: Error) => this.error.set(`FAQ list failed: ${err.message}`)
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
