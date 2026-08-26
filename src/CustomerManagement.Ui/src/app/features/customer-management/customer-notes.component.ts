import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CustomerNoteResponse } from '../../core/models/customer-management.models';

@Component({
  selector: 'app-customer-notes',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="panel">
      <h2>Notes</h2>
      <form [formGroup]="form" (ngSubmit)="submit()" class="stack">
        <textarea rows="3" formControlName="body" placeholder="Add note"></textarea>
        <button type="submit" [disabled]="form.invalid">Add Note</button>
      </form>

      <ul class="list" *ngIf="notes.length">
        <li *ngFor="let note of notes">
          <p>{{ note.body }}</p>
          <small>{{ note.createdBy }} • {{ note.createdAtUtc | date: 'short' }}</small>
        </li>
      </ul>
    </section>
  `,
  styles: [
    ':host { display: block; }',
    '.panel { height: 100%; padding: 1rem; border: 1px solid #d7d2c8; border-radius: 12px; background: #fffdf8; }',
    'h2 { margin: 0 0 0.8rem; }',
    '.stack { display: grid; gap: 0.6rem; }',
    'textarea { padding: 0.5rem; border: 1px solid #c8c2b8; border-radius: 8px; }',
    'button { justify-self: start; padding: 0.5rem 1rem; border: 0; border-radius: 999px; background: #1d5c63; color: #fff; }',
    '.list { margin-top: 0.75rem; padding-left: 1rem; display: grid; gap: 0.5rem; }'
  ]
})
export class CustomerNotesComponent {
  private readonly formBuilder = inject(FormBuilder);

  @Input() notes: CustomerNoteResponse[] = [];
  @Output() noteAdded = new EventEmitter<string>();

  readonly form = this.formBuilder.nonNullable.group({
    body: ['', [Validators.required, Validators.maxLength(4000)]]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.noteAdded.emit(this.form.controls.body.value.trim());
    this.form.reset();
  }
}
