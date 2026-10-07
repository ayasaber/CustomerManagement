import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GuidesApiService } from '../../../core/services/guides-api.service';
import { AdminGuidesPageComponent } from './admin-guides-page.component';

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

  createGuide = jasmine.createSpy().and.returnValue(
    of({
      id: 'g-2',
      title: 'Reset your password',
      isActive: true,
      steps: [{ stepNumber: 1, instruction: 'Click forgot password.' }],
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [1]
    })
  );

  updateGuide = jasmine.createSpy().and.returnValue(
    of({
      id: 'g-1',
      title: 'Order did not arrive',
      isActive: false,
      steps: [
        { stepNumber: 1, instruction: 'Check the tracking page.' },
        { stepNumber: 2, instruction: 'Contact the carrier.' }
      ],
      createdAtUtc: '2026-09-01T00:00:00Z',
      updatedAtUtc: '2026-09-01T00:00:00Z',
      rowVersion: [2]
    })
  );
}

describe('AdminGuidesPageComponent', () => {
  let fixture: ComponentFixture<AdminGuidesPageComponent>;
  let component: AdminGuidesPageComponent;
  let apiStub: GuidesApiServiceStub;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminGuidesPageComponent],
      providers: [{ provide: GuidesApiService, useClass: GuidesApiServiceStub }]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminGuidesPageComponent);
    component = fixture.componentInstance;
    apiStub = TestBed.inject(GuidesApiService) as unknown as GuidesApiServiceStub;
    fixture.detectChanges();
  });

  it('renders guides from the API', () => {
    expect(apiStub.listGuides).toHaveBeenCalled();
    expect(component.guides().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Order did not arrive');
  });

  it('starts with a single step row and can add/remove steps', () => {
    expect(component.steps.length).toBe(1);

    component.addStep();
    expect(component.steps.length).toBe(2);

    component.removeStep(0);
    expect(component.steps.length).toBe(1);

    component.removeStep(0);
    expect(component.steps.length).toBe(1);
  });

  it('creates a new guide with ordered steps from the form', () => {
    component.guideForm.controls.title.setValue('Reset your password');
    component.steps.at(0).setValue('Click forgot password.');

    component.createGuide();

    expect(apiStub.createGuide).toHaveBeenCalledWith({
      title: 'Reset your password',
      steps: ['Click forgot password.']
    });
  });

  it('retires an active guide via the toggle action', () => {
    const guide = component.guides()[0];

    component.toggleActive(guide);

    expect(apiStub.updateGuide).toHaveBeenCalledWith(guide.id, {
      title: guide.title,
      steps: guide.steps.map((step) => step.instruction),
      isActive: false,
      rowVersion: guide.rowVersion
    });
  });
});
