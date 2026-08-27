import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CreateCustomerProfileRequest,
  CustomerProfileResponse,
  UpdateCustomerProfileRequest
} from '../../core/models/customer-management.models';

@Component({
  selector: 'app-customer-profile-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="panel">
      <h2>Customer Profile</h2>
      <form [formGroup]="form" (ngSubmit)="submit()" class="stack">
        <label>
          Name
          <input formControlName="name" />
        </label>

        <label>
          Company
          <input formControlName="company" />
        </label>

        <label>
          Contact Channel
          <input type="number" formControlName="channel" />
        </label>

        <label>
          Contact Value
          <input formControlName="value" />
        </label>

        <label>
          Label
          <input formControlName="label" />
        </label>

        <button type="submit" [disabled]="form.invalid || !profile">Update Profile</button>
      </form>
    </section>
  `,
  styles: [
    ':host { display: block; }',
    '.panel { height: 100%; padding: 1rem; border: 1px solid #d7d2c8; border-radius: 12px; background: #fffdf8; }',
    'h2 { margin: 0 0 0.8rem; }',
    '.stack { display: grid; gap: 0.75rem; }',
    'input { width: 100%; padding: 0.5rem; border: 1px solid #c8c2b8; border-radius: 8px; }',
    'button { justify-self: start; padding: 0.6rem 1rem; border: 0; border-radius: 999px; background: #1d5c63; color: #ffffff; }'
  ]
})
export class CustomerProfileFormComponent {
  private readonly formBuilder = inject(FormBuilder);

  @Input() profile: CustomerProfileResponse | null = null;

  @Output() createRequested = new EventEmitter<CreateCustomerProfileRequest>();
  @Output() updateRequested = new EventEmitter<UpdateCustomerProfileRequest>();

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    company: [''],
    channel: [1, [Validators.required]],
    value: ['', [Validators.required, Validators.maxLength(200)]],
    label: ['work']
  });

  ngOnChanges(): void {
    if (!this.profile) {
      return;
    }

    const primary = this.profile.contactDetails.find((x) => x.isPrimary) ?? this.profile.contactDetails[0];
    this.form.patchValue({
      name: this.profile.name,
      company: this.profile.company ?? '',
      channel: primary?.channel ?? 1,
      value: primary?.value ?? '',
      label: primary?.label ?? 'work'
    });
  }

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    const contactDetails = [
      {
        channel: this.form.controls.channel.value,
        value: this.form.controls.value.value,
        label: this.form.controls.label.value,
        isPrimary: true
      }
    ];

    if (this.profile) {
      this.updateRequested.emit({
        name: this.form.controls.name.value,
        company: this.form.controls.company.value,
        rowVersion: this.profile.rowVersion,
        contactDetails
      });
      return;
    }
  }
}
