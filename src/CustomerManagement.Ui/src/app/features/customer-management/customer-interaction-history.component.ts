import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { CustomerInteractionHistoryItemResponse } from '../../core/models/customer-management.models';

@Component({
  selector: 'app-customer-interaction-history',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="panel">
      <h2>Interaction Timeline</h2>
      <p *ngIf="!items.length" class="muted">No interactions available.</p>
      <ol *ngIf="items.length" class="timeline">
        <li *ngFor="let item of items">
          <strong>{{ item.channel }} • {{ item.direction }}</strong>
          <p>{{ item.summary || 'No summary provided' }}</p>
          <small>{{ item.timestampUtc | date: 'medium' }}</small>
        </li>
      </ol>
    </section>
  `,
  styles: [
    ':host { display: block; }',
    '.panel { height: 100%; padding: 1rem; border: 1px solid #d7d2c8; border-radius: 12px; background: #fffdf8; }',
    'h2 { margin: 0 0 0.8rem; }',
    '.timeline { margin: 0; padding-left: 1.1rem; display: grid; gap: 0.75rem; }',
    '.muted { color: #6b675f; }',
    'p { margin: 0.25rem 0; }'
  ]
})
export class CustomerInteractionHistoryComponent {
  @Input() items: CustomerInteractionHistoryItemResponse[] = [];
}
