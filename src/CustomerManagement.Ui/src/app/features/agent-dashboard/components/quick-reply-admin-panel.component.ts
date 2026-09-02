import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { QuickReplyResponse } from '../../../core/models/agent-dashboard.models';

@Component({
  selector: 'app-quick-reply-admin-panel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="panel" *ngIf="visible">
      <h3>Quick Replies Admin</h3>

      <form [formGroup]="createForm" (ngSubmit)="create()" class="grid-form">
        <input type="text" formControlName="title" placeholder="Title" />
        <input type="text" formControlName="tags" placeholder="Tags (comma separated)" />
        <textarea formControlName="body" rows="3" placeholder="Body"></textarea>
        <button type="submit" [disabled]="createForm.invalid">Add reply</button>
      </form>

      <ul>
        <li *ngFor="let reply of replies">
          <div>
            <strong>{{ reply.title }}</strong>
            <small>{{ reply.isActive ? 'Active' : 'Inactive' }} · {{ reply.updatedAtUtc | date:'yyyy-MM-dd HH:mm':'UTC' }}</small>
          </div>
          <button type="button" (click)="toggleActive.emit(reply)">
            {{ reply.isActive ? 'Deactivate' : 'Activate' }}
          </button>
        </li>
      </ul>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d8e2ef; border-radius: 14px; background: #fff; padding: 0.8rem; }
      h3 { margin: 0 0 0.6rem; }
      .grid-form { display: grid; gap: 0.45rem; margin-bottom: 0.7rem; }
      input, textarea { width: 100%; padding: 0.45rem 0.55rem; border: 1px solid #bfd2e8; border-radius: 8px; }
      ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
      li { border: 1px solid #ecf1f6; border-radius: 10px; padding: 0.5rem; display: flex; justify-content: space-between; gap: 0.6rem; }
      small { color: #5d7592; display: block; }
      button { border: 1px solid #86a3c5; border-radius: 8px; background: #f6fbff; color: #1f4f84; padding: 0.4rem 0.7rem; }
    `
  ]
})
export class QuickReplyAdminPanelComponent {
  @Input() replies: QuickReplyResponse[] = [];
  @Input() visible = false;
  @Output() readonly createClicked = new EventEmitter<{ title: string; body: string; tags: string[] }>();
  @Output() readonly toggleActive = new EventEmitter<QuickReplyResponse>();

  readonly createForm;

  constructor(formBuilder: FormBuilder) {
    this.createForm = formBuilder.nonNullable.group({
      title: ['', [Validators.required, Validators.maxLength(120)]],
      body: ['', [Validators.required, Validators.maxLength(2000)]],
      tags: ['']
    });
  }

  create(): void {
    if (this.createForm.invalid) {
      return;
    }

    const value = this.createForm.getRawValue();
    const tags = value.tags
      .split(',')
      .map((item) => item.trim())
      .filter((item) => item.length > 0);

    this.createClicked.emit({ title: value.title.trim(), body: value.body.trim(), tags });
    this.createForm.reset({ title: '', body: '', tags: '' });
  }
}
