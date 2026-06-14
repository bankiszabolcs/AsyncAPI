import { VideoAuthor, VideoThumbnail } from './video.model';

export interface VideoStream {
  quality: string;
  url: string;
}

export interface VideoDetailMedia {
  masterStream: string;
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
  likeCount: number;
  dislikeCount: number;
  userReaction: 1 | 2 | null;
  author: VideoAuthor;
  tags: string[];
  media: VideoDetailMedia;
}
