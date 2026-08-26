import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  TicketCategoryResponse,
  TicketHistoryItemResponse,
  TicketListItemResponse,
  TicketPriorityResponse
} from '../../core/models/ticket-management.models';
import { TicketManagementApiService } from '../../core/services/ticket-management-api.service';

@Component({
  selector: 'app-ticket-management-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './ticket-management-page.component.html',
  styleUrl: './ticket-management-page.component.scss'
})
export class TicketManagementPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly tickets = signal<TicketListItemResponse[]>([]);
  readonly categories = signal<TicketCategoryResponse[]>([]);
  readonly priorities = signal<TicketPriorityResponse[]>([]);
  readonly selectedHistory = signal<TicketHistoryItemResponse[]>([]);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  readonly createForm = this.formBuilder.nonNullable.group({
    customerId: ['', [Validators.required]],
    categoryId: ['', [Validators.required]],
    priorityId: ['', [Validators.required]],
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]]
  });

  readonly statusForm = this.formBuilder.nonNullable.group({
    ticketId: ['', [Validators.required]],
    targetStatus: ['in_progress', [Validators.required]]
  });

  constructor(
    private readonly api: TicketManagementApiService
  ) {}

  ngOnInit(): void {
    this.refreshTaxonomy();
    this.loadTickets();
  }

  createTicket(): void {
    if (this.createForm.invalid) {
      return;
    }

    const value = this.createForm.getRawValue();

    this.beginRequest();
    this.api
      .createTicket({
        customerId: value.customerId.trim(),
        categoryId: value.categoryId,
        priorityId: value.priorityId,
        subject: value.subject.trim(),
        description: value.description.trim()
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (ticket) => {
          this.success.set(`Ticket ${ticket.id} created.`);
          this.createForm.controls.customerId.reset('');
          this.createForm.controls.subject.reset('');
          this.createForm.controls.description.reset('');
          this.loadTickets();
        },
        error: (err: Error) => this.error.set(`Create ticket failed: ${err.message}`)
      });
  }

  updateStatus(): void {
    if (this.statusForm.invalid) {
      return;
    }

    const value = this.statusForm.getRawValue();
    const ticket = this.tickets().find((row) => row.id === value.ticketId.trim());
    if (!ticket) {
      this.error.set('Select a ticket from the list first.');
      return;
    }

    this.beginRequest();
    this.api
      .updateStatus(ticket.id, {
        targetStatus: value.targetStatus,
        rowVersion: ticket.rowVersion
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (updated) => {
          this.success.set(`Ticket ${updated.id} status updated to ${updated.status}.`);
          this.loadTickets();
        },
        error: (err: Error) => this.error.set(`Status update failed: ${err.message}`)
      });
  }

  loadHistory(ticketId: string): void {
    this.beginRequest(false);
    this.api
      .getHistory(ticketId)
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (history) => this.selectedHistory.set(history.items),
        error: (err: Error) => this.error.set(`History load failed: ${err.message}`)
      });
  }

  loadTickets(): void {
    this.beginRequest(false);
    this.api
      .listTickets(1, 50)
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (response) => {
          this.tickets.set(response.items);

          const selectedTicketId = this.statusForm.controls.ticketId.value.trim();
          if (selectedTicketId && !response.items.some((row) => row.id === selectedTicketId)) {
            this.statusForm.controls.ticketId.setValue('');
          }
        },
        error: (err: Error) => this.error.set(`Ticket list failed: ${err.message}`)
      });
  }

  selectTicketForStatus(ticketId: string): void {
    this.statusForm.controls.ticketId.setValue(ticketId);
    this.success.set(`Ticket ${ticketId} selected for status transition.`);
  }

  private refreshTaxonomy(): void {
    this.api.listCategories(true).subscribe({
      next: (categories) => this.categories.set(categories),
      error: (err: Error) => this.error.set(`Category load failed: ${err.message}`)
    });

    this.api.listPriorities(true).subscribe({
      next: (priorities) => this.priorities.set(priorities),
      error: (err: Error) => this.error.set(`Priority load failed: ${err.message}`)
    });
  }

  private beginRequest(clearMessages = true): void {
    this.loading.set(true);
    if (clearMessages) {
      this.error.set('');
      this.success.set('');
    }
  }

  private finishRequest(): void {
    this.loading.set(false);
  }
}
