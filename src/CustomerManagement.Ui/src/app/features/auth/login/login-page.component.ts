import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { LoginRequest } from '../../../core/models/auth.models';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-screen">
      <div class="auth-shell">
        <article class="auth-panel">
          <p class="auth-eyebrow">Customer Support CRM</p>
          <h1 class="auth-title">Welcome back</h1>
          <p class="auth-subtitle">
            Sign in with your account. Your destination route is selected automatically based on your role.
          </p>
        </article>

        <article class="auth-form-panel">
          <form class="auth-form" [formGroup]="form" (ngSubmit)="submit()">
            <label>
              Email
              <input type="email" formControlName="email" placeholder="name@company.com" />
            </label>

            <label>
              Password
              <input type="password" formControlName="password" placeholder="••••••••" />
            </label>

            <p *ngIf="error()" class="auth-status error">{{ error() }}</p>

            <button class="primary-btn" type="submit" [disabled]="form.invalid || loading()">
              {{ loading() ? 'Signing in...' : 'Sign in' }}
            </button>
          </form>

          <p class="auth-alt">
            New here?
            <a routerLink="/auth/register">Create your account</a>
          </p>
        </article>
      </div>
    </section>
  `,
  styleUrls: ['../auth-shared.scss']
})
export class LoginPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly error = signal('');

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');

    const request: LoginRequest = {
      email: this.form.controls.email.value.trim(),
      password: this.form.controls.password.value
    };

    this.authService
      .login(request)
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
