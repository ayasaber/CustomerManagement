import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { CustomerListItemResponse } from '../../core/models/customer-management.models';
import {
  TicketCategoryResponse,
  TicketHistoryItemResponse,
  TicketListItemResponse,
  TicketPriorityResponse
} from '../../core/models/ticket-management.models';
import { AuthService } from '../../core/services/auth.service';
import { CustomerManagementApiService } from '../../core/services/customer-management-api.service';
import { TicketManagementApiService } from '../../core/services/ticket-management-api.service';

@Component({
  selector: 'app-ticket-management-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './ticket-management-page.component.html',
  styleUrl: './ticket-management-page.component.scss'
})
export class TicketManagementPageComponent implements OnInit {
  private static readonly customerPageSize = 200;

  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);

  readonly tickets = signal<TicketListItemResponse[]>([]);
  readonly customerOptions = signal<CustomerListItemResponse[]>([]);
  readonly categories = signal<TicketCategoryResponse[]>([]);
  readonly priorities = signal<TicketPriorityResponse[]>([]);
  readonly selectedHistory = signal<TicketHistoryItemResponse[]>([]);
  readonly selectedHistoryTicket = signal<TicketListItemResponse | null>(null);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly canAssignOthers = computed(() => this.authService.hasRole('admin'));
  readonly canSelfAssign = computed(() => this.authService.hasRole('admin') || this.authService.hasRole('agent'));
  readonly isCustomer = computed(() => this.authService.hasRole('customer'));
  readonly statusOptions = [
    { value: 'in_progress', label: 'In Progress' },
    { value: 'waiting_on_customer', label: 'Waiting On Customer' },
    { value: 'resolved', label: 'Resolved' },
    { value: 'closed', label: 'Closed' }
  ] as const;

  readonly createForm = this.formBuilder.nonNullable.group({
    customerUserId: [''],
    categoryId: ['', [Validators.required]],
    priorityId: ['', [Validators.required]],
    subject: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(4000)]]
  });

  readonly statusForm = this.formBuilder.nonNullable.group({
    ticketId: ['', [Validators.required]],
    targetStatus: ['in_progress', [Validators.required]]
  });

  readonly assignForm = this.formBuilder.nonNullable.group({
    ticketId: ['', [Validators.required]],
    assigneeUserId: ['', [Validators.required]]
  });

  constructor(
    private readonly api: TicketManagementApiService,
    private readonly customerApi: CustomerManagementApiService
  ) {}

  ngOnInit(): void {
    this.loadCustomerOptions();
    this.refreshTaxonomy();
    this.loadTickets();
  }

  createTicket(): void {
    if (this.createForm.invalid) {
      return;
    }

    const value = this.createForm.getRawValue();

    if (!this.isCustomer() && !value.customerUserId.trim()) {
      this.error.set('Select a customer before creating a ticket.');
      return;
    }

    this.beginRequest();
    const customerUserId = value.customerUserId.trim();
    this.api
      .createTicket({
        customerUserId: this.isCustomer() ? undefined : customerUserId,
        categoryId: value.categoryId,
        priorityId: value.priorityId,
        subject: value.subject.trim(),
        description: value.description.trim()
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (ticket) => {
          this.success.set(`Ticket ${ticket.id} created.`);
          this.createForm.controls.customerUserId.reset('');
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

  assignTicket(): void {
    if (this.assignForm.invalid) {
      return;
    }

    const value = this.assignForm.getRawValue();
    const ticket = this.tickets().find((row) => row.id === value.ticketId.trim());
    if (!ticket) {
      this.error.set('Select a ticket from the list first.');
      return;
    }

    this.beginRequest();
    this.api
      .assignTicket(ticket.id, {
        assigneeUserId: value.assigneeUserId.trim(),
        rowVersion: ticket.rowVersion
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (updated) => {
          this.success.set(`Ticket ${updated.id} assigned successfully.`);
          this.assignForm.controls.assigneeUserId.reset('');
          this.loadTickets();
        },
        error: (err: Error) => this.error.set(`Assign ticket failed: ${err.message}`)
      });
  }

  assignSelf(): void {
    const ticketId = this.statusForm.controls.ticketId.value.trim();
    const ticket = this.tickets().find((row) => row.id === ticketId);
    if (!ticket) {
      this.error.set('Select a ticket from the list first.');
      return;
    }

    this.beginRequest();
    this.api
      .assignSelf(ticket.id, {
        rowVersion: ticket.rowVersion
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (updated) => {
          this.success.set(`Ticket ${updated.id} self-assigned successfully.`);
          this.loadTickets();
        },
        error: (err: Error) => this.error.set(`Self-assign failed: ${err.message}`)
      });
  }

  loadHistory(ticketId: string): void {
    const selectedTicket = this.tickets().find((row) => row.id === ticketId) ?? null;
    this.selectedHistoryTicket.set(selectedTicket);

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

          this.onSelectedTicketChanged();
        },
        error: (err: Error) => this.error.set(`Ticket list failed: ${err.message}`)
      });
  }

  onSelectedTicketChanged(): void {
    const options = this.getAvailableTargetStatuses();
    const selectedTarget = this.statusForm.controls.targetStatus.value;

    if (!options.some((option) => option.value === selectedTarget)) {
      this.statusForm.controls.targetStatus.setValue(options[0]?.value ?? 'in_progress');
    }
  }

  getAvailableTargetStatuses(): ReadonlyArray<{ value: string; label: string }> {
    const ticketId = this.statusForm.controls.ticketId.value.trim();
    if (!ticketId) {
      return this.statusOptions;
    }

    const selectedTicket = this.tickets().find((ticket) => ticket.id === ticketId);
    if (!selectedTicket) {
      return this.statusOptions;
    }

    return this.statusOptions.filter((option) => this.canTransition(selectedTicket.status, option.value));
  }

  getHistoryFieldLabel(fieldName: string): string {
    const normalized = (fieldName || '').trim();
    if (!normalized) {
      return 'Details';
    }

    const fieldLabels: Record<string, string> = {
      assignedToUserId: 'Assigned Agent',
      status: 'Status',
      categoryId: 'Category',
      priorityId: 'Priority',
      subject: 'Subject',
      description: 'Description',
      isEscalated: 'Escalation'
    };

    if (fieldLabels[normalized]) {
      return fieldLabels[normalized];
    }

    return normalized
      .replace(/[_\.]+/g, ' ')
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/\s+/g, ' ')
      .trim()
      .replace(/^./, (first) => first.toUpperCase());
  }

  getHistoryValueDisplay(item: TicketHistoryItemResponse, value: string | null): string {
    if (!value) {
      return '-';
    }

    if (item.fieldName === 'assignedToUserId') {
      return this.getAgentDisplay(value);
    }

    return value;
  }

  getHistoryActorDisplay(item: TicketHistoryItemResponse): string {
    return item.actorEmail || item.actorUserId || 'System';
  }

  private getAgentDisplay(agentUserId: string): string {
    const trimmed = agentUserId.trim();
    if (!trimmed) {
      return '-';
    }

    const selectedTicket = this.selectedHistoryTicket();
    if (
      selectedTicket?.assignedToUserId &&
      selectedTicket.assignedToUserId.toLowerCase() === trimmed.toLowerCase() &&
      selectedTicket.assignedToUserEmail
    ) {
      return selectedTicket.assignedToUserEmail;
    }

    const matchedTicket = this.tickets().find(
      (ticket) =>
        !!ticket.assignedToUserId &&
        ticket.assignedToUserId.toLowerCase() === trimmed.toLowerCase() &&
        !!ticket.assignedToUserEmail
    );

    if (matchedTicket?.assignedToUserEmail) {
      return matchedTicket.assignedToUserEmail;
    }

    return `Agent (${trimmed})`;
  }

  private canTransition(currentStatus: string, targetStatus: string): boolean {
    const current = currentStatus.trim().toLowerCase();
    const target = targetStatus.trim().toLowerCase();

    if (!current || !target) {
      return false;
    }

    if (current === target) {
      return true;
    }

    switch (current) {
      case 'new':
        return target === 'in_progress' || target === 'waiting_on_customer' || target === 'resolved' || target === 'closed';
      case 'in_progress':
        return target === 'waiting_on_customer' || target === 'resolved' || target === 'closed';
      case 'waiting_on_customer':
        return target === 'in_progress' || target === 'resolved' || target === 'closed';
      case 'resolved':
        return target === 'closed';
      case 'closed':
        return false;
      default:
        return false;
    }
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

  private loadCustomerOptions(): void {
    if (this.isCustomer()) {
      return;
    }

    this.loadCustomerOptionsPage(1, []);
  }

  private loadCustomerOptionsPage(page: number, accumulated: CustomerListItemResponse[]): void {
    this.customerApi.listCustomers(page, TicketManagementPageComponent.customerPageSize).subscribe({
      next: (response) => {
        const merged = accumulated.concat(response.items);
        const totalPages = Math.ceil(response.totalCount / response.pageSize);

        if (page < totalPages) {
          this.loadCustomerOptionsPage(page + 1, merged);
          return;
        }

        const uniqueByUserId = new Map<string, CustomerListItemResponse>();
        for (const customer of merged) {
          if (!customer.applicationUserId) {
            continue;
          }

          const key = customer.applicationUserId.toLowerCase();
          if (!uniqueByUserId.has(key)) {
            uniqueByUserId.set(key, customer);
          }
        }

        const options = Array.from(uniqueByUserId.values()).sort((left, right) => left.name.localeCompare(right.name));
        this.customerOptions.set(options);

        const selectedCustomerUserId = this.createForm.controls.customerUserId.value.trim();
        if (
          selectedCustomerUserId &&
          !options.some(
            (customer) =>
              !!customer.applicationUserId &&
              customer.applicationUserId.toLowerCase() === selectedCustomerUserId.toLowerCase()
          )
        ) {
          this.createForm.controls.customerUserId.setValue('');
        }
      },
      error: (err: Error) => this.error.set(`Customer list failed: ${err.message}`)
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
