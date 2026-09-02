export interface DashboardAssignedTicketItemResponse {
  ticketId: string;
  ticketNumber: string;
  subject: string;
  status: string;
  priority: string;
  category: string;
  isEscalated: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  customerId: string;
  customerDisplayName: string;
}

export interface DashboardAssignedTicketsResponse {
  totalCount: number;
  items: DashboardAssignedTicketItemResponse[];
}

export interface DashboardOpenTaskSummaryItemResponse {
  taskId: string;
  ticketId: string;
  ticketNumber: string;
  description: string;
  dueAtUtc: string;
  createdAtUtc: string;
}

export interface DashboardOpenTaskSummaryResponse {
  totalCount: number;
  items: DashboardOpenTaskSummaryItemResponse[];
}

export interface DashboardCustomerInteractionItemResponse {
  interactionId: string;
  type: string;
  summary: string;
  occurredAtUtc: string;
}

export interface DashboardCustomerContextResponse {
  ticketId: string;
  customerId: string;
  customerDisplayName: string;
  company: string | null;
  primaryEmail: string | null;
  primaryPhone: string | null;
  recentInteractions: DashboardCustomerInteractionItemResponse[];
}

export interface TicketTaskResponse {
  id: string;
  ticketId: string;
  ticketNumber: string;
  createdByUserId: string;
  assignedToUserId: string;
  assignedToDisplayName: string;
  description: string;
  dueAtUtc: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  completedAtUtc: string | null;
  completedByUserId: string | null;
  rowVersion: string;
}

export interface TicketTaskListResponse {
  totalCount: number;
  items: TicketTaskResponse[];
}

export interface CreateTicketTaskRequest {
  ticketId: string;
  description: string;
  dueAtUtc: string;
  assignedToUserId: string | null;
}

export interface UpdateTicketTaskRequest {
  description: string;
  dueAtUtc: string;
  assignedToUserId: string;
  rowVersion: string;
}

export interface CompleteTicketTaskRequest {
  rowVersion: string;
}

export interface QuickReplyResponse {
  id: string;
  title: string;
  body: string;
  tags: string[];
  isActive: boolean;
  createdByUserId: string;
  updatedByUserId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: string;
}

export interface QuickReplyListResponse {
  totalCount: number;
  items: QuickReplyResponse[];
}

export interface CreateQuickReplyRequest {
  title: string;
  body: string;
  tags: string[];
}

export interface UpdateQuickReplyRequest {
  title: string;
  body: string;
  tags: string[];
  isActive: boolean;
  rowVersion: string;
}

export interface TicketInternalNoteMentionResponse {
  mentionedUserId: string;
  mentionedDisplayName: string;
  mentionedAtUtc: string;
  notificationDelivered: boolean;
}

export interface TicketInternalNoteResponse {
  id: string;
  ticketId: string;
  authorUserId: string;
  authorDisplayName: string;
  body: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: string;
  mentions: TicketInternalNoteMentionResponse[];
}

export interface TicketInternalNoteListResponse {
  totalCount: number;
  items: TicketInternalNoteResponse[];
}

export interface TicketReassignProposalResponse {
  ticketId: string;
  reassignToUserId: string;
}

export interface TicketInternalNoteCreateResponse {
  note: TicketInternalNoteResponse;
  reassignProposal: TicketReassignProposalResponse | null;
}

export interface CreateTicketInternalNoteRequest {
  ticketId: string;
  body: string;
  mentionedUserIds: string[];
  offerReassign: boolean;
  reassignToUserId: string | null;
}

export interface CreateTicketHandoffRequest {
  noteId: string;
  ticketId: string;
  targetAssigneeUserId: string;
  message: string | null;
  ticketRowVersion: number[];
}

export interface RespondTicketHandoffRequest {
  accept: boolean;
  message: string | null;
  ticketRowVersion: number[];
}

export interface TicketHandoffRequestResponse {
  id: string;
  ticketId: string;
  noteId: string;
  requestedByUserId: string;
  targetAssigneeUserId: string;
  status: string;
  responseMessage: string | null;
  requestedAtUtc: string;
  respondedAtUtc: string | null;
  updatedTicketAssigneeUserId: string | null;
}

export interface TicketHandoffRequestListResponse {
  totalCount: number;
  items: TicketHandoffRequestResponse[];
}

export interface AgentOption {
  id: string;
  displayName: string;
}

export interface DashboardAgentDirectoryItemResponse {
  userId: string;
  displayName: string;
  email: string;
}

export interface DashboardAgentDirectoryResponse {
  totalCount: number;
  items: DashboardAgentDirectoryItemResponse[];
}
