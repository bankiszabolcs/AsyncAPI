import { CardVideo } from '../../shared/video-card/video-card';

export interface WatchLaterItem {
  videoId: string;
  addedAt: string;
  video: CardVideo;
}
