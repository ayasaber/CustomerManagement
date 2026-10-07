export type KnowledgeBaseContentType = 'Faq' | 'HelpArticle' | 'Guide';

export interface KnowledgeBaseSearchResultResponse {
  contentType: KnowledgeBaseContentType;
  id: string;
  title: string;
  snippet: string;
}

export interface KnowledgeBaseSearchResponse {
  query: string;
  results: KnowledgeBaseSearchResultResponse[];
}
