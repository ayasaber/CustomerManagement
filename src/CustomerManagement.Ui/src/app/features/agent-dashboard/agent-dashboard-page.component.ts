import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';
import {
  AgentOption,
  DashboardAssignedTicketItemResponse,
  DashboardCustomerContextResponse,
  DashboardOpenTaskSummaryItemResponse,
  QuickReplyResponse,
  TicketHandoffRequestResponse,
  TicketInternalNoteResponse
} from '../../core/models/agent-dashboard.models';
import { TicketMessageResponse } from '../../core/models/ticket-management.models';
import { AgentDashboardApiService } from '../../core/services/agent-dashboard-api.service';
import { AuthService } from '../../core/services/auth.service';
import { TicketManagementApiService } from '../../core/services/ticket-management-api.service';
import { AssignedTicketsTableComponent } from './components/assigned-tickets-table.component';
import { CustomerContextPanelComponent } from './components/customer-context-panel.component';
import { HandoffInboxPanelComponent } from './components/handoff-inbox-panel.component';
import { NoteDraftPayload, TicketNotesPanelComponent } from './components/ticket-notes-panel.component';
import { AgentTicketConversationPanelComponent } from './components/ticket-conversation-panel.component';
import { CreateTaskDraftPayload, OpenTasksListComponent } from './components/open-tasks-list.component';
import { QuickReplyAdminPanelComponent } from './components/quick-reply-admin-panel.component';

@Component({
  selector: 'app-agent-dashboard-page',
  standalone: true,
  imports: [
    CommonModule,
    AssignedTicketsTableComponent,
    OpenTasksListComponent,
    CustomerContextPanelComponent,
    TicketNotesPanelComponent,
    AgentTicketConversationPanelComponent,
    QuickReplyAdminPanelComponent,
    HandoffInboxPanelComponent
  ],
  template: `
    <section class="dashboard-page">
      <header class="page-header">
        <div>
          <h1>Agent Dashboard</h1>
          <p>Unified workspace for assigned tickets, tasks, notes, handoff requests, and quick replies.</p>
        </div>
      </header>

      <p class="feedback error" *ngIf="error()">{{ error() }}</p>
      <p class="feedback success" *ngIf="success()">{{ success() }}</p>

      <div class="layout" [class.loading]="loading()">
        <div class="left-column">
          <app-assigned-tickets-table
            [tickets]="assignedTickets()"
            [selectedTicketId]="selectedTicketId()"
            (ticketSelected)="onTicketSelected($event)" />

          <app-open-tasks-list
            [tasks]="openTasks()"
            [selectedTicketId]="selectedTicketId()"
            (createClicked)="createTask($event)"
            (completeClicked)="completeTask($event)" />
        </div>

        <div class="right-column">
          <app-customer-context-panel [context]="customerContext()" />

          <app-ticket-notes-panel
            [notes]="notes()"
            [agents]="agentOptions()"
            [insertText]="insertText()"
            [insertToken]="insertToken()"
            (createNoteClicked)="createNote($event)" />

          <app-agent-ticket-conversation-panel
            [messages]="ticketMessages()"
            [quickReplies]="quickReplies()"
            [canCompose]="canComposeConversation()"
            [sending]="conversationSending()"
            [loading]="conversationLoading()"
            (reloadClicked)="reloadConversation()"
            (sendClicked)="postConversationMessage($event)" />

          <app-handoff-inbox-panel
            [requests]="handoffRequests()"
            (acceptClicked)="respondToHandoff($event, true)"
            (rejectClicked)="respondToHandoff($event, false)" />

          <app-quick-reply-admin-panel
            [visible]="canManageQuickReplies()"
            [replies]="quickReplies()"
            (createClicked)="createQuickReply($event)"
            (toggleActive)="toggleQuickReply($event)" />
        </div>
      </div>
    </section>
  `,
  styles: [
    `
      .dashboard-page {
        max-width: 1400px;
        margin: 1rem auto;
        padding: 1rem;
      }

      .page-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 1rem;
        margin-bottom: 0.8rem;
        flex-wrap: wrap;
      }

      h1 { margin: 0; font-size: 1.6rem; }
      .page-header p { margin: 0.3rem 0 0; color: var(--color-text-muted); }
      .page-header button:disabled { opacity: 0.6; cursor: not-allowed; }

      .layout {
        display: grid;
        grid-template-columns: 1.05fr 1fr;
        gap: 0.85rem;
      }

      .left-column,
      .right-column {
        display: grid;
        gap: 0.75rem;
        align-content: start;
      }

      .feedback { margin: 0.35rem 0 0.8rem; padding: 0.5rem 0.65rem; border-radius: 10px; }
      .feedback.error { background: #fff2f1; color: #8a2d24; border: 1px solid #efc1bd; }
      .feedback.success { background: #effcf6; color: #23563e; border: 1px solid #bee7d2; }

      .loading { opacity: 0.75; pointer-events: none; }

      @media (max-width: 1279px) {
        .layout { grid-template-columns: 1fr; }
      }
    `
  ]
})
export class AgentDashboardPageComponent implements OnInit {
  readonly assignedTickets = signal<DashboardAssignedTicketItemResponse[]>([]);
  readonly openTasks = signal<DashboardOpenTaskSummaryItemResponse[]>([]);
  readonly quickReplies = signal<QuickReplyResponse[]>([]);
  readonly notes = signal<TicketInternalNoteResponse[]>([]);
  readonly handoffRequests = signal<TicketHandoffRequestResponse[]>([]);
  readonly customerContext = signal<DashboardCustomerContextResponse | null>(null);
  readonly ticketMessages = signal<TicketMessageResponse[]>([]);
  readonly selectedTicketId = signal('');
  readonly loading = signal(false);
  readonly conversationLoading = signal(false);
  readonly conversationSending = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly insertText = signal('');
  readonly insertToken = signal(0);
  readonly agentOptions = signal<AgentOption[]>([]);

  readonly canManageQuickReplies = computed(() => this.authService.hasRole('admin'));
  readonly canComposeConversation = computed(() => this.authService.hasRole('admin') || this.authService.hasRole('agent'));

  constructor(
    private readonly dashboardApi: AgentDashboardApiService,
    private readonly ticketApi: TicketManagementApiService,
    private readonly authService: AuthService
  ) {}

  ngOnInit(): void {
    this.reloadAll();
  }

  reloadAll(): void {
    this.beginRequest();

    forkJoin({
      tickets: this.dashboardApi.getAssignedTickets(1, 30),
      tasks: this.dashboardApi.getOpenTasks(1, 30),
      quickReplies: this.dashboardApi.listQuickReplies(true),
      handoff: this.dashboardApi.getMyHandoffRequests()
    })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (payload) => {
          this.assignedTickets.set(payload.tickets.items);
          this.openTasks.set(payload.tasks.items);
          this.quickReplies.set(payload.quickReplies.items);
          this.handoffRequests.set(payload.handoff.items);
          this.success.set('Dashboard data refreshed.');

          const nextTicketId = this.selectedTicketId() || payload.tickets.items[0]?.ticketId || '';
          this.selectedTicketId.set(nextTicketId);
          this.loadTicketScope(nextTicketId);
          this.loadAgentOptions();
        },
        error: (err: Error) => this.error.set(`Dashboard load failed: ${err.message}`)
      });
  }

  onTicketSelected(ticketId: string): void {
    this.selectedTicketId.set(ticketId);
    this.loadTicketScope(ticketId);
  }

  createNote(payload: NoteDraftPayload): void {
    const ticketId = this.selectedTicketId();
    if (!ticketId) {
      this.error.set('Select a ticket first.');
      return;
    }

    const currentUserId = this.authService.currentUser()()?.userId.toLowerCase() ?? '';
    const mentionSet = new Set(payload.mentionedUserIds.map((id) => id.toLowerCase()));

    if (currentUserId && mentionSet.has(currentUserId)) {
      this.error.set('You cannot mention yourself. Select teammates only.');
      return;
    }

    if (payload.offerReassign) {
      if (!payload.reassignToUserId) {
        this.error.set('Select a handoff target when "Mention and offer handoff" is enabled.');
        return;
      }

      const targetUserId = payload.reassignToUserId.toLowerCase();
      if (currentUserId && targetUserId === currentUserId) {
        this.error.set('Handoff target must be a teammate, not yourself.');
        return;
      }

      if (!mentionSet.has(targetUserId)) {
        this.error.set('Handoff target must also be selected in "Mention teammates".');
        return;
      }
    }

    this.beginRequest();
    this.dashboardApi
      .createTicketNote({
        ticketId,
        body: payload.body,
        mentionedUserIds: payload.mentionedUserIds,
        offerReassign: payload.offerReassign,
        reassignToUserId: payload.reassignToUserId
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (created) => {
          this.success.set('Internal note posted.');
          this.loadTicketScope(ticketId);

          if (created.reassignProposal?.reassignToUserId) {
            this.createHandoffAfterNote(created.note.id, created.reassignProposal.reassignToUserId, ticketId);
          }
        },
        error: (err: Error) => this.error.set(`Create note failed: ${err.message}`)
      });
  }

  completeTask(taskId: string): void {
    this.beginRequest();

    this.dashboardApi
      .listTicketTasks(true)
      .pipe(
        map((response) => response.items.find((task) => task.id === taskId) ?? null),
        finalize(() => this.finishRequest())
      )
      .subscribe({
        next: (task) => {
          if (!task) {
            this.error.set('Task no longer available. Refresh and try again.');
            return;
          }

          this.beginRequest();
          this.dashboardApi
            .completeTicketTask(task.id, { rowVersion: task.rowVersion })
            .pipe(finalize(() => this.finishRequest()))
            .subscribe({
              next: () => {
                this.success.set('Task marked as done.');
                this.reloadAll();
              },
              error: (err: Error) => this.error.set(`Complete task failed: ${err.message}`)
            });
        },
        error: (err: Error) => this.error.set(`Task refresh failed: ${err.message}`)
      });
  }

  createTask(payload: CreateTaskDraftPayload): void {
    const ticketId = this.selectedTicketId();
    if (!ticketId) {
      this.error.set('Select a ticket first.');
      return;
    }

    this.beginRequest();
    this.dashboardApi
      .createTicketTask({
        ticketId,
        description: payload.description,
        dueAtUtc: payload.dueAtUtc,
        assignedToUserId: null
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('Task added.');
          this.reloadAll();
        },
        error: (err: Error) => this.error.set(`Create task failed: ${err.message}`)
      });
  }

  insertQuickReply(body: string): void {
    this.insertText.set(body);
    this.insertToken.update((current) => current + 1);
    this.success.set('Quick reply inserted into note composer.');
  }

  postConversationMessage(body: string): void {
    const ticketId = this.selectedTicketId();
    if (!ticketId) {
      this.error.set('Select a ticket first.');
      return;
    }

    this.conversationSending.set(true);
    this.ticketApi
      .createTicketMessage({ ticketId, body })
      .pipe(finalize(() => this.conversationSending.set(false)))
      .subscribe({
        next: (response) => {
          this.success.set('Conversation message posted.');
          this.ticketMessages.update((items) => items.concat(response.message));
        },
        error: (err: Error) => this.error.set(`Conversation post failed: ${err.message}`)
      });
  }

  reloadConversation(): void {
    const ticketId = this.selectedTicketId();
    if (!ticketId) {
      this.ticketMessages.set([]);
      return;
    }

    this.conversationLoading.set(true);
    this.ticketApi
      .listTicketMessages(ticketId, 1, 50)
      .pipe(finalize(() => this.conversationLoading.set(false)))
      .subscribe({
        next: (response) => this.ticketMessages.set(response.items),
        error: (err: Error) => this.error.set(`Conversation load failed: ${err.message}`)
      });
  }

  createQuickReply(payload: { title: string; body: string; tags: string[] }): void {
    this.beginRequest();
    this.dashboardApi
      .createQuickReply(payload)
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('Quick reply added.');
          this.loadQuickReplies();
        },
        error: (err: Error) => this.error.set(`Create quick reply failed: ${err.message}`)
      });
  }

  toggleQuickReply(reply: QuickReplyResponse): void {
    this.beginRequest();
    this.dashboardApi
      .updateQuickReply(reply.id, {
        title: reply.title,
        body: reply.body,
        tags: reply.tags,
        isActive: !reply.isActive,
        rowVersion: reply.rowVersion
      })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: () => {
          this.success.set('Quick reply updated.');
          this.loadQuickReplies();
        },
        error: (err: Error) => this.error.set(`Update quick reply failed: ${err.message}`)
      });
  }

  respondToHandoff(request: TicketHandoffRequestResponse, accept: boolean): void {
    this.beginRequest();
    this.ticketApi
      .getTicket(request.ticketId)
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (ticket) => {
          this.beginRequest();
          this.dashboardApi
            .respondToHandoffRequest(request.id, {
              accept,
              message: accept ? 'Accepted from dashboard.' : 'Rejected from dashboard.',
              ticketRowVersion: ticket.rowVersion
            })
            .pipe(finalize(() => this.finishRequest()))
            .subscribe({
              next: () => {
                this.success.set(accept ? 'Handoff accepted.' : 'Handoff rejected.');
                this.reloadAll();
              },
              error: (err: Error) => this.error.set(`Respond handoff failed: ${err.message}`)
            });
        },
        error: (err: Error) => this.error.set(`Ticket refresh failed: ${err.message}`)
      });
  }

  private createHandoffAfterNote(noteId: string, targetAssigneeUserId: string, ticketId: string): void {
    this.beginRequest(false);
    this.ticketApi
      .getTicket(ticketId)
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (ticket) => {
          this.beginRequest(false);
          this.dashboardApi
            .createHandoffRequest(noteId, {
              noteId,
              ticketId,
              targetAssigneeUserId,
              message: 'Handoff proposed from dashboard note.',
              ticketRowVersion: ticket.rowVersion
            })
            .pipe(finalize(() => this.finishRequest()))
            .subscribe({
              next: () => {
                this.success.set('Handoff request submitted.');
                this.loadHandoffRequests();
              },
              error: (err: Error) => this.error.set(`Create handoff failed: ${err.message}`)
            });
        },
        error: (err: Error) => this.error.set(`Ticket refresh failed: ${err.message}`)
      });
  }

  private loadTicketScope(ticketId: string): void {
    if (!ticketId) {
      this.customerContext.set(null);
      this.notes.set([]);
      this.ticketMessages.set([]);
      return;
    }

    this.beginRequest(false);
    forkJoin({
      context: this.dashboardApi.getTicketCustomerContext(ticketId),
      notes: this.dashboardApi.listTicketNotes(ticketId),
      messages: this.ticketApi.listTicketMessages(ticketId, 1, 50)
    })
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (payload) => {
          this.customerContext.set(payload.context);
          this.notes.set(payload.notes.items.slice().reverse());
          this.ticketMessages.set(payload.messages.items);
          this.mergeAgentOptionsFromNotes(payload.notes.items);
        },
        error: (err: Error) => this.error.set(`Ticket context load failed: ${err.message}`)
      });
  }

  private loadQuickReplies(): void {
    this.beginRequest(false);
    this.dashboardApi
      .listQuickReplies(true)
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (response) => this.quickReplies.set(response.items),
        error: (err: Error) => this.error.set(`Quick replies load failed: ${err.message}`)
      });
  }

  private loadHandoffRequests(): void {
    this.beginRequest(false);
    this.dashboardApi
      .getMyHandoffRequests()
      .pipe(finalize(() => this.finishRequest()))
      .subscribe({
        next: (response) => this.handoffRequests.set(response.items),
        error: (err: Error) => this.error.set(`Handoff inbox load failed: ${err.message}`)
      });
  }

  private loadAgentOptions(): void {
    const currentUserId = this.authService.currentUser()()?.userId.toLowerCase() ?? '';

    this.dashboardApi
      .getAssignableAgents()
      .pipe(
        map((response) =>
          response.items
            .map((item) => ({
              id: item.userId,
              displayName: item.displayName || item.email
            }))
            .filter((item) => !currentUserId || item.id.toLowerCase() !== currentUserId)
        ),
        catchError(() => of([] as AgentOption[]))
      )
      .subscribe({
        next: (agents) => {
          if (agents.length > 0) {
            this.agentOptions.set(agents);
            return;
          }

          this.mergeAgentOptionsFromNotes(this.notes());
        }
      });
  }

  private mergeAgentOptionsFromNotes(notes: TicketInternalNoteResponse[]): void {
    const current = this.agentOptions();
    const dictionary = new Map(current.map((item) => [item.id.toLowerCase(), item]));

    for (const note of notes) {
      dictionary.set(note.authorUserId.toLowerCase(), {
        id: note.authorUserId,
        displayName: note.authorDisplayName
      });

      for (const mention of note.mentions) {
        dictionary.set(mention.mentionedUserId.toLowerCase(), {
          id: mention.mentionedUserId,
          displayName: mention.mentionedDisplayName
        });
      }
    }

    const currentUser = this.authService.currentUser()();
    if (currentUser) {
      dictionary.delete(currentUser.userId.toLowerCase());
    }

    this.agentOptions.set(Array.from(dictionary.values()));
  }

  private beginRequest(clearMessages = true): void {
    this.loading.set(true);
    if (clearMessages) {
      this.error.set('');
      this.success.set('');
    }
  }

  private finishRequest(): void {
    this.loading.set(false);
  }
}
