import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CustomerAttachmentResponse } from '../../core/models/customer-management.models';

@Component({
  selector: 'app-customer-attachments',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="panel">
      <h2>Attachments</h2>
      <input type="file" (change)="onFileChange($event)" />
      <p *ngIf="!attachments.length" class="muted">No attachments uploaded yet.</p>

      <ul *ngIf="attachments.length" class="list">
        <li *ngFor="let item of attachments">
          <span>{{ item.originalFileName }} ({{ item.sizeBytes }} bytes)</span>
          <button
            type="button"
            class="download-btn"
            [disabled]="!customerId"
            (click)="requestDownload(item)">
            Download
          </button>
        </li>
      </ul>
    </section>
  `,
  styles: [
    ':host { display: block; }',
    '.panel { height: 100%; padding: 1rem; border: 1px solid #d7d2c8; border-radius: 12px; background: #fffdf8; }',
    '.list { margin-top: 0.8rem; padding-left: 1rem; display: grid; gap: 0.45rem; }',
    'li { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }',
    '.download-btn { border: 1px solid #9ec2e8; border-radius: 999px; background: #e8f3ff; color: #0b4f91; font-weight: 700; padding: 0.35rem 0.8rem; cursor: pointer; }',
    '.download-btn[disabled] { opacity: 0.5; cursor: not-allowed; }',
    '.muted { margin: 0.5rem 0 0; color: #6b675f; }'
  ]
})
export class CustomerAttachmentsComponent {
  @Input() attachments: CustomerAttachmentResponse[] = [];
  @Input() customerId = '';
  @Output() fileSelected = new EventEmitter<File>();
  @Output() downloadRequested = new EventEmitter<CustomerAttachmentResponse>();

  onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) {
      return;
    }

    this.fileSelected.emit(input.files[0]);
    input.value = '';
  }

  requestDownload(attachment: CustomerAttachmentResponse): void {
    this.downloadRequested.emit(attachment);
  }
}
