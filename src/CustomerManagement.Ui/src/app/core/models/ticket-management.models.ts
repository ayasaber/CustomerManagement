export interface CreateTicketCategoryRequest {
  name: string;
  description: string | null;
}

export interface UpdateTicketCategoryRequest {
  name: string;
  description: string | null;
  isActive: boolean;
  rowVersion: number[];
}

export interface TicketCategoryResponse {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface CreateTicketPriorityRequest {
  name: string;
  sortOrder: number;
}

export interface UpdateTicketPriorityRequest {
  name: string;
  sortOrder: number;
  isActive: boolean;
  rowVersion: number[];
}

export interface TicketPriorityResponse {
  id: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface CreateTicketRequest {
  customerId: string;
  categoryId: string;
  priorityId: string;
  subject: string;
  description: string;
}

export interface UpdateTicketStatusRequest {
  targetStatus: string;
  rowVersion: number[];
}

export interface TicketListItemResponse {
  id: string;
  customerId: string;
  assignedToUserId: string | null;
  assignedToUserEmail: string | null;
  categoryId: string;
  categoryName: string;
  priorityId: string;
  priorityName: string;
  subject: string;
  status: string;
  isEscalated: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface TicketListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: TicketListItemResponse[];
}

export interface TicketResponse {
  id: string;
  customerId: string;
  assignedToUserId: string | null;
  assignedToUserEmail: string | null;
  categoryId: string;
  categoryName: string;
  categoryIsActive: boolean;
  priorityId: string;
  priorityName: string;
  priorityIsActive: boolean;
  prioritySortOrder: number;
  subject: string;
  description: string;
  status: string;
  isEscalated: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface TicketHistoryItemResponse {
  id: string;
  actionType: string;
  fieldName: string;
  oldValue: string | null;
  newValue: string | null;
  actorUserId: string | null;
  actorEmail: string | null;
  occurredAtUtc: string;
}

export interface TicketHistoryResponse {
  ticketId: string;
  items: TicketHistoryItemResponse[];
}

export interface ApiValidationError {
  title?: string;
  errors?: Record<string, string[]>;
}
