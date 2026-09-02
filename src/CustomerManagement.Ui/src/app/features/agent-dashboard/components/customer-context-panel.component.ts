import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { DashboardCustomerContextResponse } from '../../../core/models/agent-dashboard.models';

@Component({
  selector: 'app-customer-context-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="panel">
      <h3>Customer Context</h3>

      <ng-container *ngIf="context; else emptyState">
        <div class="profile">
          <p><strong>{{ context.customerDisplayName }}</strong></p>
          <p>{{ context.company || 'No company' }}</p>
          <p>{{ context.primaryEmail || 'No email' }} · {{ context.primaryPhone || 'No phone' }}</p>
        </div>

        <h4>Recent Interactions</h4>
        <ul>
          <li *ngFor="let event of context.recentInteractions">
            <strong>{{ event.type }}</strong>
            <span>{{ event.summary || '-' }}</span>
            <small>{{ event.occurredAtUtc | date: 'yyyy-MM-dd HH:mm':'UTC' }}</small>
          </li>
        </ul>
      </ng-container>

      <ng-template #emptyState>
        <p class="empty">Select a ticket to load customer context.</p>
      </ng-template>
    </section>
  `,
  styles: [
    `
      .panel { border: 1px solid #d8e2ef; border-radius: 14px; background: #fff; padding: 0.8rem; }
      h3, h4 { margin: 0 0 0.6rem; }
      .profile { background: #f7fbff; border: 1px solid #e7f0fa; border-radius: 10px; padding: 0.6rem; margin-bottom: 0.7rem; }
      .profile p { margin: 0.1rem 0; }
      ul { margin: 0; padding: 0; list-style: none; display: grid; gap: 0.5rem; }
      li { border-left: 3px solid #98b5d8; padding: 0.35rem 0.5rem; background: #fcfdff; }
      li span, li small { display: block; color: #58708f; }
      .empty { margin: 0; color: #58708f; }
    `
  ]
})
export class CustomerContextPanelComponent {
  @Input() context: DashboardCustomerContextResponse | null = null;
}
