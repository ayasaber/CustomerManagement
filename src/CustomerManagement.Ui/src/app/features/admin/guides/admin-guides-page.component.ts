import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { GuideResponse } from '../../../core/models/guide.models';
import { GuidesApiService } from '../../../core/services/guides-api.service';

@Component({
  selector: 'app-admin-guides-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="taxonomy-page">
      <h2>Guides</h2>

      <p class="status error" *ngIf="error()">{{ error() }}</p>
      <p class="status success" *ngIf="success()">{{ success() }}</p>

      <div class="forms">
        <form [formGroup]="guideForm" (ngSubmit)="createGuide()" class="card">
          <h3>New Guide</h3>
          <label>
            Title
            <input type="text" formControlName="title" maxlength="200" />
          </label>

          <div formArrayName="steps">
            <label *ngFor="let step of steps.controls; let i = index">
              Step {{ i + 1 }}
              <input type="text" [formControlName]="i" maxlength="1000" />
              <button type="button" (click)="removeStep(i)" [disabled]="steps.length <= 1">Remove step</button>
            </label>
          </div>
          <button type="button" (click)="addStep()">Add step</button>

          <button type="submit" [disabled]="guideForm.invalid || loading()">Create Guide</button>
        </form>
      </div>

      <div class="lists">
        <section class="card">
          <h3>Guides</h3>
          <ul>
            <li *ngFor="let guide of guides()">
              <strong>{{ guide.title }}</strong>
              <small>({{ guide.isActive ? 'active' : 'retired' }})</small>
              <ol>
                <li *ngFor="let step of guide.steps">{{ step.instruction }}</li>
              </ol>
              <button type="button" (click)="toggleActive(guide)" [disabled]="loading()">
                {{ guide.isActive ? 'Retire' : 'Reactivate' }}
              </button>
            </li>
          </ul>
        </section>
      </div>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminGuidesPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly guides = signal<GuideResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  readonly guideForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    steps: this.formBuilder.nonNullable.array([this.newStepControl()])
  });

  get steps() {
    return this.guideForm.controls.steps;
  }

  constructor(private readonly api: GuidesApiService) {}

  ngOnInit(): void {
    this.refresh();
  }

  addStep(): void {
    this.steps.push(this.newStepControl());
  }

  removeStep(index: number): void {
    if (this.steps.length <= 1) {
      return;
    }

    this.steps.removeAt(index);
  }

  createGuide(): void {
    if (this.guideForm.invalid) {
      return;
    }

    const value = this.guideForm.getRawValue();
    this.beginRequest();
    this.api
      .createGuide({
        title: value.title.trim(),
        steps: value.steps.map((step) => step.trim())
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('Guide created.');
          this.resetForm();
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Create guide failed: ${err.message}`)
      });
  }

  toggleActive(guide: GuideResponse): void {
    this.beginRequest();
    this.api
      .updateGuide(guide.id, {
        title: guide.title,
        steps: guide.steps.map((step) => step.instruction),
        isActive: !guide.isActive,
        rowVersion: guide.rowVersion
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set(guide.isActive ? 'Guide retired.' : 'Guide reactivated.');
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Update guide failed: ${err.message}`)
      });
  }

  private resetForm(): void {
    this.guideForm.reset({ title: '' });
    while (this.steps.length > 1) {
      this.steps.removeAt(0);
    }
    this.steps.at(0).setValue('');
  }

  private newStepControl(): FormControl<string> {
    return this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(1000)]);
  }

  private refresh(): void {
    this.api.listGuides().subscribe({
      next: (guides) => this.guides.set(guides),
      error: (err: Error) => this.error.set(`Guide list failed: ${err.message}`)
    });
  }

  private beginRequest(): void {
    this.loading.set(true);
    this.error.set('');
    this.success.set('');
  }

  private finishRequest(): void {
    this.loading.set(false);
  }
}
