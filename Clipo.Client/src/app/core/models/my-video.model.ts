import { VideoThumbnail } from './video.model';

export interface MyVideoMedia {
  thumbnails: VideoThumbnail[] | null;
}

export interface MyVideo {
  id: string;
  title: string;
  duration: number;
  publishedAt: string | null;
  statusId: number;
  status: string;
  media: MyVideoMedia;
}
