import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TicketHandoffRequestResponse } from '../../../core/models/agent-dashboard.models';

@Component({
  selector: 'app-handoff-inbox-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="panel">
      <header>
        <h3>My Handoff Requests</h3>
        <span>{{ requests.length }} items</span>
      </header>

      <ul *ngIf="requests.length; else emptyState">
        <li *ngFor="let request of requests">
          <div>
            <strong>{{ request.status }}</strong>
            <small>{{ request.requestedAtUtc | date: 'yyyy-MM-dd HH:mm':'UTC' }}</small>
            <small>Ticket: {{ request.ticketId }}</small>
          </div>
          <div class="actions" *ngIf="request.status.toLowerCase() === 'pending'">
            <button type="button" (click)="acceptClicked.emit(request)">Accept</button>
            <button type="button" (click)="rejectClicked.emit(request)">Reject</button>
          </div>
        </li>
      </ul>

      <ng-template #emptyState>
        <p class="empty">No handoff requests.</p>
      </ng-template>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d8e2ef; border-radius: 14px; background: #fff; padding: 0.8rem; }
      header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 0.6rem; }
      h3 { margin: 0; font-size: 1rem; }
      ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.45rem; }
      li { border: 1px solid #ecf1f6; border-radius: 10px; padding: 0.5rem; display: flex; justify-content: space-between; gap: 0.6rem; }
      small { color: #5d7592; display: block; }
      .actions { display: flex; gap: 0.4rem; }
      button { border: 1px solid #86a3c5; border-radius: 8px; background: #f6fbff; color: #1f4f84; padding: 0.35rem 0.65rem; }
      .empty { margin: 0; color: #58708f; }
    `
  ]
})
export class HandoffInboxPanelComponent {
  @Input() requests: TicketHandoffRequestResponse[] = [];
  @Output() readonly acceptClicked = new EventEmitter<TicketHandoffRequestResponse>();
  @Output() readonly rejectClicked = new EventEmitter<TicketHandoffRequestResponse>();
}
