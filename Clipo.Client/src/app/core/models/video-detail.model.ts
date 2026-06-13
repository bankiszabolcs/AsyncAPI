import { VideoAuthor, VideoThumbnail } from './video.model';

export interface VideoStream {
  quality: string;
  url: string;
}

export interface VideoDetailMedia {
  streams: VideoStream[];
  sprite: string;
  preview: string;
  thumbnails: VideoThumbnail[];
}

export interface VideoDetail {
  id: string;
  title: string;
  description: string | null;
  duration: number;
  publishedAt: string;
  statusId: number;
  status: string;
  author: VideoAuthor;
  tags: string[];
  media: VideoDetailMedia;
}
