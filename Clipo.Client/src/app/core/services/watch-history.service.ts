import { Injectable } from '@angular/core';
import { VideoDetail } from '../models/video-detail.model';
import { CardVideo } from '../../shared/video-card/video-card';

const HISTORY_KEY = 'clipo_watch_history';
const MAX_ENTRIES = 100;

interface HistoryEntry extends CardVideo {
  watchedAt: string;
}

@Injectable({ providedIn: 'root' })
export class WatchHistoryService {

  record(video: VideoDetail): void {
    const entry: HistoryEntry = {
      id:          video.id,
      title:       video.title,
      duration:    video.duration,
      publishedAt: video.publishedAt,
      statusId:    3,
      author: {
        id:        video.author.id,
        name:      video.author.name,
        avatarUrl: null,
      },
      media: {
        thumbnails:  video.media.thumbnails,
        hoverStream: video.media.streams?.find(s => s.quality === '480p')?.url ?? null,
        preview:     video.media.preview ?? null,
      },
      watchedAt: new Date().toISOString(),
    };

    const history = this.load().filter(h => h.id !== video.id);
    history.unshift(entry);
    localStorage.setItem(HISTORY_KEY, JSON.stringify(history.slice(0, MAX_ENTRIES)));
  }

  getHistory(): HistoryEntry[] {
    return this.load();
  }

  clear(): void {
    localStorage.removeItem(HISTORY_KEY);
  }

  private load(): HistoryEntry[] {
    try {
      return JSON.parse(localStorage.getItem(HISTORY_KEY) ?? '[]');
    } catch {
      return [];
    }
  }
}
