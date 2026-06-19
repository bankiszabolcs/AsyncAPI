import { VideoThumbnail } from './video.model';

export interface MyVideoMedia {
  thumbnails: VideoThumbnail[] | null;
  hoverStream: string | null;
  preview: string | null;
}

export interface MyVideo {
  id: string;
  title: string;
  description: string | null;
  duration: number;
  publishedAt: string | null;
  viewCount: number;
  statusId: number;
  status: string;
  visibilityId: number;
  tags: string[];
  media: MyVideoMedia;
}
