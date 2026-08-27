export interface CreateCustomerContactDetailRequest {
  channel: number;
  value: string;
  label?: string | null;
  isPrimary: boolean;
}

export interface CreateCustomerProfileRequest {
  name: string;
  company?: string | null;
  contactDetails?: CreateCustomerContactDetailRequest[] | null;
}

export interface UpdateCustomerProfileRequest {
  name: string;
  company?: string | null;
  rowVersion: string;
  contactDetails?: CreateCustomerContactDetailRequest[] | null;
}

export interface CustomerContactDetailResponse {
  id: string;
  channel: number;
  value: string;
  label?: string | null;
  isPrimary: boolean;
  createdAtUtc: string;
}

export interface CustomerProfileResponse {
  id: string;
  applicationUserId?: string | null;
  name: string;
  company?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: string;
  contactDetails: CustomerContactDetailResponse[];
}

export interface CustomerListItemResponse {
  id: string;
  applicationUserId?: string | null;
  name: string;
  company?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  primaryEmail?: string | null;
}

export interface CustomerListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: CustomerListItemResponse[];
}

export interface CustomerContactDetailsResponse {
  customerId: string;
  contactDetails: CustomerContactDetailResponse[];
}

export interface CreateCustomerNoteRequest {
  body: string;
}

export interface CustomerNoteResponse {
  id: string;
  customerId: string;
  body: string;
  createdBy: string;
  createdAtUtc: string;
}

export interface CustomerAttachmentResponse {
  id: string;
  customerId: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  storageKey: string;
  createdBy: string;
  createdAtUtc: string;
}

export interface CustomerInteractionHistoryItemResponse {
  interactionId: string;
  channel: string;
  direction: string;
  timestampUtc: string;
  summary?: string | null;
  sourceRef?: string | null;
  sourceSystem?: string | null;
}

export interface CustomerInteractionHistoryResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: CustomerInteractionHistoryItemResponse[];
}

export interface ApiValidationError {
  errors?: Record<string, string[]>;
  title?: string;
  status?: number;
}
