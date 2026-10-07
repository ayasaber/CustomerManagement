import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { HelpArticlesApiService } from '../../../core/services/help-articles-api.service';
import { AdminHelpArticlesPageComponent } from './admin-help-articles-page.component';

class HelpArticlesApiServiceStub {
  listHelpArticles = jasmine.createSpy().and.returnValue(
    of([
      {
        id: 'a-1',
        title: 'Getting started with your account',
        body: 'Go to Settings to configure your profile.',
        isActive: true,
        createdAtUtc: '2026-09-01T00:00:00Z',
        updatedAtUtc: '2026-09-01T00:00:00Z',
        rowVersion: [1]
      }
    ])
  );

  createHelpArticle = jasmine.createSpy().and.returnValue(
    of({
      id: 'a-2',
      title: 'Resetting your password',
      body: 'Use the forgot password link.',
      isActive: true,
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [1]
    })
  );

  updateHelpArticle = jasmine.createSpy().and.returnValue(
    of({
      id: 'a-1',
      title: 'Getting started with your account',
      body: 'Go to Settings to configure your profile.',
      isActive: false,
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [2]
    })
  );
}

describe('AdminHelpArticlesPageComponent', () => {
  let fixture: ComponentFixture<AdminHelpArticlesPageComponent>;
  let component: AdminHelpArticlesPageComponent;
  let apiStub: HelpArticlesApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminHelpArticlesPageComponent],
      providers: [{ provide: HelpArticlesApiService, useClass: HelpArticlesApiServiceStub }]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminHelpArticlesPageComponent);
    component = fixture.componentInstance;
    apiStub = TestBed.inject(HelpArticlesApiService) as unknown as HelpArticlesApiServiceStub;
    fixture.detectChanges();
  });

  it('renders help articles from the API', () => {
    expect(apiStub.listHelpArticles).toHaveBeenCalled();
    expect(component.articles().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Getting started with your account');
  });

  it('creates a new help article from the form', () => {
    component.articleForm.setValue({
      title: 'Resetting your password',
      body: 'Use the forgot password link.'
    });

    component.createArticle();

    expect(apiStub.createHelpArticle).toHaveBeenCalledWith({
      title: 'Resetting your password',
      body: 'Use the forgot password link.'
    });
  });

  it('retires an active article via the toggle action', () => {
    const article = component.articles()[0];

    component.toggleActive(article);

    expect(apiStub.updateHelpArticle).toHaveBeenCalledWith(article.id, {
      title: article.title,
      body: article.body,
      isActive: false,
      rowVersion: article.rowVersion
    });
  });
});
