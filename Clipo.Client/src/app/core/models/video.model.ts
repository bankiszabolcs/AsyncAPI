export interface VideoThumbnail {
  width: number;
  url: string;
}

export interface VideoAuthor {
  id: string;
  name: string;
  avatarUrl: string | null;
}

export interface VideoMedia {
  sprite: string;
  preview: string;
  hoverStream: string;
  thumbnails: VideoThumbnail[];
}

export interface Video {
  id: string;
  title: string;
  description: string | null;
  duration: number;
  publishedAt: string;
  viewCount: number;
  author: VideoAuthor;
  media: VideoMedia;
}
