export interface GuideStepResponse {
  stepNumber: number;
  instruction: string;
}

export interface GuideResponse {
  id: string;
  title: string;
  isActive: boolean;
  steps: GuideStepResponse[];
  createdAtUtc: string;
  updatedAtUtc: string;
  rowVersion: number[];
}

export interface CreateGuideRequest {
  title: string;
  steps: string[];
}

export interface UpdateGuideRequest {
  title: string;
  steps: string[];
  isActive: boolean;
  rowVersion: number[];
}
