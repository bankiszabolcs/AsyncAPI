export interface VideoThumbnail {
  width: number;
  url: string;
}

export interface VideoAuthor {
  id: string;
  name: string;
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
  author: VideoAuthor;
  media: VideoMedia;
}
