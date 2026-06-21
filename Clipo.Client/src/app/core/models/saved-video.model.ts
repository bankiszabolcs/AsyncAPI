import { CardVideo } from '../../shared/video-card/video-card';

export interface SavedVideoItem {
  videoId: string;
  savedAt: string;
  video: CardVideo;
}
