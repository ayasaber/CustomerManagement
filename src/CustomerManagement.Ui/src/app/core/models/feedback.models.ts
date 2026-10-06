export interface CreateFeedbackRequest {
  rating: number;
  comment?: string;
}

export interface FeedbackResponse {
  id: string;
  customerId: string;
  customerName: string;
  rating: number;
  comment: string | null;
  createdAtUtc: string;
}
