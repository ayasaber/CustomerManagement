import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { HelpArticleResponse } from '../../../core/models/help-article.models';
import { HelpArticlesApiService } from '../../../core/services/help-articles-api.service';

@Component({
  selector: 'app-admin-help-articles-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="taxonomy-page">
      <h2>Help Articles</h2>

      <p class="status error" *ngIf="error()">{{ error() }}</p>
      <p class="status success" *ngIf="success()">{{ success() }}</p>

      <div class="forms">
        <form [formGroup]="articleForm" (ngSubmit)="createArticle()" class="card">
          <h3>New Help Article</h3>
          <label>
            Title
            <input type="text" formControlName="title" maxlength="200" />
          </label>
          <label>
            Body
            <textarea formControlName="body" rows="6" maxlength="10000"></textarea>
          </label>
          <button type="submit" [disabled]="articleForm.invalid || loading()">Create Article</button>
        </form>
      </div>

      <div class="lists">
        <section class="card">
          <h3>Articles</h3>
          <ul>
            <li *ngFor="let article of articles()">
              <strong>{{ article.title }}</strong>
              <small>({{ article.isActive ? 'active' : 'retired' }})</small>
              <button type="button" (click)="toggleActive(article)" [disabled]="loading()">
                {{ article.isActive ? 'Retire' : 'Reactivate' }}
              </button>
            </li>
          </ul>
        </section>
      </div>
    </section>
  `,
  styleUrls: ['../admin-console.shared.scss']
})
export class AdminHelpArticlesPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly articles = signal<HelpArticleResponse[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly success = signal('');

  readonly articleForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    body: ['', [Validators.required, Validators.maxLength(10000)]]
  });

  constructor(private readonly api: HelpArticlesApiService) {}

  ngOnInit(): void {
    this.refresh();
  }

  createArticle(): void {
    if (this.articleForm.invalid) {
      return;
    }

    const value = this.articleForm.getRawValue();
    this.beginRequest();
    this.api
      .createHelpArticle({
        title: value.title.trim(),
        body: value.body.trim()
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('Help article created.');
          this.articleForm.reset({ title: '', body: '' });
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Create help article failed: ${err.message}`)
      });
  }

  toggleActive(article: HelpArticleResponse): void {
    this.beginRequest();
    this.api
      .updateHelpArticle(article.id, {
        title: article.title,
        body: article.body,
        isActive: !article.isActive,
        rowVersion: article.rowVersion
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set(article.isActive ? 'Help article retired.' : 'Help article reactivated.');
          this.refresh();
        },
        error: (err: Error) => this.error.set(`Update help article failed: ${err.message}`)
      });
  }

  private refresh(): void {
    this.api.listHelpArticles().subscribe({
      next: (articles) => this.articles.set(articles),
      error: (err: Error) => this.error.set(`Help article list failed: ${err.message}`)
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
