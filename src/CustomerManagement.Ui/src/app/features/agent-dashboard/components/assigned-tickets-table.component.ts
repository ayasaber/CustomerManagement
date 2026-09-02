import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DashboardAssignedTicketItemResponse } from '../../../core/models/agent-dashboard.models';

@Component({
  selector: 'app-assigned-tickets-table',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="panel">
      <header>
        <h3>Assigned Tickets</h3>
        <span>{{ tickets.length }} items</span>
      </header>

      <div class="table-wrap" *ngIf="tickets.length; else emptyState">
        <table>
          <thead>
            <tr>
              <th>Ticket #</th>
              <th>Subject</th>
              <th>Customer</th>
              <th>Priority</th>
              <th>Status</th>
              <th>Escalated</th>
              <th>Updated</th>
            </tr>
          </thead>
          <tbody>
            <tr
              *ngFor="let ticket of tickets"
              [class.selected]="ticket.ticketId === selectedTicketId"
              (click)="onSelect(ticket.ticketId)">
              <td>{{ ticket.ticketNumber }}</td>
              <td>{{ ticket.subject }}</td>
              <td>{{ ticket.customerDisplayName }}</td>
              <td>{{ ticket.priority }}</td>
              <td>{{ ticket.status }}</td>
              <td>{{ ticket.isEscalated ? 'Yes' : 'No' }}</td>
              <td>{{ ticket.updatedAtUtc | date: 'yyyy-MM-dd HH:mm':'UTC' }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <ng-template #emptyState>
        <p class="empty">No assigned tickets found.</p>
      </ng-template>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d8e2ef; border-radius: 14px; background: #fff; padding: 0.8rem; }
      header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 0.6rem; }
      h3 { margin: 0; font-size: 1rem; }
      .table-wrap { overflow: auto; }
      table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
      th, td { text-align: left; border-bottom: 1px solid #eef3f8; padding: 0.45rem; white-space: nowrap; }
      tbody tr { cursor: pointer; }
      tbody tr.selected { background: #f0f7ff; }
      .empty { margin: 0; color: #58708f; }
    `
  ]
})
export class AssignedTicketsTableComponent {
  @Input() tickets: DashboardAssignedTicketItemResponse[] = [];
  @Input() selectedTicketId = '';
  @Output() readonly ticketSelected = new EventEmitter<string>();

  onSelect(ticketId: string): void {
    this.ticketSelected.emit(ticketId);
  }
}
