import { Component, EventEmitter, Input, Output } from '@angular/core';
import { QuickReplyResponse } from '../../../core/models/agent-dashboard.models';
import { TicketMessageResponse } from '../../../core/models/ticket-management.models';
import { TicketConversationPanelComponent } from '../../tickets/components/ticket-conversation-panel.component';

@Component({
  selector: 'app-agent-ticket-conversation-panel',
  standalone: true,
  imports: [TicketConversationPanelComponent],
  template: `
    <app-ticket-conversation-panel
      [messages]="messages"
      [quickReplies]="quickReplies"
      [canCompose]="canCompose"
      [sending]="sending"
      [loading]="loading"
      (sendClicked)="sendClicked.emit($event)"
      (reloadClicked)="reloadClicked.emit()" />
  `
})
export class AgentTicketConversationPanelComponent {
  @Input() messages: TicketMessageResponse[] = [];
  @Input() quickReplies: QuickReplyResponse[] = [];
  @Input() canCompose = false;
  @Input() sending = false;
  @Input() loading = false;

  @Output() readonly sendClicked = new EventEmitter<string>();
  @Output() readonly reloadClicked = new EventEmitter<void>();
}
