import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TicketConversationPanelComponent } from './ticket-conversation-panel.component';

describe('TicketConversationPanelComponent', () => {
  let fixture: ComponentFixture<TicketConversationPanelComponent>;
  let component: TicketConversationPanelComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [TicketConversationPanelComponent] }).compileComponents();
    fixture = TestBed.createComponent(TicketConversationPanelComponent);
    component = fixture.componentInstance;
    component.canCompose = true;
    component.quickReplies = [
      {
        id: 'q-1',
        title: 'Greeting',
        body: 'Hello, thanks for reaching out.',
        tags: ['welcome'],
        isActive: true,
        createdByUserId: 'u-1',
        updatedByUserId: null,
        createdAtUtc: '2026-09-02T10:00:00Z',
        updatedAtUtc: '2026-09-02T10:00:00Z',
        rowVersion: 'AA=='
      }
    ];
    fixture.detectChanges();
  });

  it('opens helper popup and inserts selected quick reply text', () => {
    const helperButton = fixture.nativeElement.querySelector('.helper-wrap .secondary') as HTMLButtonElement;
    helperButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.helper-popup')).toBeTruthy();

    const option = fixture.nativeElement.querySelector('.helper-popup li button') as HTMLButtonElement;
    option.click();
    fixture.detectChanges();

    expect(component.draft).toContain('Hello, thanks for reaching out.');
    expect(fixture.nativeElement.querySelector('.helper-popup')).toBeFalsy();
  });

  it('emits send event for valid compose text', () => {
    spyOn(component.sendClicked, 'emit');
    component.draft = 'Visible reply';

    component.submit();

    expect(component.sendClicked.emit).toHaveBeenCalledWith('Visible reply');
    expect(component.draft).toBe('');
  });
});
