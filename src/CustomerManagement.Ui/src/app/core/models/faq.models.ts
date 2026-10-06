export interface FaqEntryResponse {
  id: string;
  topic: string;
  question: string;
  answer: string;
  sortOrder: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface CreateFaqEntryRequest {
  topic: string;
  question: string;
  answer: string;
  sortOrder: number;
}

export interface UpdateFaqEntryRequest {
  topic: string;
  question: string;
  answer: string;
  sortOrder: number;
  isActive: boolean;
  rowVersion: number[];
}
