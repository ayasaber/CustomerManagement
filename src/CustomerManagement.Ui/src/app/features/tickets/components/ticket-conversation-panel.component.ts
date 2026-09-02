import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, Input, Output, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { QuickReplyResponse } from '../../../core/models/agent-dashboard.models';
import { TicketMessageResponse } from '../../../core/models/ticket-management.models';

@Component({
  selector: 'app-ticket-conversation-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="panel conversation-panel">
      <div class="panel-head">
        <div>
          <h3>Customer-visible Conversation</h3>
          <p class="scope">Visible to customer</p>
        </div>
        <button type="button" class="ghost" (click)="reloadClicked.emit()" [disabled]="loading">Refresh</button>
      </div>

      <ul class="thread" *ngIf="messages.length; else emptyState">
        <li *ngFor="let message of messages" class="entry">
          <div class="avatar" [attr.data-sender]="message.senderType">{{ initials(message.senderDisplayName) }}</div>
          <div class="content">
            <div class="meta">
              <strong>{{ message.senderDisplayName }}</strong>
              <small>{{ formatStamp(message.createdAtUtc) }}</small>
            </div>
            <p>{{ message.body }}</p>
          </div>
        </li>
      </ul>

      <ng-template #emptyState>
        <p class="empty">No conversation messages yet.</p>
      </ng-template>

      <div class="compose" *ngIf="canCompose">
        <label for="ticket-conversation-body">Reply</label>
        <textarea
          #composeArea
          id="ticket-conversation-body"
          rows="4"
          [(ngModel)]="draft"
          placeholder="Write a customer-visible reply"></textarea>

        <div class="compose-actions">
          <div class="helper-wrap" *ngIf="quickReplies.length">
            <button type="button" class="secondary" (click)="toggleHelper()" [disabled]="sending">
              Quick replies
            </button>

            <div class="helper-popup" *ngIf="helperOpen()">
              <input
                type="search"
                [ngModel]="search()"
                (ngModelChange)="search.set($event)"
                placeholder="Search quick replies" />

              <ul>
                <li *ngFor="let reply of filteredQuickReplies()">
                  <button type="button" (click)="insertQuickReply(reply.body)">
                    <strong>{{ reply.title }}</strong>
                    <small>{{ preview(reply.body) }}</small>
                  </button>
                </li>
              </ul>
            </div>
          </div>

          <button type="button" (click)="submit()" [disabled]="sending">Send</button>
        </div>

        <p class="error" *ngIf="errorText">{{ errorText }}</p>
      </div>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d7dfe8; border-radius: 12px; padding: 1rem; background: #fff; }
      .panel-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 0.75rem; }
      h3 { margin: 0; }
      .scope { margin: 0.2rem 0 0; color: #526579; font-size: 0.85rem; }
      .ghost { border: 1px solid #afc4da; background: #f4f9ff; color: #1b4e79; border-radius: 8px; padding: 0.4rem 0.7rem; }

      .thread { list-style: none; margin: 0.8rem 0 0; padding: 0; display: grid; gap: 0.7rem; }
      .entry { display: grid; grid-template-columns: 42px 1fr; gap: 0.7rem; align-items: start; }
      .avatar {
        width: 42px;
        height: 42px;
        border-radius: 999px;
        background: #d6e6f8;
        color: #1f4b75;
        display: grid;
        place-items: center;
        font-weight: 700;
      }
      .avatar[data-sender='customer'] { background: #fbe7cd; color: #7f4b16; }
      .content { padding-top: 0.1rem; }
      .meta { display: flex; gap: 0.6rem; align-items: baseline; flex-wrap: wrap; }
      .meta small { color: #607588; }
      p { margin: 0.25rem 0 0; white-space: pre-wrap; color: #1a2d3f; }
      .empty { margin: 0.8rem 0 0; color: #657a8f; }

      .compose { margin-top: 0.9rem; display: grid; gap: 0.45rem; }
      label { font-weight: 600; color: #29445e; }
      textarea, input[type='search'] {
        width: 100%;
        border: 1px solid #c1cfe0;
        border-radius: 8px;
        padding: 0.55rem 0.65rem;
      }

      .compose-actions { display: flex; justify-content: space-between; align-items: flex-start; gap: 0.6rem; position: relative; }
      .compose-actions button { border: 1px solid #113a56; border-radius: 8px; background: #113a56; color: #fff; padding: 0.45rem 0.75rem; }
      .compose-actions .secondary { border-color: #86a3c5; background: #f6fbff; color: #1f4f84; }

      .helper-wrap { position: relative; }
      .helper-popup {
        position: absolute;
        top: calc(100% + 0.4rem);
        left: 0;
        width: min(420px, calc(100vw - 4rem));
        border: 1px solid #c7d7e8;
        border-radius: 10px;
        background: #fff;
        box-shadow: 0 10px 28px rgba(31, 60, 91, 0.16);
        padding: 0.55rem;
        z-index: 8;
      }
      .helper-popup ul { list-style: none; margin: 0.55rem 0 0; padding: 0; max-height: 220px; overflow: auto; display: grid; gap: 0.35rem; }
      .helper-popup li button {
        width: 100%;
        text-align: left;
        border: 1px solid #e4edf6;
        border-radius: 8px;
        background: #fbfdff;
        color: #17324a;
        padding: 0.5rem;
      }
      .helper-popup li button small { display: block; margin-top: 0.2rem; color: #59708a; }

      .error { margin: 0; color: #8a2d24; font-size: 0.85rem; }
    `
  ]
})
export class TicketConversationPanelComponent {
  @Input() messages: TicketMessageResponse[] = [];
  @Input() quickReplies: QuickReplyResponse[] = [];
  @Input() canCompose = false;
  @Input() sending = false;
  @Input() loading = false;

  @Output() readonly sendClicked = new EventEmitter<string>();
  @Output() readonly reloadClicked = new EventEmitter<void>();

  @ViewChild('composeArea') composeArea?: ElementRef<HTMLTextAreaElement>;

  readonly search = signal('');
  readonly helperOpen = signal(false);

  draft = '';
  errorText = '';

  toggleHelper(): void {
    this.helperOpen.update((open) => !open);
  }

  filteredQuickReplies(): QuickReplyResponse[] {
    const term = this.search().trim().toLowerCase();
    if (!term) {
      return this.quickReplies;
    }

    return this.quickReplies.filter((reply) => {
      const title = reply.title.toLowerCase();
      const body = reply.body.toLowerCase();
      const tags = reply.tags.join(',').toLowerCase();
      return title.includes(term) || body.includes(term) || tags.includes(term);
    });
  }

  insertQuickReply(text: string): void {
    const value = (text || '').trim();
    if (!value) {
      return;
    }

    const element = this.composeArea?.nativeElement;
    if (!element) {
      this.draft = this.draft ? `${this.draft}\n${value}` : value;
      this.helperOpen.set(false);
      return;
    }

    const start = element.selectionStart ?? this.draft.length;
    const end = element.selectionEnd ?? start;
    const original = this.draft;

    this.draft = `${original.slice(0, start)}${value}${original.slice(end)}`;
    this.helperOpen.set(false);

    queueMicrotask(() => {
      element.focus();
      const cursor = start + value.length;
      element.setSelectionRange(cursor, cursor);
    });
  }

  submit(): void {
    this.errorText = '';
    const body = this.draft.trim();

    if (!body) {
      this.errorText = 'Message body is required.';
      return;
    }

    if (body.length > 4000) {
      this.errorText = 'Message body must be 4000 characters or fewer.';
      return;
    }

    this.sendClicked.emit(body);
    this.draft = '';
    this.search.set('');
    this.helperOpen.set(false);
  }

  initials(name: string): string {
    const parts = (name || '').trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return '??';
    }

    if (parts.length === 1) {
      return parts[0].slice(0, 2).toUpperCase();
    }

    return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  }

  preview(text: string): string {
    const normalized = (text || '').replace(/\s+/g, ' ').trim();
    if (normalized.length <= 96) {
      return normalized;
    }

    return `${normalized.slice(0, 93)}...`;
  }

  formatStamp(value: string): string {
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return value;
    }

    return parsed.toLocaleString(undefined, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
}
