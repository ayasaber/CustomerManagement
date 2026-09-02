import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  AgentOption,
  TicketInternalNoteResponse
} from '../../../core/models/agent-dashboard.models';

export interface NoteDraftPayload {
  body: string;
  mentionedUserIds: string[];
  offerReassign: boolean;
  reassignToUserId: string | null;
}

@Component({
  selector: 'app-ticket-notes-panel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="panel">
      <h3>Internal Notes & Mentions</h3>

      <form [formGroup]="form" (ngSubmit)="submit()" class="compose">
        <textarea formControlName="body" rows="4" placeholder="Write an internal note"></textarea>

        <label class="field">
          Mention teammates
          <select formControlName="mentions" multiple size="4">
            <option *ngFor="let agent of agents" [value]="agent.id">{{ agent.displayName }}</option>
          </select>
        </label>

        <label class="checkbox">
          <input type="checkbox" formControlName="offerReassign" />
          Mention and offer handoff
        </label>

        <label class="field" *ngIf="form.controls.offerReassign.value">
          Reassign target
          <select formControlName="reassignToUserId">
            <option value="">Select target</option>
            <option *ngFor="let agent of agents" [value]="agent.id">{{ agent.displayName }}</option>
          </select>
        </label>

        <p class="error" *ngIf="submissionError">{{ submissionError }}</p>

        <button type="submit">Post note</button>
      </form>

      <ul *ngIf="notes.length; else emptyState" class="thread">
        <li *ngFor="let note of notes">
          <div class="head">
            <strong>{{ note.authorDisplayName }}</strong>
            <small>{{ note.createdAtUtc | date: 'yyyy-MM-dd HH:mm':'UTC' }}</small>
          </div>
          <p>{{ note.body }}</p>
          <p class="mentions" *ngIf="note.mentions.length">
            Mentions:
            <span *ngFor="let mention of note.mentions">@{{ mention.mentionedDisplayName }} </span>
          </p>
        </li>
      </ul>

      <ng-template #emptyState>
        <p class="empty">No internal notes yet.</p>
      </ng-template>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d8e2ef; border-radius: 14px; background: #fff; padding: 0.8rem; }
      h3 { margin: 0 0 0.6rem; }
      .compose { display: grid; gap: 0.45rem; margin-bottom: 0.75rem; }
      textarea, select { width: 100%; border: 1px solid #bfd2e8; border-radius: 8px; padding: 0.45rem 0.55rem; }
      .field { display: grid; gap: 0.25rem; color: #284968; font-size: 0.9rem; }
      .checkbox { display: flex; gap: 0.45rem; align-items: center; color: #284968; font-size: 0.9rem; }
      button { justify-self: start; border: 1px solid #86a3c5; border-radius: 8px; background: #f6fbff; color: #1f4f84; padding: 0.4rem 0.7rem; }
      .thread { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
      .thread li { border: 1px solid #ecf1f6; border-radius: 10px; padding: 0.5rem; }
      .head { display: flex; justify-content: space-between; }
      p { margin: 0.25rem 0 0; white-space: pre-wrap; }
      .mentions { color: #355b80; font-size: 0.9rem; }
      .empty { margin: 0; color: #58708f; }
      .error { margin: 0; color: #8a2d24; font-size: 0.85rem; }
    `
  ]
})
export class TicketNotesPanelComponent implements OnChanges {
  @Input() notes: TicketInternalNoteResponse[] = [];
  @Input() agents: AgentOption[] = [];
  @Input() insertText = '';
  @Input() insertToken = 0;

  @Output() readonly createNoteClicked = new EventEmitter<NoteDraftPayload>();

  readonly form;
  submissionError = '';

  constructor(formBuilder: FormBuilder) {
    this.form = formBuilder.nonNullable.group({
      body: ['', [Validators.required, Validators.maxLength(4000)]],
      mentions: [[] as string[]],
      offerReassign: false,
      reassignToUserId: ''
    });

    this.form.controls.offerReassign.valueChanges.subscribe((enabled) => {
      if (!enabled) {
        this.form.controls.reassignToUserId.setValue('');
        this.submissionError = '';
      }
    });

    this.form.controls.mentions.valueChanges.subscribe((mentions) => {
      const currentTarget = this.form.controls.reassignToUserId.value;
      if (currentTarget && !mentions.includes(currentTarget)) {
        this.form.controls.reassignToUserId.setValue('');
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['insertToken'] && this.insertText) {
      const current = this.form.controls.body.value;
      this.form.controls.body.setValue(current ? `${current}\n${this.insertText}` : this.insertText);
    }
  }

  submit(): void {
    this.submissionError = '';

    if (this.form.invalid) {
      this.submissionError = 'Note body is required.';
      return;
    }

    const value = this.form.getRawValue();
    const trimmedBody = value.body.trim();
    if (!trimmedBody) {
      this.submissionError = 'Note body is required.';
      return;
    }

    if (value.offerReassign && !value.reassignToUserId) {
      this.submissionError = 'Select a handoff target when offering handoff.';
      return;
    }

    if (value.offerReassign && !value.mentions.includes(value.reassignToUserId)) {
      this.submissionError = 'Handoff target must also be selected in Mention teammates.';
      return;
    }

    this.createNoteClicked.emit({
      body: trimmedBody,
      mentionedUserIds: value.mentions,
      offerReassign: value.offerReassign,
      reassignToUserId: value.offerReassign ? value.reassignToUserId : null
    });

    this.submissionError = '';
    this.form.reset({ body: '', mentions: [], offerReassign: false, reassignToUserId: '' });
  }
}
