import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { GuideResponse } from '../../core/models/guide.models';
import { HelpArticleResponse } from '../../core/models/help-article.models';
import { KnowledgeBaseSearchResultResponse } from '../../core/models/knowledge-base-search.models';
import { GuidesApiService } from '../../core/services/guides-api.service';
import { HelpArticlesApiService } from '../../core/services/help-articles-api.service';
import { KnowledgeBaseSearchApiService } from '../../core/services/knowledge-base-search-api.service';

@Component({
  selector: 'app-knowledge-base-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <section class="knowledge-base">
      <header class="kb-header">
        <h1>Knowledge Base</h1>
        <p class="subtitle">Search FAQs, help articles, and guides in one place.</p>
      </header>

      <p class="status error" *ngIf="error()">{{ error() }}</p>

      <form class="search-bar" [formGroup]="searchForm" (ngSubmit)="search()">
        <input type="text" formControlName="query" placeholder="Search FAQs, help articles, and guides" aria-label="Search" />
        <button type="submit">Search</button>
      </form>

      <section class="card results" *ngIf="hasSearched()">
        <h2>Search Results</h2>
        <ul class="result-list">
          <li class="result-row" *ngFor="let result of results()">
            <span class="badge" [class]="contentTypeClass(result.contentType)">{{ contentTypeLabel(result.contentType) }}</span>
            <div class="result-body">
              <strong>{{ result.title }}</strong>
              <p>{{ result.snippet }}</p>
            </div>
          </li>
        </ul>
        <p class="empty" *ngIf="!results().length">No results found.</p>
      </section>

      <section class="browse-grid" *ngIf="!hasSearched()">
        <section class="card">
          <h2>Help Articles</h2>
          <ul class="plain-list">
            <li *ngFor="let article of helpArticles()">{{ article.title }}</li>
          </ul>
          <p class="empty" *ngIf="!helpArticles().length">No help articles are available yet.</p>
        </section>

        <section class="card">
          <h2>Guides</h2>
          <ul class="guide-list">
            <li class="guide-item" *ngFor="let guide of guides()">
              <button type="button" class="guide-toggle" (click)="toggleGuide(guide.id)">
                <span>{{ guide.title }}</span>
                <span class="chevron" [class.open]="expandedGuideId() === guide.id">⌄</span>
              </button>
              <ol class="guide-steps" *ngIf="expandedGuideId() === guide.id">
                <li *ngFor="let step of guide.steps">{{ step.instruction }}</li>
              </ol>
            </li>
          </ul>
          <p class="empty" *ngIf="!guides().length">No guides are available yet.</p>
        </section>
      </section>
    </section>
  `,
  styles: [
    `
      .knowledge-base {
        max-width: 960px;
        margin: 0 auto;
        padding: 1.5rem;
      }

      .kb-header { margin-bottom: 1.2rem; }
      .kb-header h1 { margin: 0; }
      .subtitle { margin: 0.3rem 0 0; color: var(--color-text-muted); }

      .status.error {
        color: #8a2d24;
        background: #fff2f1;
        border: 1px solid #efc1bd;
        border-radius: var(--radius-md);
        padding: 0.5rem 0.65rem;
      }

      .search-bar {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 1.2rem;
      }

      .search-bar input {
        flex: 1;
        border: 1px solid var(--color-border);
        border-radius: var(--radius-md);
        padding: 0.6rem 0.8rem;
        font: inherit;
      }

      .search-bar button {
        border: none;
        border-radius: var(--radius-md);
        background: var(--color-primary-gradient);
        color: #fff;
        font-weight: 600;
        padding: 0.6rem 1.2rem;
        cursor: pointer;
      }

      .card {
        background: var(--color-surface);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-lg);
        box-shadow: var(--shadow-card);
        padding: 1.1rem 1.2rem;
      }

      .card h2 { margin: 0 0 0.8rem; font-size: 1.05rem; }

      .browse-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 1rem;
      }

      @media (max-width: 720px) {
        .browse-grid { grid-template-columns: 1fr; }
      }

      .plain-list,
      .guide-list,
      .result-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: grid;
        gap: 0.5rem;
      }

      .plain-list li {
        padding: 0.55rem 0.7rem;
        border: 1px solid var(--color-border);
        border-radius: var(--radius-md);
        background: #fbfdff;
      }

      .result-row {
        display: flex;
        align-items: flex-start;
        gap: 0.7rem;
        padding: 0.7rem 0.8rem;
        border: 1px solid var(--color-border);
        border-radius: var(--radius-md);
      }

      .result-body strong { display: block; color: var(--color-primary-dark); }
      .result-body p { margin: 0.2rem 0 0; color: var(--color-text-muted); }

      .badge {
        flex: none;
        padding: 0.15rem 0.55rem;
        border-radius: 999px;
        font-size: 0.75rem;
        font-weight: 700;
        background: #eaf2ff;
        color: #19498e;
      }

      .badge-faq { background: #eaf2ff; color: #19498e; }
      .badge-help-article { background: #eafff2; color: #1a6a46; }
      .badge-guide { background: #fff3e0; color: #8a5a10; }

      .guide-item {
        border: 1px solid var(--color-border);
        border-radius: var(--radius-md);
        overflow: hidden;
      }

      .guide-toggle {
        width: 100%;
        display: flex;
        justify-content: space-between;
        align-items: center;
        border: none;
        background: #fbfdff;
        color: var(--color-primary-dark);
        font: inherit;
        font-weight: 600;
        padding: 0.65rem 0.8rem;
        cursor: pointer;
      }

      .guide-toggle .chevron { transition: transform 0.15s ease; }
      .guide-toggle .chevron.open { transform: rotate(180deg); }

      .guide-steps {
        margin: 0;
        padding: 0.6rem 1.2rem 0.8rem 1.6rem;
        background: #fff;
        color: var(--color-text-muted);
      }

      .guide-steps li { margin: 0.2rem 0; }

      .empty { color: var(--color-text-muted); }
    `
  ]
})
export class KnowledgeBasePageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly helpArticles = signal<HelpArticleResponse[]>([]);
  readonly guides = signal<GuideResponse[]>([]);
  readonly results = signal<KnowledgeBaseSearchResultResponse[]>([]);
  readonly hasSearched = signal(false);
  readonly expandedGuideId = signal<string | null>(null);
  readonly error = signal('');

  readonly searchForm = this.formBuilder.nonNullable.group({
    query: ['']
  });

  constructor(
    private readonly helpArticlesApi: HelpArticlesApiService,
    private readonly guidesApi: GuidesApiService,
    private readonly searchApi: KnowledgeBaseSearchApiService
  ) {}

  ngOnInit(): void {
    this.helpArticlesApi.listHelpArticles().subscribe({
      next: (articles) => this.helpArticles.set(articles),
      error: (err: Error) => this.error.set(`Help article list failed: ${err.message}`)
    });

    this.guidesApi.listGuides().subscribe({
      next: (guides) => this.guides.set(guides),
      error: (err: Error) => this.error.set(`Guide list failed: ${err.message}`)
    });
  }

  search(): void {
    const query = this.searchForm.getRawValue().query.trim();
    if (!query) {
      this.hasSearched.set(false);
      this.results.set([]);
      return;
    }

    this.searchApi.search(query).subscribe({
      next: (response) => {
        this.hasSearched.set(true);
        this.results.set(response.results);
      },
      error: (err: Error) => this.error.set(`Search failed: ${err.message}`)
    });
  }

  toggleGuide(guideId: string): void {
    this.expandedGuideId.set(this.expandedGuideId() === guideId ? null : guideId);
  }

  contentTypeLabel(contentType: KnowledgeBaseSearchResultResponse['contentType']): string {
    switch (contentType) {
      case 'Faq':
        return 'FAQ';
      case 'HelpArticle':
        return 'Help Article';
      case 'Guide':
        return 'Guide';
      default:
        return contentType;
    }
  }

  contentTypeClass(contentType: KnowledgeBaseSearchResultResponse['contentType']): string {
    switch (contentType) {
      case 'Faq':
        return 'badge-faq';
      case 'HelpArticle':
        return 'badge-help-article';
      case 'Guide':
        return 'badge-guide';
      default:
        return '';
    }
  }
}
