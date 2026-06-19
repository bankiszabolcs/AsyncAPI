// PUT /videos/{id} kérés és válasz
export interface UpdateVideoRequest {
  title: string;
  description: string | null;
  visibilityId: number;
  tags: string[];
}

export interface UpdateVideoResponse {
  id: string;
  title: string;
  description: string | null;
  statusId: number;
  status: string;
  visibilityId: number;
  tags: string[];
}
