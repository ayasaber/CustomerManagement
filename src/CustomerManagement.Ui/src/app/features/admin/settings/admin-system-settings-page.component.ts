import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { AdminApiService, SystemSettingResponse } from '../../../core/services/admin-api.service';

@Component({
  selector: 'app-admin-system-settings-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="admin-card">
      <header class="section-header">
        <h2>System Settings</h2>
      </header>

      <p class="status-banner warn" *ngIf="conflictMessage()">{{ conflictMessage() }}</p>
      <p class="status-banner error" *ngIf="error()">{{ error() }}</p>
      <p class="status-banner success" *ngIf="success()">{{ success() }}</p>

      <form [formGroup]="settingsForm" class="form-grid" style="grid-template-columns: 1fr;" *ngIf="settings().length > 0">
        <div *ngFor="let setting of settings()" class="admin-card" style="padding: 0.8rem; margin: 0;">
          <h3 style="margin: 0 0 0.35rem;">{{ setting.key }}</h3>
          <p style="margin: 0 0 0.4rem; color: #57708f;">{{ setting.description || 'No description' }}</p>

          <label style="display: grid; gap: 0.3rem;">
            Value
            <input [formControlName]="setting.key" />
          </label>

          <div style="margin-top: 0.55rem;">
            <button class="btn-primary" type="button" (click)="saveSetting(setting)" [disabled]="loading()">Save</button>
          </div>
        </div>
      </form>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminSystemSettingsPageComponent implements OnInit {
  private readonly adminApi = inject(AdminApiService);
  private readonly formBuilder = inject(FormBuilder);

  readonly settings = signal<SystemSettingResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly conflictMessage = signal('');

  readonly settingsForm = this.formBuilder.group({});

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set('');
    this.success.set('');

    this.adminApi
      .listSystemSettings()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (settings) => {
          this.settings.set(settings);

          for (const setting of settings) {
            const control = this.getSettingControl(setting.key);
            if (!control) {
              this.settingsForm.addControl(setting.key, new FormControl(setting.value, { nonNullable: true }));
            } else {
              control.setValue(setting.value);
            }
          }
        },
        error: (error: Error) => this.error.set(error.message)
      });
  }

  saveSetting(setting: SystemSettingResponse): void {
    const control = this.getSettingControl(setting.key);
    if (!control) {
      return;
    }

    const value = String(control.value ?? '');

    this.loading.set(true);
    this.error.set('');
    this.success.set('');
    this.conflictMessage.set('');

    this.adminApi
      .updateSystemSetting(setting.key, value, setting.rowVersion)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (updated) => {
          this.success.set(`Updated ${updated.key}.`);
          const next = this.settings().map((item) => (item.key === updated.key ? updated : item));
          this.settings.set(next);
        },
        error: (error: Error) => {
          if (error.message.toLowerCase().includes('concurrency conflict')) {
            this.conflictMessage.set('Another admin updated this value first. Latest settings were reloaded.');
            this.reload();
            return;
          }

          this.error.set(error.message);
        }
      });
  }

  private getSettingControl(key: string): FormControl<string> | undefined {
    return (this.settingsForm.controls as Record<string, FormControl<string> | undefined>)[key];
  }
}
