import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AssignedTicketsTableComponent } from './assigned-tickets-table.component';
import { OpenTasksListComponent } from './open-tasks-list.component';
import { QuickReplyPickerComponent } from './quick-reply-picker.component';
import { TicketConversationPanelComponent } from '../../tickets/components/ticket-conversation-panel.component';
import { TicketNotesPanelComponent } from './ticket-notes-panel.component';

describe('Agent dashboard components', () => {
  it('assigned tickets component renders rows and emits selection', async () => {
    await TestBed.configureTestingModule({ imports: [AssignedTicketsTableComponent] }).compileComponents();
    const fixture: ComponentFixture<AssignedTicketsTableComponent> = TestBed.createComponent(AssignedTicketsTableComponent);
    const component = fixture.componentInstance;

    component.tickets = [
      {
        ticketId: 't-1',
        ticketNumber: 'TKT-0001',
        subject: 'Need refund',
        status: 'in_progress',
        priority: 'normal',
        category: 'billing',
        isEscalated: false,
        createdAtUtc: '2026-09-01T10:00:00Z',
        updatedAtUtc: '2026-09-01T11:00:00Z',
        customerId: 'c-1',
        customerDisplayName: 'Aya'
      }
    ];

    spyOn(component.ticketSelected, 'emit');
    fixture.detectChanges();

    const row = fixture.nativeElement.querySelector('tbody tr') as HTMLTableRowElement;
    expect(row).toBeTruthy();
    row.click();

    expect(component.ticketSelected.emit).toHaveBeenCalledWith('t-1');
  });

  it('open tasks component emits complete action', async () => {
    await TestBed.configureTestingModule({ imports: [OpenTasksListComponent] }).compileComponents();
    const fixture: ComponentFixture<OpenTasksListComponent> = TestBed.createComponent(OpenTasksListComponent);
    const component = fixture.componentInstance;

    component.tasks = [
      {
        taskId: 'task-1',
        ticketId: 't-1',
        ticketNumber: 'TKT-0001',
        description: 'Call customer',
        dueAtUtc: '2026-09-01T16:00:00Z',
        createdAtUtc: '2026-09-01T09:00:00Z'
      }
    ];

    spyOn(component.completeClicked, 'emit');
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button.complete') as HTMLButtonElement;
    button.click();

    expect(component.completeClicked.emit).toHaveBeenCalledWith('task-1');
  });

  it('quick reply picker emits insert text', async () => {
    await TestBed.configureTestingModule({ imports: [QuickReplyPickerComponent] }).compileComponents();
    const fixture: ComponentFixture<QuickReplyPickerComponent> = TestBed.createComponent(QuickReplyPickerComponent);
    const component = fixture.componentInstance;

    component.replies = [
      {
        id: 'q-1',
        title: 'Greeting',
        body: 'Hello, thanks for contacting us.',
        tags: ['welcome'],
        isActive: true,
        createdByUserId: 'u-1',
        updatedByUserId: null,
        createdAtUtc: '2026-09-01T10:00:00Z',
        updatedAtUtc: '2026-09-01T10:00:00Z',
        rowVersion: 'AA=='
      }
    ];

    spyOn(component.insertClicked, 'emit');
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    button.click();

    expect(component.insertClicked.emit).toHaveBeenCalledWith('Hello, thanks for contacting us.');
  });

  it('ticket notes panel blocks handoff when no reassign target selected', async () => {
    await TestBed.configureTestingModule({ imports: [TicketNotesPanelComponent] }).compileComponents();
    const fixture: ComponentFixture<TicketNotesPanelComponent> = TestBed.createComponent(TicketNotesPanelComponent);
    const component = fixture.componentInstance;

    component.agents = [
      { id: 'u-1', displayName: 'Agent One' },
      { id: 'u-2', displayName: 'Agent Two' }
    ];

    spyOn(component.createNoteClicked, 'emit');
    fixture.detectChanges();

    component.form.patchValue({ body: 'Need handoff', offerReassign: true, mentions: ['u-2'], reassignToUserId: '' });
    component.submit();

    expect(component.createNoteClicked.emit).not.toHaveBeenCalled();

    component.form.patchValue({ reassignToUserId: 'u-2' });
    component.submit();

    expect(component.createNoteClicked.emit).toHaveBeenCalled();
  });

  it('conversation panel helper inserts selected quick reply into compose text', async () => {
    await TestBed.configureTestingModule({ imports: [TicketConversationPanelComponent] }).compileComponents();
    const fixture: ComponentFixture<TicketConversationPanelComponent> = TestBed.createComponent(TicketConversationPanelComponent);
    const component = fixture.componentInstance;

    component.canCompose = true;
    component.quickReplies = [
      {
        id: 'q-1',
        title: 'Follow-up',
        body: 'Thank you for your patience.',
        tags: ['followup'],
        isActive: true,
        createdByUserId: 'u-1',
        updatedByUserId: null,
        createdAtUtc: '2026-09-02T10:00:00Z',
        updatedAtUtc: '2026-09-02T10:00:00Z',
        rowVersion: 'AA=='
      }
    ];

    fixture.detectChanges();

    const helperButton = fixture.nativeElement.querySelector('.helper-wrap .secondary') as HTMLButtonElement;
    helperButton.click();
    fixture.detectChanges();

    const replyButton = fixture.nativeElement.querySelector('.helper-popup li button') as HTMLButtonElement;
    replyButton.click();
    fixture.detectChanges();

    expect(component.draft).toContain('Thank you for your patience.');
  });
});
