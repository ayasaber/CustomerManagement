import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { FaqEntryResponse } from '../../core/models/faq.models';
import {
  TicketAttachmentResponse,
  TicketCategoryResponse,
  TicketListItemResponse,
  TicketMessageResponse
} from '../../core/models/ticket-management.models';
import { FaqApiService } from '../../core/services/faq-api.service';
import { FeedbackApiService } from '../../core/services/feedback-api.service';
import { TicketManagementApiService } from '../../core/services/ticket-management-api.service';
import { TicketConversationPanelComponent } from '../tickets/components/ticket-conversation-panel.component';

type PortalSection = 'requests' | 'new-request' | 'faq' | 'feedback';

interface FaqTopicGroup {
  topic: string;
  entries: FaqEntryResponse[];
}

@Component({
  selector: 'app-customer-portal-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TicketConversationPanelComponent],
  template: `
    <section class="portal">
      <header class="portal-header">
        <h1>Customer Portal</h1>
        <nav class="app-nav">
          <button type="button" [class.active]="section() === 'requests'" (click)="selectSection('requests')">
            My Requests
          </button>
          <button type="button" [class.active]="section() === 'new-request'" (click)="selectSection('new-request')">
            New Request
          </button>
          <button type="button" [class.active]="section() === 'faq'" (click)="selectSection('faq')">FAQ</button>
          <button type="button" [class.active]="section() === 'feedback'" (click)="selectSection('feedback')">
            Feedback
          </button>
        </nav>
      </header>

      <p class="status error" *ngIf="error()">{{ error() }}</p>
      <p class="status success" *ngIf="success()">{{ success() }}</p>

      <section class="panel" *ngIf="section() === 'requests'">
        <div class="requests-layout">
          <ul class="request-list">
            <li *ngFor="let ticket of tickets()" [class.selected]="selectedTicket()?.id === ticket.id">
              <button type="button" (click)="selectTicket(ticket.id)">
                <strong>{{ ticket.subject }}</strong>
                <small>{{ ticket.categoryName }} · {{ ticket.status }}</small>
              </button>
            </li>
          </ul>

          <p class="empty" *ngIf="!tickets().length">You have not submitted any requests yet.</p>

          <div class="request-detail" *ngIf="selectedTicket() as ticket">
            <h3>{{ ticket.subject }}</h3>
            <p class="meta">{{ ticket.categoryName }} · {{ ticket.priorityName }} · {{ ticket.status }}</p>

            <app-ticket-conversation-panel
              [messages]="ticketMessages()"
              [quickReplies]="[]"
              [canCompose]="true"
              [sending]="conversationSending()"
              [loading]="conversationLoading()"
              (sendClicked)="postConversationMessage($event)"
              (reloadClicked)="loadConversation(ticket.id)"
            ></app-ticket-conversation-panel>

            <div class="attachments">
              <h4>Attachments</h4>
              <ul>
                <li *ngFor="let attachment of ticketAttachments()">{{ attachment.originalFileName }}</li>
              </ul>
              <p class="empty" *ngIf="!ticketAttachments().length">No attachments yet.</p>
              <input type="file" (change)="onDetailFileSelected($event)" />
              <button type="button" (click)="uploadDetailAttachment(ticket.id)" [disabled]="!detailFile || attachmentUploading()">
                Upload Attachment
              </button>
            </div>
          </div>
        </div>
      </section>

      <section class="panel" *ngIf="section() === 'new-request'">
        <form [formGroup]="createForm" (ngSubmit)="submitNewRequest()">
          <label>
            Category
            <select formControlName="categoryId">
              <option value="" disabled>Select a category</option>
              <option *ngFor="let category of categories()" [value]="category.id">{{ category.name }}</option>
            </select>
          </label>

          <label>
            Subject
            <input type="text" formControlName="subject" maxlength="200" />
          </label>

          <label>
            Description
            <textarea formControlName="description" rows="5" maxlength="4000"></textarea>
          </label>

          <label>
            Attachment (optional)
            <input type="file" (change)="onNewRequestFileSelected($event)" />
          </label>

          <button type="submit" [disabled]="createForm.invalid || submitting()">Submit Request</button>
        </form>
      </section>

      <section class="panel" *ngIf="section() === 'faq'">
        <div class="faq-topic" *ngFor="let group of faqGroups()">
          <h3>{{ group.topic }}</h3>
          <div class="faq-entry" *ngFor="let entry of group.entries">
            <p class="question">{{ entry.question }}</p>
            <p class="answer">{{ entry.answer }}</p>
          </div>
        </div>

        <p class="empty" *ngIf="!faqGroups().length">No FAQ entries are available yet.</p>
      </section>

      <section class="panel" *ngIf="section() === 'feedback'">
        <form [formGroup]="feedbackForm" (ngSubmit)="submitFeedback()">
          <label>
            Rating (1-5)
            <select formControlName="rating">
              <option [value]="1">1</option>
              <option [value]="2">2</option>
              <option [value]="3">3</option>
              <option [value]="4">4</option>
              <option [value]="5">5</option>
            </select>
          </label>

          <label>
            Comment (optional)
            <textarea formControlName="comment" rows="4" maxlength="2000"></textarea>
          </label>

          <button type="submit" [disabled]="feedbackForm.invalid || submittingFeedback()">Submit Feedback</button>
        </form>

        <p class="status success" *ngIf="feedbackSubmitted()">Thank you for your feedback.</p>
      </section>
    </section>
  `,
  styles: [
    `
      .portal {
        max-width: 960px;
        margin: 2rem auto;
        padding: 1.2rem;
        border-radius: var(--radius-lg);
        border: 1px solid var(--color-border);
        background: var(--color-surface);
        box-shadow: var(--shadow-card);
      }
      .portal-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.8rem; }
      .status.error { color: #8a2d24; }
      .status.success { color: #1f6f43; }
      .panel { margin-top: 1.2rem; }
      .requests-layout { display: grid; grid-template-columns: 260px 1fr; gap: 1rem; }
      .request-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.4rem; }
      .request-list li button {
        width: 100%;
        text-align: left;
        border: 1px solid var(--color-border);
        border-radius: var(--radius-md);
        background: #fbfdff;
        padding: 0.6rem;
      }
      .request-list li.selected button { border-color: var(--color-primary); background: #eaf2fb; }
      .request-list small { display: block; color: var(--color-text-muted); margin-top: 0.2rem; }
      .request-detail { display: grid; gap: 0.8rem; }
      .attachments { border-top: 1px solid #e4edf6; padding-top: 0.8rem; }
      form { display: grid; gap: 0.7rem; max-width: 520px; }
      label { font-weight: 600; color: #29445e; display: grid; gap: 0.3rem; }
      input, select, textarea {
        border: 1px solid #c1cfe0;
        border-radius: 8px;
        padding: 0.55rem 0.65rem;
        font: inherit;
      }
      button[type='submit'] {
        border: 1px solid var(--color-primary-dark);
        border-radius: var(--radius-md);
        background: var(--color-primary-gradient);
        color: #fff;
        padding: 0.55rem 0.9rem;
        justify-self: start;
      }
      .faq-topic { margin-bottom: 1.2rem; }
      .faq-entry { margin: 0.5rem 0; }
      .faq-entry .question { font-weight: 600; margin: 0; }
      .faq-entry .answer { margin: 0.2rem 0 0; color: #3c4d60; }
      .empty { color: #657a8f; }
    `
  ]
})
export class CustomerPortalPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly section = signal<PortalSection>('requests');

  readonly tickets = signal<TicketListItemResponse[]>([]);
  readonly categories = signal<TicketCategoryResponse[]>([]);
  readonly selectedTicket = signal<TicketListItemResponse | null>(null);
  readonly ticketMessages = signal<TicketMessageResponse[]>([]);
  readonly ticketAttachments = signal<TicketAttachmentResponse[]>([]);
  readonly conversationLoading = signal(false);
  readonly conversationSending = signal(false);
  readonly attachmentUploading = signal(false);

  readonly faqEntries = signal<FaqEntryResponse[]>([]);
  readonly faqGroups = computed<FaqTopicGroup[]>(() => {
    const grouped = new Map<string, FaqEntryResponse[]>();
    for (const entry of this.faqEntries()) {
      const bucket = grouped.get(entry.topic) ?? [];
      bucket.push(entry);
      grouped.set(entry.topic, bucket);
    }

    return Array.from(grouped.entries()).map(([topic, entries]) => ({
      topic,
      entries: entries.slice().sort((a, b) => a.sortOrder - b.sortOrder)
    }));
  });

  readonly submitting = signal(false);
  readonly submittingFeedback = signal(false);
  readonly feedbackSubmitted = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  detailFile: File | null = null;
  private newRequestFile: File | null = null;

  readonly createForm = this.formBuilder.nonNullable.group({
    categoryId: ['', [Validators.required]],
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]]
  });

  readonly feedbackForm = this.formBuilder.nonNullable.group({
    rating: [5, [Validators.required]],
    comment: ['', [Validators.maxLength(2000)]]
  });

  constructor(
    private readonly ticketApi: TicketManagementApiService,
    private readonly faqApi: FaqApiService,
    private readonly feedbackApi: FeedbackApiService
  ) {}

  ngOnInit(): void {
    this.loadTickets();
    this.loadCategories();
  }

  selectSection(section: PortalSection): void {
    this.section.set(section);
    this.error.set('');
    this.success.set('');

    if (section === 'faq' && !this.faqEntries().length) {
      this.loadFaq();
    }
  }

  loadTickets(): void {
    this.ticketApi.listTickets(1, 50).subscribe({
      next: (response) => this.tickets.set(response.items),
      error: (err: Error) => this.error.set(`Request list failed: ${err.message}`)
    });
  }

  loadCategories(): void {
    this.ticketApi.listCategories(true).subscribe({
      next: (categories) => this.categories.set(categories),
      error: (err: Error) => this.error.set(`Category list failed: ${err.message}`)
    });
  }

  loadFaq(): void {
    this.faqApi.listFaqEntries().subscribe({
      next: (entries) => this.faqEntries.set(entries),
      error: (err: Error) => this.error.set(`FAQ list failed: ${err.message}`)
    });
  }

  selectTicket(ticketId: string): void {
    const ticket = this.tickets().find((row) => row.id === ticketId) ?? null;
    this.selectedTicket.set(ticket);
    this.detailFile = null;

    if (!ticket) {
      this.ticketMessages.set([]);
      this.ticketAttachments.set([]);
      return;
    }

    this.loadConversation(ticket.id);
    this.loadAttachments(ticket.id);
  }

  loadConversation(ticketId: string): void {
    this.conversationLoading.set(true);
    this.ticketApi
      .listTicketMessages(ticketId, 1, 50)
      .pipe(finalize(() => this.conversationLoading.set(false)))
      .subscribe({
        next: (response) => this.ticketMessages.set(response.items),
        error: (err: Error) => this.error.set(`Conversation load failed: ${err.message}`)
      });
  }

  loadAttachments(ticketId: string): void {
    this.ticketApi.listTicketAttachments(ticketId).subscribe({
      next: (attachments) => this.ticketAttachments.set(attachments),
      error: (err: Error) => this.error.set(`Attachment list failed: ${err.message}`)
    });
  }

  postConversationMessage(body: string): void {
    const ticket = this.selectedTicket();
    if (!ticket) {
      return;
    }

    this.conversationSending.set(true);
    this.ticketApi
      .createTicketMessage({ ticketId: ticket.id, body })
      .pipe(finalize(() => this.conversationSending.set(false)))
      .subscribe({
        next: (response) => {
          this.ticketMessages.update((items) => items.concat(response.message));
          this.loadTickets();
        },
        error: (err: Error) => this.error.set(`Reply failed: ${err.message}`)
      });
  }

  onDetailFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.detailFile = input.files?.[0] ?? null;
  }

  uploadDetailAttachment(ticketId: string): void {
    if (!this.detailFile) {
      return;
    }

    this.attachmentUploading.set(true);
    this.ticketApi
      .uploadTicketAttachment(ticketId, this.detailFile)
      .pipe(finalize(() => this.attachmentUploading.set(false)))
      .subscribe({
        next: () => {
          this.detailFile = null;
          this.loadAttachments(ticketId);
        },
        error: (err: Error) => this.error.set(`Attachment upload failed: ${err.message}`)
      });
  }

  onNewRequestFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.newRequestFile = input.files?.[0] ?? null;
  }

  submitNewRequest(): void {
    if (this.createForm.invalid) {
      return;
    }

    const value = this.createForm.getRawValue();
    const file = this.newRequestFile;

    this.submitting.set(true);
    this.error.set('');
    this.success.set('');
    this.ticketApi
      .createTicket({
        categoryId: value.categoryId,
        subject: value.subject.trim(),
        description: value.description.trim()
      })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (ticket) => {
          const finish = () => {
            this.success.set(`Request "${ticket.subject}" submitted.`);
            this.createForm.reset({ categoryId: '', subject: '', description: '' });
            this.newRequestFile = null;
            this.loadTickets();
            this.section.set('requests');
            this.selectTicket(ticket.id);
          };

          if (file) {
            this.ticketApi.uploadTicketAttachment(ticket.id, file).subscribe({
              next: finish,
              error: (err: Error) => {
                this.error.set(`Request submitted, but attachment upload failed: ${err.message}`);
                finish();
              }
            });
          } else {
            finish();
          }
        },
        error: (err: Error) => this.error.set(`Request submission failed: ${err.message}`)
      });
  }

  submitFeedback(): void {
    if (this.feedbackForm.invalid) {
      return;
    }

    const value = this.feedbackForm.getRawValue();
    this.submittingFeedback.set(true);
    this.feedbackSubmitted.set(false);
    this.feedbackApi
      .submitFeedback({
        rating: Number(value.rating),
        comment: value.comment.trim() || undefined
      })
      .pipe(finalize(() => this.submittingFeedback.set(false)))
      .subscribe({
        next: () => {
          this.feedbackSubmitted.set(true);
          this.feedbackForm.reset({ rating: 5, comment: '' });
        },
        error: (err: Error) => this.error.set(`Feedback submission failed: ${err.message}`)
      });
  }
}

