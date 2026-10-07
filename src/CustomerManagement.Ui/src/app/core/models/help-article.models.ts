export interface HelpArticleResponse {
  id: string;
  title: string;
  body: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface CreateHelpArticleRequest {
  title: string;
  body: string;
}

export interface UpdateHelpArticleRequest {
  title: string;
  body: string;
  isActive: boolean;
  rowVersion: number[];
}
