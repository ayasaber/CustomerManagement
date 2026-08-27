import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { RegisterRequest } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-screen">
      <div class="auth-shell">
        <article class="auth-panel">
          <p class="auth-eyebrow">Create account</p>
          <h1 class="auth-title">Choose your workspace role</h1>
          <p class="auth-subtitle">
            Step 1: pick the role that matches how you use the platform.
          </p>

          <div class="role-cards" *ngIf="!selectedRole()">
            <button type="button" class="role-card" (click)="selectRole('agent')">
              <h3>I'm an Agent</h3>
              <p>I handle customers, notes, attachments, and interaction history.</p>
            </button>

            <button type="button" class="role-card" (click)="selectRole('customer')">
              <h3>I'm a Customer</h3>
              <p>I need access to my customer portal and support requests.</p>
            </button>
          </div>

          <button *ngIf="selectedRole()" type="button" class="ghost-btn" (click)="clearRole()">
            Change role selection
          </button>
        </article>

        <article class="auth-form-panel" *ngIf="selectedRole()">
          <p class="auth-subtitle selected-pill">Selected role: {{ selectedRole() }}</p>

          <form class="auth-form" [formGroup]="form" (ngSubmit)="submit()">
            <label *ngIf="!isCustomerRole()">
              Display Name
              <input type="text" formControlName="displayName" placeholder="Aya Hassan" />
            </label>

            <label>
              Email
              <input type="email" formControlName="email" placeholder="name@company.com" />
            </label>

            <label>
              Password
              <input type="password" formControlName="password" placeholder="••••••••" />
            </label>

            <label>
              Confirm Password
              <input type="password" formControlName="confirmPassword" placeholder="••••••••" />
            </label>

            <ng-container *ngIf="isCustomerRole()">
              <label>
                Full Name
                <input type="text" formControlName="fullName" placeholder="Aya Hassan" />
              </label>

              <label>
                Company
                <input type="text" formControlName="company" placeholder="Contoso" />
              </label>

              <label>
                Primary Contact Type
                <select formControlName="primaryContactChannel">
                  <option [ngValue]="1">Email</option>
                  <option [ngValue]="6">Phone</option>
                </select>
              </label>

              <label>
                Primary Contact Value
                <input type="text" formControlName="primaryContactValue" placeholder="name@company.com or +201000000000" />
              </label>

              <label>
                Primary Contact Label
                <input type="text" formControlName="primaryContactLabel" placeholder="work" />
              </label>
            </ng-container>

            <p *ngIf="passwordMismatch()" class="auth-status error">
              Password and confirm password must match.
            </p>
            <p *ngIf="error()" class="auth-status error">{{ error() }}</p>

            <button class="primary-btn" type="submit" [disabled]="form.invalid || loading() || passwordMismatch()">
              {{ loading() ? 'Creating account...' : 'Create account' }}
            </button>
          </form>

          <p class="auth-alt">
            Already have an account?
            <a routerLink="/auth/login">Sign in</a>
          </p>
        </article>
      </div>
    </section>
  `,
  styles: [
    `
      .role-cards {
        margin-top: 1rem;
        display: grid;
        gap: 0.8rem;
      }

      .role-card {
        border: 1px solid #bfd0e6;
        background: linear-gradient(170deg, #ffffff, #f8fbff);
        border-radius: 14px;
        text-align: left;
        padding: 0.9rem;
        cursor: pointer;
      }

      .role-card:hover {
        border-color: #4f9fff;
        box-shadow: 0 10px 20px rgba(25, 64, 119, 0.09);
      }

      .role-card h3 {
        margin: 0 0 0.35rem;
        color: #153058;
      }

      .role-card p {
        margin: 0;
        color: #4a6283;
      }

      .selected-pill {
        display: inline-block;
        margin: 0 0 0.8rem;
        padding: 0.25rem 0.65rem;
        border-radius: 999px;
        background: #edf6ff;
        color: #0d4e95;
        font-weight: 700;
      }
    `
  ],
  styleUrls: ['../auth-shared.scss']
})
export class RegisterPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly selectedRole = signal<'agent' | 'customer' | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly isCustomerRole = computed(() => this.selectedRole() === 'customer');

  readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]],
    fullName: [''],
    company: [''],
    primaryContactChannel: [1],
    primaryContactValue: [''],
    primaryContactLabel: ['work']
  });

  readonly passwordMismatch = computed(
    () =>
      this.form.controls.password.value.length > 0 &&
      this.form.controls.confirmPassword.value.length > 0 &&
      this.form.controls.password.value !== this.form.controls.confirmPassword.value
  );

  selectRole(role: 'agent' | 'customer'): void {
    this.selectedRole.set(role);

    if (role === 'customer') {
      this.form.controls.displayName.clearValidators();
      this.form.controls.fullName.addValidators([Validators.required, Validators.maxLength(200)]);
      this.form.controls.company.addValidators([Validators.required, Validators.maxLength(200)]);
      this.form.controls.primaryContactValue.addValidators([Validators.required, Validators.maxLength(320)]);
    } else {
      this.form.controls.displayName.addValidators([Validators.required, Validators.maxLength(200)]);
      this.form.controls.fullName.clearValidators();
      this.form.controls.company.clearValidators();
      this.form.controls.primaryContactValue.clearValidators();
    }

    this.form.controls.displayName.updateValueAndValidity();
    this.form.controls.fullName.updateValueAndValidity();
    this.form.controls.company.updateValueAndValidity();
    this.form.controls.primaryContactValue.updateValueAndValidity();
  }

  clearRole(): void {
    this.selectedRole.set(null);
  }

  submit(): void {
    if (!this.selectedRole()) {
      this.error.set('Select a role before registering.');
      return;
    }

    if (this.form.invalid || this.passwordMismatch()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');

    const request: RegisterRequest = {
      displayName: this.form.controls.displayName.value.trim(),
      email: this.form.controls.email.value.trim(),
      password: this.form.controls.password.value,
      confirmPassword: this.form.controls.confirmPassword.value,
      accountType: this.selectedRole()!
    };

    if (request.accountType === 'customer') {
      request.fullName = this.form.controls.fullName.value.trim();
      request.displayName = request.fullName;
      request.company = this.form.controls.company.value.trim();
      request.contactDetails = [
        {
          channel: this.form.controls.primaryContactChannel.value,
          value: this.form.controls.primaryContactValue.value.trim(),
          label: this.form.controls.primaryContactLabel.value.trim(),
          isPrimary: true
        }
      ];
    }

    this.authService
      .register(request)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          const target = this.authService.resolveLandingRoute({
            roles: response.roles,
            permissions: response.permissions
          });
          void this.router.navigateByUrl(target);
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }
}
