import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GuidesApiService } from '../../core/services/guides-api.service';
import { HelpArticlesApiService } from '../../core/services/help-articles-api.service';
import { KnowledgeBaseSearchApiService } from '../../core/services/knowledge-base-search-api.service';
import { KnowledgeBasePageComponent } from './knowledge-base-page.component';

class HelpArticlesApiServiceStub {
  listHelpArticles = jasmine.createSpy().and.returnValue(
    of([
      {
        id: 'a-1',
        title: 'Getting started with your account',
        body: 'Body text.',
        isActive: true,
        createdAtUtc: '2026-09-01T00:00:00Z',
        updatedAtUtc: '2026-09-01T00:00:00Z',
        rowVersion: [1]
      }
    ])
  );
}

class GuidesApiServiceStub {
  listGuides = jasmine.createSpy().and.returnValue(
    of([
      {
        id: 'g-1',
        title: 'Order did not arrive',
        isActive: true,
        steps: [
          { stepNumber: 1, instruction: 'Check the tracking page.' },
          { stepNumber: 2, instruction: 'Contact the carrier.' }
        ],
        createdAtUtc: '2026-09-01T00:00:00Z',
        updatedAtUtc: '2026-09-01T00:00:00Z',
        rowVersion: [1]
      }
    ])
  );
}

class KnowledgeBaseSearchApiServiceStub {
  search = jasmine.createSpy().and.returnValue(
    of({
      query: 'billing',
      results: [
        { contentType: 'Faq', id: 'f-1', title: 'Billing question', snippet: 'Answer text.' },
        { contentType: 'HelpArticle', id: 'a-1', title: 'Getting started with your account', snippet: 'Body text.' },
        { contentType: 'Guide', id: 'g-1', title: 'Order did not arrive', snippet: 'Check the tracking page.' }
      ]
    })
  );
}

describe('KnowledgeBasePageComponent', () => {
  let fixture: ComponentFixture<KnowledgeBasePageComponent>;
  let component: KnowledgeBasePageComponent;
  let searchApiStub: KnowledgeBaseSearchApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KnowledgeBasePageComponent],
      providers: [
        { provide: HelpArticlesApiService, useClass: HelpArticlesApiServiceStub },
        { provide: GuidesApiService, useClass: GuidesApiServiceStub },
        { provide: KnowledgeBaseSearchApiService, useClass: KnowledgeBaseSearchApiServiceStub }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(KnowledgeBasePageComponent);
    component = fixture.componentInstance;
    searchApiStub = TestBed.inject(KnowledgeBaseSearchApiService) as unknown as KnowledgeBaseSearchApiServiceStub;
    fixture.detectChanges();
  });

  it('renders the default browse lists with no query yet', () => {
    expect(component.helpArticles().length).toBe(1);
    expect(component.guides().length).toBe(1);
    expect(component.hasSearched()).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Getting started with your account');
    expect(fixture.nativeElement.textContent).toContain('Order did not arrive');
  });

  it('renders results with a visible content-type label per row after searching', () => {
    component.searchForm.controls.query.setValue('billing');
    component.search();
    fixture.detectChanges();

    expect(searchApiStub.search).toHaveBeenCalledWith('billing');
    expect(component.results().length).toBe(3);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('FAQ');
    expect(text).toContain('Help Article');
    expect(text).toContain('Guide');
  });

  it('renders an empty state when search returns no results', () => {
    searchApiStub.search.and.returnValue(of({ query: 'nothing', results: [] }));

    component.searchForm.controls.query.setValue('nothing');
    component.search();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No results found.');
  });

  it('expands a guide row to show its ordered steps', () => {
    component.toggleGuide('g-1');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Check the tracking page.');
    expect(text).toContain('Contact the carrier.');
  });
});
