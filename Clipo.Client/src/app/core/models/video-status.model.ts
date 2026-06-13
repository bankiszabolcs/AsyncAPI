export interface VideoStatusLinks {
  master: string;
  sprite: string;
  preview: string;
  [key: string]: string;
}

export interface VideoStatus {
  id: string;
  status: 'Queued' | 'Processing' | 'Completed' | 'Failed';
  links: VideoStatusLinks;
}
