import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { CustomerContactDetailResponse } from '../../core/models/customer-management.models';

@Component({
  selector: 'app-customer-contact-details',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="panel">
      <h2>Contact Details</h2>
      <p *ngIf="!contactDetails.length" class="muted">No contact details found.</p>
      <ul *ngIf="contactDetails.length" class="list">
        <li *ngFor="let contact of contactDetails">
          <strong>{{ contact.label || 'contact' }}</strong>
          <span>{{ contact.value }}</span>
          <small>channel {{ contact.channel }}{{ contact.isPrimary ? ' • primary' : '' }}</small>
        </li>
      </ul>
    </section>
  `,
  styles: [
    ':host { display: block; }',
    '.panel { height: 100%; padding: 1rem; border: 1px solid #d7d2c8; border-radius: 12px; background: #fffdf8; }',
    'h2 { margin: 0 0 0.8rem; }',
    '.list { margin: 0; padding-left: 1rem; display: grid; gap: 0.5rem; }',
    '.muted { color: #6b675f; }'
  ]
})
export class CustomerContactDetailsComponent {
  @Input() contactDetails: CustomerContactDetailResponse[] = [];
}
