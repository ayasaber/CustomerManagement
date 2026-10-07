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
      <h1>Knowledge Base</h1>

      <p class="status error" *ngIf="error()">{{ error() }}</p>

      <form [formGroup]="searchForm" (ngSubmit)="search()">
        <label>
          Search
          <input type="text" formControlName="query" placeholder="Search FAQs, help articles, and guides" />
        </label>
        <button type="submit">Search</button>
      </form>

      <section class="results" *ngIf="hasSearched()">
        <h2>Search Results</h2>
        <ul>
          <li *ngFor="let result of results()">
            <span class="content-type-badge">{{ contentTypeLabel(result.contentType) }}</span>
            <strong>{{ result.title }}</strong>
            <p>{{ result.snippet }}</p>
          </li>
        </ul>
        <p class="empty" *ngIf="!results().length">No results found.</p>
      </section>

      <section class="browse" *ngIf="!hasSearched()">
        <section class="help-articles">
          <h2>Help Articles</h2>
          <ul>
            <li *ngFor="let article of helpArticles()">{{ article.title }}</li>
          </ul>
          <p class="empty" *ngIf="!helpArticles().length">No help articles are available yet.</p>
        </section>

        <section class="guides">
          <h2>Guides</h2>
          <ul>
            <li *ngFor="let guide of guides()">
              <button type="button" (click)="toggleGuide(guide.id)">{{ guide.title }}</button>
              <ol *ngIf="expandedGuideId() === guide.id">
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

      .content-type-badge {
        display: inline-block;
        margin-right: 0.5rem;
        padding: 0.1rem 0.5rem;
        border-radius: 0.25rem;
        background: #eef2ff;
        font-size: 0.75rem;
      }
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
}
