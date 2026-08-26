import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CreateCustomerProfileRequest,
  CustomerAttachmentResponse,
  CustomerContactDetailResponse,
  CustomerInteractionHistoryItemResponse,
  CustomerNoteResponse,
  CustomerProfileResponse,
  UpdateCustomerProfileRequest
} from '../../core/models/customer-management.models';
import { AgentContextService } from '../../core/services/agent-context.service';
import { CustomerManagementApiService } from '../../core/services/customer-management-api.service';
import { CustomerAttachmentsComponent } from './customer-attachments.component';
import { CustomerContactDetailsComponent } from './customer-contact-details.component';
import { CustomerInteractionHistoryComponent } from './customer-interaction-history.component';
import { CustomerNotesComponent } from './customer-notes.component';
import { CustomerProfileFormComponent } from './customer-profile-form.component';

@Component({
  selector: 'app-customer-management-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CustomerProfileFormComponent,
    CustomerContactDetailsComponent,
    CustomerInteractionHistoryComponent,
    CustomerNotesComponent,
    CustomerAttachmentsComponent
  ],
  templateUrl: './customer-management-page.component.html',
  styleUrl: './customer-management-page.component.scss'
})
export class CustomerManagementPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private activeRequests = 0;

  readonly profile = signal<CustomerProfileResponse | null>(null);
  readonly contactDetails = signal<CustomerContactDetailResponse[]>([]);
  readonly interactionItems = signal<CustomerInteractionHistoryItemResponse[]>([]);
  readonly notes = signal<CustomerNoteResponse[]>([]);
  readonly attachments = signal<CustomerAttachmentResponse[]>([]);

  readonly error = signal('');
  readonly success = signal('');
  readonly loading = signal(false);

  readonly effectiveRole = computed(() => this.agentContext.role());

  readonly lookupForm = this.formBuilder.nonNullable.group({
    customerId: ['', [Validators.required]]
  });

  constructor(
    private readonly api: CustomerManagementApiService,
    private readonly agentContext: AgentContextService
  ) {}

  setRole(role: string): void {
    this.agentContext.setRole(role);
    this.setSuccess(`Role switched to ${role}.`);
  }

  loadCustomer(): void {
    if (this.lookupForm.invalid) {
      return;
    }

    const customerId = this.lookupForm.controls.customerId.value.trim();
    if (!customerId) {
      this.setError('Enter a customer id first.');
      return;
    }

    this.fetchAll(customerId);
  }

  createProfile(request: CreateCustomerProfileRequest): void {
    this.beginRequest();
    this.api
      .createProfile(request)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.lookupForm.patchValue({ customerId: profile.id });
          this.setSuccess('Profile created successfully.');
          this.fetchAll(profile.id);
        },
        error: (err: Error) => this.setError(`Profile create failed: ${err.message}`)
      });
  }

  updateProfile(request: UpdateCustomerProfileRequest): void {
    const customerId = this.lookupForm.controls.customerId.value.trim();
    if (!customerId) {
      this.setError('Enter customer id before updating.');
      return;
    }

    this.beginRequest();
    this.api
      .updateProfile(customerId, request)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.setSuccess('Profile updated successfully.');
          this.fetchAll(customerId);
        },
        error: (err: Error) => this.setError(`Profile update failed: ${err.message}`)
      });
  }

  addNote(body: string): void {
    const customerId = this.lookupForm.controls.customerId.value.trim();
    if (!customerId) {
      this.setError('Enter customer id before adding notes.');
      return;
    }

    this.beginRequest();
    this.api
      .addNote(customerId, { body })
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: () => {
          this.setSuccess('Note added.');
          this.refreshNotes(customerId);
        },
        error: (err: Error) => this.setError(`Add note failed: ${err.message}`)
      });
  }

  uploadAttachment(file: File): void {
    const customerId = this.lookupForm.controls.customerId.value.trim();
    if (!customerId) {
      this.setError('Enter customer id before uploading attachments.');
      return;
    }

    this.beginRequest();
    this.api
      .uploadAttachment(customerId, file)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: () => {
          this.setSuccess('Attachment uploaded.');
          this.refreshAttachments(customerId);
        },
        error: (err: Error) => this.setError(`Attachment upload failed: ${err.message}`)
      });
  }

  downloadAttachment(attachment: CustomerAttachmentResponse): void {
    const customerId = this.lookupForm.controls.customerId.value.trim();
    if (!customerId) {
      this.setError('Enter customer id before downloading attachments.');
      return;
    }

    this.beginRequest(false);
    this.api
      .downloadAttachment(customerId, attachment.id)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (blob) => {
          const objectUrl = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = objectUrl;
          link.download = attachment.originalFileName || `attachment-${attachment.id}`;
          link.click();
          URL.revokeObjectURL(objectUrl);
          this.setSuccess(`Downloading ${link.download}.`);
        },
        error: (err: Error) => this.setError(`Attachment download failed: ${err.message}`)
      });
  }

  private fetchAll(customerId: string): void {
    this.error.set('');
    this.success.set('');

    this.beginRequest(false);
    this.api
      .getProfile(customerId)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.setSuccess('Customer loaded from gateway.');
        },
        error: (err: Error) => this.setError(`Customer load failed: ${err.message}`)
      });

    this.refreshContactDetails(customerId);
    this.refreshInteractionHistory(customerId);
    this.refreshNotes(customerId);
    this.refreshAttachments(customerId);
  }

  private refreshContactDetails(customerId: string): void {
    this.beginRequest(false);
    this.api
      .getContactDetails(customerId)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (response) => this.contactDetails.set(response.contactDetails),
        error: (err: Error) => this.setError(`Contact details failed: ${err.message}`)
      });
  }

  private refreshInteractionHistory(customerId: string): void {
    this.beginRequest(false);
    this.api
      .getInteractionHistory(customerId)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (response) => this.interactionItems.set(response.items),
        error: (err: Error) => this.setError(`Interaction history failed: ${err.message}`)
      });
  }

  private refreshNotes(customerId: string): void {
    this.beginRequest(false);
    this.api
      .listNotes(customerId)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (notes) => this.notes.set(notes),
        error: (err: Error) => this.setError(`Notes load failed: ${err.message}`)
      });
  }

  private refreshAttachments(customerId: string): void {
    this.beginRequest(false);
    this.api
      .listAttachments(customerId)
      .pipe(finalize(() => this.completeRequest()))
      .subscribe({
        next: (attachments) => this.attachments.set(attachments),
        error: (err: Error) => this.setError(`Attachments load failed: ${err.message}`)
      });
  }

  private beginRequest(clearMessages = true): void {
    this.activeRequests += 1;
    this.loading.set(true);
    if (clearMessages) {
      this.error.set('');
      this.success.set('');
    }
  }

  private completeRequest(): void {
    this.activeRequests = Math.max(0, this.activeRequests - 1);
    this.loading.set(this.activeRequests > 0);
  }

  private setError(message: string): void {
    this.error.set(message);
  }

  private setSuccess(message: string): void {
    this.success.set(message);
  }
}
